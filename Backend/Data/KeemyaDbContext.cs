using Microsoft.EntityFrameworkCore;
using Keemya.Backend.Models;

namespace Keemya.Backend.Data
{
    public class KeemyaDbContext : DbContext
    {
        public KeemyaDbContext(DbContextOptions<KeemyaDbContext> options) : base(options)
        {
        }

        // Security
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Privilege> Privileges { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }

        // Siren Management
        public DbSet<SirenDevice> SirenDevices { get; set; }
        public DbSet<SirenGroup> SirenGroups { get; set; }
        public DbSet<SirenDetails> SirenDetails { get; set; }

        // Alerts
        public DbSet<AlertRule> AlertRules { get; set; }
        public DbSet<AlertType> AlertTypes { get; set; }
        public DbSet<NotificationTemplate> NotificationTemplates { get; set; }

        // Commands
        public DbSet<CommandConfig> CommandConfigs { get; set; }
        public DbSet<AudioFile> AudioFiles { get; set; }

        // Audit
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User - Role (Many-to-Many)
            modelBuilder.Entity<User>()
                .HasMany(u => u.Roles)
                .WithMany(r => r.Users)
                .UsingEntity(j => j.ToTable("UserRoles"));

            // Role - Privilege (Many-to-Many)
            modelBuilder.Entity<Role>()
                .HasMany(r => r.Privileges)
                .WithMany(p => p.Roles)
                .UsingEntity(j => j.ToTable("RolePrivileges"));

            // User - UserProfile (One-to-One)
            modelBuilder.Entity<User>()
                .HasOne(u => u.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<UserProfile>(p => p.UserId);

            // SirenDevice - SirenDetails (One-to-One)
            modelBuilder.Entity<SirenDevice>()
                .HasOne(d => d.Details)
                .WithOne(d => d.SirenDevice)
                .HasForeignKey<SirenDetails>(d => d.SirenDeviceId);

            // AlertRule - AlertType (One-to-One)
            modelBuilder.Entity<AlertRule>()
                .HasOne(ar => ar.AlertType)
                .WithOne(at => at.AlertRule)
                .HasForeignKey<AlertRule>(ar => ar.AlertTypeId);

            // Convert Enums to Strings
            modelBuilder.Entity<SirenDevice>()
                .Property(s => s.Status)
                .HasConversion<string>();

            modelBuilder.Entity<AlertRule>()
                .Property(a => a.Priority)
                .HasConversion<string>();

            // Seed Default Admin User
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Username = "admin",
                Password = "admin123",
                Enabled = true,
                IsFirstTimeLogin = false,
                Role = "Admin",
                Created = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });

