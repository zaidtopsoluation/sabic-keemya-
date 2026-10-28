using Keemya.Frontend.Services;
using MySqlConnector;
using Xunit;
using Xunit.Abstractions;

namespace Keemya.Frontend.Tests;

public class SyncDecideTests
{
    [Theory]
    // both sides present
    [InlineData("A", "A", null, SyncAction.None)]
    [InlineData("A", "A", "X", SyncAction.None)]
    [InlineData("A", "B", "A", SyncAction.CopyToLocal)]    // only peer edited
    [InlineData("B", "A", "A", SyncAction.CopyToPeer)]     // only local edited
    [InlineData("B", "C", "A", SyncAction.CopyToPeer)]     // both edited, local preferred
    [InlineData("B", "C", null, SyncAction.CopyToPeer)]    // same row created on both, local preferred
    // one side only
    [InlineData("A", null, null, SyncAction.CopyToPeer)]   // new here
    [InlineData(null, "A", null, SyncAction.CopyToLocal)]  // new there
    [InlineData("A", null, "A", SyncAction.DeleteLocal)]   // deleted there
    [InlineData(null, "A", "A", SyncAction.DeleteOnPeer)]  // deleted here
    [InlineData("B", null, "A", SyncAction.CopyToPeer)]    // edited here, deleted there -> keep edit
    [InlineData(null, "B", "A", SyncAction.CopyToLocal)]   // deleted here, edited there -> keep edit
    [InlineData(null, null, "A", SyncAction.None)]
    public void Decide_LocalPreferred(string? local, string? peer, string? baseHash, SyncAction expected)
    {
        Assert.Equal(expected, DatabaseSyncService.Decide(local, peer, baseHash, localWinsConflict: true));
    }

    [Fact]
    public void Decide_Conflict_PeerPreferred()
    {
        Assert.Equal(SyncAction.CopyToLocal, DatabaseSyncService.Decide("B", "C", "A", localWinsConflict: false));
    }
}

/// <summary>
/// Real two-database test on the local MySQL (two scratch databases stand in for Server 1 and Server 2).
/// Set KEEMYA_TEST_MYSQL to override the server login; the test is skipped (passes with a note) when MySQL is not reachable.
/// </summary>
public class DatabaseSyncIntegrationTests : IAsyncLifetime
{
    private const string DbA = "keemya_synctest_a";
    private const string DbB = "keemya_synctest_b";
    private readonly ITestOutputHelper _out;
    private readonly string _server = Environment.GetEnvironmentVariable("KEEMYA_TEST_MYSQL")
        ?? "Server=localhost;Port=3306;User=root;Password=root1234;";
    private bool _available;

    public DatabaseSyncIntegrationTests(ITestOutputHelper output) => _out = output;

    private string Conn(string db) => new MySqlConnectionStringBuilder(_server) { Database = db }.ConnectionString;

    private const string Schema = @"
        CREATE TABLE sirengroups (Id char(36) NOT NULL PRIMARY KEY, Name longtext NOT NULL);
        CREATE TABLE sirendevices (Id char(36) NOT NULL PRIMARY KEY, Name longtext NOT NULL, Lat double NOT NULL,
            Status longtext NOT NULL, Redundant tinyint(1) NOT NULL, GroupId char(36) NULL,
            CONSTRAINT FK_g FOREIGN KEY (GroupId) REFERENCES sirengroups (Id));
        CREATE TABLE userroles (RolesId char(36) NOT NULL, UsersId char(36) NOT NULL, PRIMARY KEY (RolesId, UsersId));
        CREATE TABLE auditlogs (Id char(36) NOT NULL PRIMARY KEY, Timestamp datetime(6) NOT NULL, EntityData json NOT NULL);
        CREATE TABLE audit_logs (id INT AUTO_INCREMENT PRIMARY KEY, actor VARCHAR(255) NOT NULL, description TEXT, timestamp DATETIME DEFAULT CURRENT_TIMESTAMP);
        CREATE TABLE notification_settings (id INT AUTO_INCREMENT PRIMARY KEY, email_enabled TINYINT(1) DEFAULT 1);
        CREATE TABLE stationstatuses (Id char(36) NOT NULL PRIMARY KEY, Name varchar(255) NOT NULL UNIQUE,
            LastHeartbeat datetime(6) NULL, Status varchar(50) NOT NULL DEFAULT 'OFFLINE');
        CREATE TABLE pendingcommands (Id char(36) NOT NULL PRIMARY KEY, FrameHex text NOT NULL);";

