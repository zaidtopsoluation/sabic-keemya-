using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keemya.Frontend.Services
{
    public enum SyncAction { None, CopyToPeer, CopyToLocal, DeleteOnPeer, DeleteLocal }

    /// <summary>
    /// Two-way sync between this server's own MySQL and the other server's MySQL, so both hold the same
    /// sirens, users, buttons, logs, etc. Runs only on Server-role PCs. Every row is compared on both sides
    /// against its hash at the last sync (kept in the local SyncState table), which tells apart
    /// "added here", "edited there" and "deleted there" without touching the code that writes the data.
    /// </summary>
    public class DatabaseSyncService
    {
        // Per-server command queue and sync bookkeeping never leave their server.
        private static readonly HashSet<string> SkipTables = new(StringComparer.OrdinalIgnoreCase)
            { "pendingcommands", "syncstate", "__efmigrationshistory" };

        // Live state each server measures itself; syncing it would make the servers overwrite each other every poll.
        private static readonly Dictionary<string, string[]> LocalOnlyColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sirendevices"] = new[] { "Status", "AcFailed", "ApmFailed", "DoorIntruded" },
            ["stationstatuses"] = new[] { "Status" },
        };

        // On a conflict (edited on both servers) the row with the newer value here wins; otherwise the preferred server wins.
        private static readonly Dictionary<string, string> NewestWinsColumn = new(StringComparer.OrdinalIgnoreCase)
        {
            ["stationstatuses"] = "LastHeartbeat",
        };

        // Single-row settings tables: id 1 is the same record on both servers, so key by id instead of SyncId.
        private static readonly HashSet<string> KeyByIdTables = new(StringComparer.OrdinalIgnoreCase) { "notification_settings" };

        private const char KeySep = '\u001F';
        private const int Chunk = 100;

        private readonly string _localConn;
        private readonly string _peerConn;
        private readonly bool _localPreferred;
        private readonly Action<string> _log;

        public DatabaseSyncService(string localConnectionString, string peerConnectionString, bool localPreferred, Action<string>? log = null)
        {
            _localConn = WithTimeout(localConnectionString);
            _peerConn = WithTimeout(peerConnectionString);
            _localPreferred = localPreferred;
            _log = log ?? (_ => { });
        }

        private static string WithTimeout(string cs) =>
            new MySqlConnectionStringBuilder(cs) { ConnectionTimeout = 3 }.ConnectionString;

        /// <summary>3-way merge decision for one row. Hashes are null when the row does not exist (or was never synced).</summary>
        public static SyncAction Decide(string? local, string? peer, string? baseHash, bool localWinsConflict)
        {
            if (local == null && peer == null) return SyncAction.None;
            if (local != null && peer != null)
            {
                if (local == peer) return SyncAction.None;
                if (local == baseHash) return SyncAction.CopyToLocal;   // only peer changed
                if (peer == baseHash) return SyncAction.CopyToPeer;     // only local changed
                return localWinsConflict ? SyncAction.CopyToPeer : SyncAction.CopyToLocal;
            }
            if (local != null)
            {
                // New here -> copy. Deleted on peer and unchanged here -> delete. Edited here but deleted there -> keep the edit.
                return local == baseHash ? SyncAction.DeleteLocal : SyncAction.CopyToPeer;
            }
            return peer == baseHash ? SyncAction.DeleteOnPeer : SyncAction.CopyToLocal;
        }

        private record Column(string Name, string DataType, bool IsPk, bool IsAutoInc);
        private record RowInfo(string Hash, string? Newest);

        /// <summary>Runs one full sync pass. Returns the number of rows copied or deleted.</summary>
        public async Task<int> SyncOnceAsync()
        {
            using var local = new MySqlConnection(_localConn);
            using var peer = new MySqlConnection(_peerConn);
            await local.OpenAsync();
            await peer.OpenAsync();

            // Rows arrive in any table order, so parent rows may come after their children.
            await ExecAsync(local, "SET FOREIGN_KEY_CHECKS = 0");
            await ExecAsync(peer, "SET FOREIGN_KEY_CHECKS = 0");
            await ExecAsync(local, @"CREATE TABLE IF NOT EXISTS `SyncState` (
                `TableName` varchar(64) NOT NULL, `RowKey` varchar(255) NOT NULL, `RowHash` char(32) NOT NULL,
                PRIMARY KEY (`TableName`, `RowKey`))");

            var localTables = await GetTablesAsync(local);
            var peerTables = await GetTablesAsync(peer);

            // Auto-increment ids clash between servers (both create id 5), so those rows are matched by a UUID instead.
            bool schemaChanged = false;
            foreach (var t in localTables.Keys.Where(t => peerTables.ContainsKey(t) && NeedsSyncId(t, localTables[t])).ToList())
            {
                schemaChanged |= await EnsureSyncIdAsync(local, t, localTables[t]);
                schemaChanged |= await EnsureSyncIdAsync(peer, t, peerTables[t]);
            }
            if (schemaChanged)
            {
                localTables = await GetTablesAsync(local);
                peerTables = await GetTablesAsync(peer);
            }

            int changes = 0;
            foreach (var (table, localCols) in localTables)
            {
                if (SkipTables.Contains(table) || !peerTables.TryGetValue(table, out var peerCols)) continue;
                changes += await SyncTableAsync(local, peer, table, localCols, peerCols);
            }
            return changes;
        }

        private static bool NeedsSyncId(string table, List<Column> cols)
        {
            var pk = cols.Where(c => c.IsPk).ToList();
            return !SkipTables.Contains(table) && !KeyByIdTables.Contains(table) && pk.Count == 1 && pk[0].IsAutoInc;
        }

        private static async Task<bool> EnsureSyncIdAsync(MySqlConnection conn, string table, List<Column> cols)
        {
            bool added = false;
            if (!cols.Any(c => c.Name.Equals("SyncId", StringComparison.OrdinalIgnoreCase)))
            {
                // No DEFAULT (UUID()): MySQL with binary logging rejects it as unsafe, so ids are assigned below.
                await ExecAsync(conn, $"ALTER TABLE `{table}` ADD COLUMN `SyncId` char(36) NULL, ADD UNIQUE KEY `UX_{table}_SyncId` (`SyncId`)");
                added = true;
            }

            // Rows written by app code since the last pass have no SyncId yet.
            string idCol = cols.First(c => c.IsPk).Name;
            var ids = new List<object>();
            using (var sel = new MySqlCommand($"SELECT `{idCol}` FROM `{table}` WHERE `SyncId` IS NULL", conn))
            using (var rdr = await sel.ExecuteReaderAsync())
                while (await rdr.ReadAsync()) ids.Add(rdr.GetValue(0));
            foreach (var id in ids)
            {
                // "AND SyncId IS NULL": if the other server's sync loop got here first, keep its id.
                using var upd = new MySqlCommand($"UPDATE `{table}` SET `SyncId` = @g WHERE `{idCol}` = @id AND `SyncId` IS NULL", conn);
                upd.Parameters.AddWithValue("@g", Guid.NewGuid().ToString());
                upd.Parameters.AddWithValue("@id", id);
                await upd.ExecuteNonQueryAsync();
            }
            return added;
        }

        private static async Task<Dictionary<string, List<Column>>> GetTablesAsync(MySqlConnection conn)
        {
            var result = new Dictionary<string, List<Column>>(StringComparer.OrdinalIgnoreCase);
            using var cmd = new MySqlCommand(@"SELECT c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE, c.COLUMN_KEY, c.EXTRA
                FROM information_schema.COLUMNS c
                JOIN information_schema.TABLES t ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
                WHERE c.TABLE_SCHEMA = DATABASE() AND t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION", conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                string table = rdr.GetString(0);
                if (!result.TryGetValue(table, out var list)) result[table] = list = new List<Column>();
                list.Add(new Column(rdr.GetString(1), rdr.GetString(2).ToLowerInvariant(),
                    rdr.GetString(3) == "PRI", rdr.GetString(4).Contains("auto_increment", StringComparison.OrdinalIgnoreCase)));
            }
            return result;
        }

        private async Task<int> SyncTableAsync(MySqlConnection local, MySqlConnection peer, string table, List<Column> localCols, List<Column> peerCols)
        {
            var peerNames = new HashSet<string>(peerCols.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            var cols = localCols.Where(c => peerNames.Contains(c.Name)).ToList();

            bool syncIdMode = NeedsSyncId(table, localCols);
            var keyCols = syncIdMode ? new List<string> { "SyncId" } : cols.Where(c => c.IsPk).Select(c => c.Name).ToList();
            if (keyCols.Count == 0 || keyCols.Any(k => !peerNames.Contains(k))) return 0;

            var localOnly = new HashSet<string>(LocalOnlyColumns.GetValueOrDefault(table) ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var insertCols = cols.Where(c => !(syncIdMode && c.IsAutoInc)).ToList();
            var hashCols = insertCols.Where(c => !keyCols.Contains(c.Name, StringComparer.OrdinalIgnoreCase) && !localOnly.Contains(c.Name)).ToList();
            string? newestCol = NewestWinsColumn.TryGetValue(table, out var n) && cols.Any(c => c.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) ? n : null;

            string keyExpr = $"CONCAT_WS(CHAR(31 USING utf8mb4), {string.Join(", ", keyCols.Select(k => $"CAST(`{k}` AS CHAR)"))})";
            string hashExpr = hashCols.Count == 0 ? "MD5('')" :
                $"MD5(CONCAT_WS(CHAR(31 USING utf8mb4), {string.Join(", ", hashCols.Select(ValueExpr))}))";
            string sql = $"SELECT {keyExpr}, {hashExpr}{(newestCol != null ? $", CAST(`{newestCol}` AS CHAR)" : "")} FROM `{table}`";

            var localRows = await ReadHashesAsync(local, sql);
            var peerRows = await ReadHashesAsync(peer, sql);
            var baseRows = await ReadBaseAsync(local, table);

            var toPeer = new List<string>(); var toLocal = new List<string>();
            var delPeer = new List<string>(); var delLocal = new List<string>();
            var newBase = new Dictionary<string, string?>();

            foreach (var key in localRows.Keys.Union(peerRows.Keys).Union(baseRows.Keys))
            {
                localRows.TryGetValue(key, out var l);
                peerRows.TryGetValue(key, out var p);
                baseRows.TryGetValue(key, out var b);

                bool localWins = _localPreferred;
                if (newestCol != null && l != null && p != null && l.Newest != p.Newest)
                    localWins = string.CompareOrdinal(l.Newest ?? "", p.Newest ?? "") > 0;

                switch (Decide(l?.Hash, p?.Hash, b, localWins))
                {
                    case SyncAction.CopyToPeer: toPeer.Add(key); newBase[key] = l!.Hash; break;
                    case SyncAction.CopyToLocal: toLocal.Add(key); newBase[key] = p!.Hash; break;
                    case SyncAction.DeleteOnPeer: delPeer.Add(key); newBase[key] = null; break;
                    case SyncAction.DeleteLocal: delLocal.Add(key); newBase[key] = null; break;
                    default:
                        string? settled = l != null && l.Hash == p?.Hash ? l.Hash : null;
                        if (settled != b) newBase[key] = settled;
                        break;
                }
            }

            if (toPeer.Count + toLocal.Count + delPeer.Count + delLocal.Count > 0)
            {
                await CopyRowsAsync(local, peer, table, keyCols, insertCols, hashCols, toPeer);
                await CopyRowsAsync(peer, local, table, keyCols, insertCols, hashCols, toLocal);
                await DeleteRowsAsync(peer, table, keyCols, delPeer);
                await DeleteRowsAsync(local, table, keyCols, delLocal);
                _log($"🔄 [DB Sync] {table}: {toPeer.Count} copied to peer, {toLocal.Count} copied here, {delPeer.Count} deleted on peer, {delLocal.Count} deleted here.");
            }
            await WriteBaseAsync(local, table, newBase);
            return toPeer.Count + toLocal.Count + delPeer.Count + delLocal.Count;
        }

        private static readonly HashSet<string> BinaryTypes = new() { "binary", "varbinary", "blob", "tinyblob", "mediumblob", "longblob", "bit" };

        private static string ValueExpr(Column c) =>
            BinaryTypes.Contains(c.DataType)
                ? $"COALESCE(HEX(`{c.Name}`), '~NULL~')"
                : $"COALESCE(CAST(`{c.Name}` AS CHAR), '~NULL~')";

        private static async Task<Dictionary<string, RowInfo>> ReadHashesAsync(MySqlConnection conn, string sql)
        {
            var rows = new Dictionary<string, RowInfo>();
            using var cmd = new MySqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                if (rdr.IsDBNull(0)) continue;
                rows[rdr.GetString(0)] = new RowInfo(rdr.GetString(1), rdr.FieldCount > 2 && !rdr.IsDBNull(2) ? rdr.GetString(2) : null);
            }
            return rows;
        }

        private static async Task<Dictionary<string, string>> ReadBaseAsync(MySqlConnection conn, string table)
        {
            var rows = new Dictionary<string, string>();
            using var cmd = new MySqlCommand("SELECT RowKey, RowHash FROM SyncState WHERE TableName = @t", conn);
            cmd.Parameters.AddWithValue("@t", table);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync()) rows[rdr.GetString(0)] = rdr.GetString(1);
            return rows;
        }

        private static async Task WriteBaseAsync(MySqlConnection conn, string table, Dictionary<string, string?> changes)
        {
            foreach (var (key, hash) in changes)
            {
                using var cmd = new MySqlCommand(hash == null
                    ? "DELETE FROM SyncState WHERE TableName = @t AND RowKey = @k"
                    : "INSERT INTO SyncState (TableName, RowKey, RowHash) VALUES (@t, @k, @h) ON DUPLICATE KEY UPDATE RowHash = @h", conn);
                cmd.Parameters.AddWithValue("@t", table);
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@h", hash);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static string KeyWhere(List<string> keyCols, IReadOnlyList<string> keys, MySqlCommand cmd)
        {
            var groups = new List<string>();
            for (int i = 0; i < keys.Count; i++)
            {
                var parts = keys[i].Split(KeySep);
                groups.Add("(" + string.Join(" AND ", keyCols.Select((k, j) =>
                {
                    cmd.Parameters.AddWithValue($"@k{i}_{j}", parts[j]);
                    return $"`{k}` = @k{i}_{j}";
                })) + ")");
            }
            return string.Join(" OR ", groups);
        }

        private static async Task CopyRowsAsync(MySqlConnection src, MySqlConnection dst, string table, List<string> keyCols,
            List<Column> insertCols, List<Column> updateCols, List<string> keys)
        {
            string colList = string.Join(", ", insertCols.Select(c => $"`{c.Name}`"));
            string update = updateCols.Count > 0
                ? string.Join(", ", updateCols.Select(c => $"`{c.Name}` = VALUES(`{c.Name}`)"))
                : $"`{keyCols[0]}` = `{keyCols[0]}`";

            foreach (var chunk in keys.Chunk(Chunk))
            {
                var rows = new List<object[]>();
                using (var sel = new MySqlCommand { Connection = src })
                {
                    sel.CommandText = $"SELECT {colList} FROM `{table}` WHERE {KeyWhere(keyCols, chunk, sel)}";
                    using var rdr = await sel.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        var values = new object[insertCols.Count];
                        rdr.GetValues(values);
                        rows.Add(values);
                    }
                }

                foreach (var values in rows)
                {
                    using var ins = new MySqlCommand(
                        $"INSERT INTO `{table}` ({colList}) VALUES ({string.Join(", ", insertCols.Select((_, i) => $"@v{i}"))}) ON DUPLICATE KEY UPDATE {update}", dst);
                    for (int i = 0; i < values.Length; i++) ins.Parameters.AddWithValue($"@v{i}", values[i]);
                    await ins.ExecuteNonQueryAsync();
                }
            }
        }

        private static async Task DeleteRowsAsync(MySqlConnection conn, string table, List<string> keyCols, List<string> keys)
        {
            foreach (var chunk in keys.Chunk(Chunk))
            {
                using var cmd = new MySqlCommand { Connection = conn };
                cmd.CommandText = $"DELETE FROM `{table}` WHERE {KeyWhere(keyCols, chunk, cmd)}";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static async Task ExecAsync(MySqlConnection conn, string sql)
        {
            using var cmd = new MySqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Background loop on Server PCs ───────────────────────────────────

        public static bool? LastSyncOk { get; private set; }

        /// <summary>Starts the sync loop if this PC is a server with a different peer database. Safe to call once at startup.</summary>
        public static void StartForThisServer()
        {
            if (!AppConfig.IsControllerStation) return;
            if (string.Equals(AppConfig.PrimaryConnectionString, AppConfig.SecondaryConnectionString, StringComparison.OrdinalIgnoreCase)) return;

            // Both servers must agree who wins a conflict, so use a fixed order ("Server 1 (RCB)" < "Server 2 (PCB)").
            bool localPreferred = string.CompareOrdinal(AppConfig.PrimaryServerName, AppConfig.SecondaryServerName) < 0;
            // Create the comm service here on the UI thread: its constructor logs via Dispatcher.Invoke, so creating it
            // first from the background loop deadlocks with the UI thread waiting on the same Lazy instance.
            var comm = SirenCommunicationService.Instance;
            Action<string> log = comm.Log;
            var sync = new DatabaseSyncService(AppConfig.PrimaryConnectionString, AppConfig.SecondaryConnectionString, localPreferred, log);

            Task.Run(async () =>
            {
                log($"🔄 [DB Sync] Started: {AppConfig.PrimaryServerName} <-> {AppConfig.SecondaryServerName} every 3 s.");
                while (true)
                {
                    try
                    {
                        await sync.SyncOnceAsync();
                        if (LastSyncOk != true) log($"✅ [DB Sync] Databases in sync with {AppConfig.SecondaryServerName}.");
                        LastSyncOk = true;
                    }
                    catch (Exception ex)
                    {
                        // Log only on the transition, so an offline peer does not flood the log every 3 s.
                        if (LastSyncOk != false)
                        {
                            log($"⚠️ [DB Sync] Cannot sync with {AppConfig.SecondaryServerName}: {ex.Message}. Retrying every 3 s; changes are kept and sent when it is back.");
                            _ = new NotificationService().AddNotificationAsync("Database Sync",
                                $"Cannot reach {AppConfig.SecondaryServerName}'s database. Changes will sync automatically when it is back.", "Warning");
                        }
                        LastSyncOk = false;
                    }
                    await Task.Delay(3000);
                }
            });
        }
    }
}