            // ---------------------------------------------------------------
            // Seed Whelen Siren Protocol Commands (from RS-232 Command Spec)
            // These are the master list — users pick from these in Command Config.
            // CommandHex = the actual byte value sent to hardware (0x00–0x3F).
            // ---------------------------------------------------------------
            modelBuilder.Entity<CommandConfig>().HasData(

                // ── Group 0: Core Tones & Positioning ──────────────────────
                Cmd("00000000-0000-0000-0001-000000000001", "Clear",          "Clears any event in progress.",                                  "Clear",         0x00, "Blue",   0),
                Cmd("00000000-0000-0000-0001-000000000002", "Wail",           "Wail tone warning.",                                             "Wail",          0x01, "Red",    4),
                Cmd("00000000-0000-0000-0001-000000000003", "Attack",         "Attack tone warning.",                                           "Attack",        0x02, "Red",    4),
                Cmd("00000000-0000-0000-0001-000000000004", "Alert",          "Alert tone warning.",                                            "Alert",         0x03, "Orange", 4),
                Cmd("00000000-0000-0000-0001-000000000005", "Public Address", "Live public address — tone generator bypassed.",                 "PublicAddress", 0x04, "Purple", 0),
                Cmd("00000000-0000-0000-0001-000000000006", "Air Horn",       "Air horn tone warning.",                                         "AirHorn",       0x05, "Orange", 4),
                Cmd("00000000-0000-0000-0001-000000000007", "Hi-Lo",          "Hi-Lo tone warning.",                                            "HiLo",          0x06, "Yellow", 4),
                Cmd("00000000-0000-0000-0001-000000000008", "Whoop",          "Whoop tone warning.",                                            "Whoop",         0x07, "Yellow", 4),
                Cmd("00000000-0000-0000-0001-000000000009", "Noon Test",      "Short wail-2 tone (noon test).",                                 "NoonTest",      0x08, "Green",  4),
                Cmd("00000000-0000-0000-0001-000000000010", "Silent Test",    "Initiates diagnostic silent test, produces a status response.",  "SilentTest",    0x0F, "Cyan",   4),

                // ── Group 1: System Control ─────────────────────────────────
                Cmd("00000000-0000-0000-0002-000000000001", "Status Request", "Retrieves the full status byte from the siren.",                 "StatusRequest", 0x1F, "Blue",   4),
                Cmd("00000000-0000-0000-0002-000000000002", "Arm System",     "Arms the Instant Status response.",                              "ArmSystem",     0x18, "Green",  4),
                Cmd("00000000-0000-0000-0002-000000000003", "Dis-arm System", "Disables the Instant Status response.",                          "DisarmSystem",  0x19, "Red",    4),
                Cmd("00000000-0000-0000-0002-000000000004", "Siren On",       "Enables the tone generator and digital voice.",                  "SirenOn",       0x1A, "Green",  4),
                Cmd("00000000-0000-0000-0002-000000000005", "Siren Off",      "Disables the tone generator; digital voice stays active.",       "SirenOff",      0x1B, "Red",    4),
                Cmd("00000000-0000-0000-0002-000000000006", "Instant Status", "Get real-time instant status of the remote siren station.",      "InstantStatus", 0x23, "Cyan",   4),
                Cmd("00000000-0000-0000-0002-000000000007", "Counter",        "Tone activation software counter request.",                      "Counter",       0x16, "Blue",   2),
                Cmd("00000000-0000-0000-0002-000000000008", "Clear Counter",  "Clears the software tone activation counter to zero.",           "ClearCounter",  0x17, "Blue",   2),
                Cmd("00000000-0000-0000-0002-000000000009", "Test Clear",     "Clears LEDs.",                                                   "TestClear",     0x1E, "Blue",   0),
                Cmd("00000000-0000-0000-0002-000000000010", "Battery / AC",   "Requests battery DC voltage and AC voltage measurements.",       "BatteryAC",     0x21, "Green",  4),
                Cmd("00000000-0000-0000-0002-000000000011", "Battery / Temp", "Requests battery DC voltage and cabinet temperature.",           "BatteryTemp",   0x22, "Green",  4),
                Cmd("00000000-0000-0000-0002-000000000012", "Transmit Off",   "Disables the transmit repeat feature during Instant Status.",    "TransmitOff",   0x24, "Orange", 0),

                // ── Group 1: RSDVM Digital Voice (Msg 13–16) ───────────────
                Cmd("00000000-0000-0000-0003-000000000001", "Message 13",     "Initiates digital voice message 13 (RSDVM module).",             "Message13",     0x11, "Purple", 0),
                Cmd("00000000-0000-0000-0003-000000000002", "Message 14",     "Initiates digital voice message 14 (RSDVM module).",             "Message14",     0x12, "Purple", 0),
                Cmd("00000000-0000-0000-0003-000000000003", "Message 15",     "Initiates digital voice message 15 (RSDVM module).",             "Message15",     0x13, "Purple", 0),
                Cmd("00000000-0000-0000-0003-000000000004", "Message 16",     "Initiates digital voice message 16 (RSDVM module).",             "Message16",     0x14, "Purple", 0),

                // ── Group 3: RSDVM Digital Voice (Msg 1–12) ────────────────
                Cmd("00000000-0000-0000-0004-000000000001", "Message 1",      "Initiates digital voice message 1 (RSDVM module).",              "Message1",      0x31, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000002", "Message 2",      "Initiates digital voice message 2 (RSDVM module).",              "Message2",      0x32, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000003", "Message 3",      "Initiates digital voice message 3 (RSDVM module).",              "Message3",      0x33, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000004", "Message 4",      "Initiates digital voice message 4 (RSDVM module).",              "Message4",      0x34, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000005", "Message 5",      "Initiates digital voice message 5 (RSDVM module).",              "Message5",      0x35, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000006", "Message 6",      "Initiates digital voice message 6 (RSDVM module).",              "Message6",      0x36, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000007", "Message 7",      "Initiates digital voice message 7 (RSDVM module).",              "Message7",      0x37, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000008", "Message 8",      "Initiates digital voice message 8 (RSDVM module).",              "Message8",      0x38, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000009", "Message 9",      "Initiates digital voice message 9 (RSDVM module).",              "Message9",      0x3B, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000010", "Message 10",     "Initiates digital voice message 10 (RSDVM module).",             "Message10",     0x3C, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000011", "Message 11",     "Initiates digital voice message 11 (RSDVM module).",             "Message11",     0x3D, "Purple", 0),
                Cmd("00000000-0000-0000-0004-000000000012", "Message 12",     "Initiates digital voice message 12 (RSDVM module).",             "Message12",     0x3E, "Purple", 0),

                // ── Group 3: Strobe ─────────────────────────────────────────
                Cmd("00000000-0000-0000-0005-000000000001", "Strobe On",      "Activates the strobe light.",                                    "StrobeOn",      0x39, "Yellow", 0),
                Cmd("00000000-0000-0000-0005-000000000002", "Strobe Off",     "De-activates the strobe light.",                                 "StrobeOff",     0x3A, "Yellow", 0)
            );
        }

        /// <summary>Helper to build a seeded CommandConfig row cleanly.</summary>
        private static CommandConfig Cmd(
            string id, string name, string description,
            string commandType, int commandHex,
            string color, int expectedResponseBytes)
        {
            return new CommandConfig
            {
                Id                    = Guid.Parse(id),
                Name                  = name,
                Description           = description,
                CommandType           = commandType,
                CommandHex            = commandHex,
                Color                 = color,
                ExpectedResponseBytes = expectedResponseBytes,
                IsEnabled             = true,
                IsSystemDefault       = true,   // ← dropdown-only, hidden from user list
                Duration              = 0
            };
        }
    }
}