    public async Task InitializeAsync()
    {
        try
        {
            using var c = new MySqlConnection(_server);
            await c.OpenAsync();
            foreach (var db in new[] { DbA, DbB })
                await Exec(c, $"DROP DATABASE IF EXISTS {db}; CREATE DATABASE {db};");
            foreach (var db in new[] { DbA, DbB })
                await Exec(Conn(db), Schema);
            _available = true;
        }
        catch (Exception ex)
        {
            _out.WriteLine($"MySQL not reachable, integration test skipped: {ex.Message}");
        }
    }

    public async Task DisposeAsync()
    {
        if (!_available) return;
        using var c = new MySqlConnection(_server);
        await c.OpenAsync();
        await Exec(c, $"DROP DATABASE IF EXISTS {DbA}; DROP DATABASE IF EXISTS {DbB};");
    }

    private static async Task Exec(MySqlConnection c, string sql)
    {
        using var cmd = new MySqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task Exec(string conn, string sql)
    {
        using var c = new MySqlConnection(conn);
        await c.OpenAsync();
        await Exec(c, sql);
    }

    private async Task<object?> Scalar(string db, string sql)
    {
        using var c = new MySqlConnection(Conn(db));
        await c.OpenAsync();
        using var cmd = new MySqlCommand(sql, c);
        return await cmd.ExecuteScalarAsync();
    }

    private async Task<long> Count(string db, string table, string where = "1=1") =>
        Convert.ToInt64(await Scalar(db, $"SELECT COUNT(*) FROM {table} WHERE {where}"));

    private static string Id(int n) => $"00000000-0000-0000-0000-{n:D12}";

    [Fact]
    public async Task TwoServers_EndUpWithSameData()
    {
        if (!_available) return;

        // Server 1 = A (preferred), Server 2 = B. Both run the sync loop, like the real servers.
        var s1 = new DatabaseSyncService(Conn(DbA), Conn(DbB), localPreferred: true, _out.WriteLine);
        var s2 = new DatabaseSyncService(Conn(DbB), Conn(DbA), localPreferred: false, _out.WriteLine);

        // 4 sirens added on Server 1, 3 on Server 2 (one in a zone that exists only on Server 2)
        await Exec(Conn(DbA), $@"INSERT INTO sirendevices VALUES
            ('{Id(1)}','S1-A',27.023997123,'ONLINE',0,NULL),('{Id(2)}','S1-B',27.1,'ONLINE',1,NULL),
            ('{Id(3)}','S1-C',27.2,'OFFLINE',0,NULL),('{Id(4)}','S1-D',27.3,'WARNING',0,NULL);
            INSERT INTO audit_logs (actor, description) VALUES ('admin','added sirens on S1'),('admin','second S1 log');
            INSERT INTO userroles VALUES ('{Id(50)}','{Id(60)}');
            INSERT INTO auditlogs VALUES ('{Id(70)}', '2026-10-25 10:11:12.123456', '{{""b"": 2, ""a"": [1, null]}}');
            INSERT INTO notification_settings (email_enabled) VALUES (1);
            INSERT INTO pendingcommands VALUES ('{Id(90)}','0281');");
        await Exec(Conn(DbB), $@"INSERT INTO sirengroups VALUES ('{Id(40)}','Zone North');
            INSERT INTO sirendevices VALUES
            ('{Id(5)}','S2-A',26.5,'ONLINE',0,'{Id(40)}'),('{Id(6)}','S2-B',26.6,'ONLINE',0,NULL),('{Id(7)}','S2-C',26.7,'ONLINE',0,NULL);
            INSERT INTO audit_logs (actor, description) VALUES ('operator','added sirens on S2'),('operator','second S2 log');
            INSERT INTO notification_settings (email_enabled) VALUES (0);");

        await s1.SyncOnceAsync();

        Assert.Equal(7, await Count(DbA, "sirendevices"));
        Assert.Equal(7, await Count(DbB, "sirendevices"));
        Assert.Equal(1, await Count(DbA, "sirengroups"));
        Assert.Equal(4, await Count(DbA, "audit_logs"));   // ids 1,2 on both servers were different logs -> all 4 kept
        Assert.Equal(4, await Count(DbB, "audit_logs"));
        Assert.Equal(1, await Count(DbB, "userroles"));
        Assert.Equal(1, await Count(DbB, "auditlogs"));
        Assert.Equal(1, await Count(DbB, "notification_settings"));   // single settings row, Server 1's value wins
        Assert.Equal(1L, Convert.ToInt64(await Scalar(DbB, "SELECT email_enabled FROM notification_settings")));
        Assert.Equal(0, await Count(DbB, "pendingcommands"));   // command queue is never synced
        Assert.Equal(27.023997123, Convert.ToDouble(await Scalar(DbB, $"SELECT Lat FROM sirendevices WHERE Id='{Id(1)}'")));

        // Values survive the round trip exactly: neither server sees a change any more (no endless copying)
        Assert.Equal(0, await s1.SyncOnceAsync());
        Assert.Equal(0, await s2.SyncOnceAsync());

        // Edit on Server 2 reaches Server 1; delete on Server 1 reaches Server 2
        await Exec(Conn(DbB), $"UPDATE sirendevices SET Name='S1-A renamed on S2' WHERE Id='{Id(1)}'");
        await Exec(Conn(DbA), $"DELETE FROM sirendevices WHERE Id='{Id(6)}'; DELETE FROM audit_logs WHERE actor='operator' LIMIT 1;");
        await s2.SyncOnceAsync();
        Assert.Equal("S1-A renamed on S2", await Scalar(DbA, $"SELECT Name FROM sirendevices WHERE Id='{Id(1)}'"));
        Assert.Equal(6, await Count(DbB, "sirendevices"));
        Assert.Equal(3, await Count(DbB, "audit_logs"));
        Assert.Equal(0, await s1.SyncOnceAsync());

        // Live Status is measured per server and must not be copied or cause sync traffic
        await Exec(Conn(DbA), $"UPDATE sirendevices SET Status='OFFLINE' WHERE Id='{Id(5)}'");
        await Exec(Conn(DbB), $"UPDATE sirendevices SET Status='ONLINE' WHERE Id='{Id(5)}'");
        Assert.Equal(0, await s1.SyncOnceAsync());
        Assert.Equal("ONLINE", await Scalar(DbB, $"SELECT Status FROM sirendevices WHERE Id='{Id(5)}'"));

        // Same siren edited on both servers before a sync: Server 1 wins, on whichever server the sync runs
        await Exec(Conn(DbA), $"UPDATE sirendevices SET Name='edit on S1' WHERE Id='{Id(2)}'");
        await Exec(Conn(DbB), $"UPDATE sirendevices SET Name='edit on S2' WHERE Id='{Id(2)}'");
        await s2.SyncOnceAsync();
        Assert.Equal("edit on S1", await Scalar(DbB, $"SELECT Name FROM sirendevices WHERE Id='{Id(2)}'"));

        // Heartbeats: the newer heartbeat wins a conflict, even from the non-preferred server
        await Exec(Conn(DbA), $"INSERT INTO stationstatuses VALUES ('{Id(80)}','PCB-Controller','2026-10-25 10:00:00',  'ONLINE')");
        await s1.SyncOnceAsync();
        await Exec(Conn(DbA), $"UPDATE stationstatuses SET LastHeartbeat='2026-10-25 10:00:05' WHERE Id='{Id(80)}'");
        await Exec(Conn(DbB), $"UPDATE stationstatuses SET LastHeartbeat='2026-10-25 10:00:09' WHERE Id='{Id(80)}'");
        await s1.SyncOnceAsync();
        Assert.Equal(new DateTime(2026, 10, 25, 10, 0, 9), Convert.ToDateTime(await Scalar(DbA, $"SELECT LastHeartbeat FROM stationstatuses WHERE Id='{Id(80)}'")));

        // New log written by app code after SyncId exists gets its own UUID automatically and syncs
        await Exec(Conn(DbB), "INSERT INTO audit_logs (actor, description) VALUES ('svc','late log')");
        await s1.SyncOnceAsync();
        Assert.Equal(1, await Count(DbA, "audit_logs", "actor='svc'"));

        Assert.Equal(0, await s1.SyncOnceAsync());
        Assert.Equal(0, await s2.SyncOnceAsync());
    }

    [Fact]
    public async Task PeerOffline_ThrowsAndKeepsLocalData()
    {
        if (!_available) return;
        await Exec(Conn(DbA), $"INSERT INTO sirendevices VALUES ('{Id(1)}','S1-A',1,'ONLINE',0,NULL)");
        var offlinePeer = new MySqlConnectionStringBuilder(Conn(DbB)) { Port = 1 }.ConnectionString;
        var s1 = new DatabaseSyncService(Conn(DbA), offlinePeer, localPreferred: true);
        await Assert.ThrowsAnyAsync<Exception>(() => s1.SyncOnceAsync());
        Assert.Equal(1, await Count(DbA, "sirendevices"));
    }
}
