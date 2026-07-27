using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Keemya.Backend.Models
{
    // --- Security Models ---

    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; } // Should be hashed in practice
        
        public bool Enabled { get; set; }
        
        public bool IsFirstTimeLogin { get; set; } = true;
        
        public string Role { get; set; } = "Service";
        
        public DateTime Created { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastLogin { get; set; }

        public UserProfile Profile { get; set; }
        
        public ICollection<Role> Roles { get; set; } = new List<Role>();
    }

    public class UserProfile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid UserId { get; set; }
        
        [ForeignKey("UserId")]
        public User User { get; set; }
        
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
    }

    public class Role
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Name { get; set; } // ADMIN, USER, OPERATOR
        
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Privilege> Privileges { get; set; } = new List<Privilege>();
    }

    public class Privilege
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Name { get; set; }
        
        public ICollection<Role> Roles { get; set; } = new List<Role>();
    }

    // --- Siren Models ---

    public class SirenGroup
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Name { get; set; }
        
        public string Description { get; set; }
        
        public string Color { get; set; } = "Red";
        
        public string Shape { get; set; } = "Rectangle";
        
        public ICollection<SirenDevice> Devices { get; set; } = new List<SirenDevice>();
    }

    public class SirenDevice
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Name { get; set; }
        
        public string Description { get; set; }
        public string Address { get; set; }
        public string AreaCode { get; set; } = string.Empty;
        public string AddressCode { get; set; } = string.Empty;
        public double Lat { get; set; } = 0.0;
        public double Lng { get; set; } = 0.0;
        
        public SirenStatus Status { get; set; } = SirenStatus.OFFLINE;
        public string Ip { get; set; } = string.Empty;
        public bool Redundant { get; set; } = false;
        
        public Guid? GroupId { get; set; }
        
        [ForeignKey("GroupId")]
        public SirenGroup Group { get; set; }
        
        public SirenDetails Details { get; set; }
    }

    public enum SirenStatus
    {
        ONLINE,
        OFFLINE,
        ERROR
    }

    public class SirenDetails
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid SirenDeviceId { get; set; }
        
        [ForeignKey("SirenDeviceId")]
        public SirenDevice SirenDevice { get; set; }
        
        public string FirmwareVersion { get; set; }
        public string HardwareModel { get; set; }
        public DateTime LastHealthCheck { get; set; }
    }

    // --- Alert Models ---

    public class AlertRule
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid AlertTypeId { get; set; }
        
        [ForeignKey("AlertTypeId")]
        public AlertType AlertType { get; set; }
        
        public AlertPriority Priority { get; set; }
        
        public Guid? TemplateId { get; set; }
        
        [ForeignKey("TemplateId")]
        public NotificationTemplate Template { get; set; }
        
        public bool Active { get; set; }
    }

    public enum AlertPriority
    {
        LOW,
        MEDIUM,
        HIGH,
        CRITICAL
    }

    public class AlertType
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Name { get; set; }
        
        public AlertRule AlertRule { get; set; }
    }

    public class NotificationTemplate
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Name { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        
        public ICollection<AlertRule> AlertRules { get; set; } = new List<AlertRule>();
    }

    // --- Command Models ---

    public class CommandConfig
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Name { get; set; }
        
        public string Description { get; set; }
        
        /// <summary>
        /// The Whelen protocol command byte name (e.g. "Wail", "Attack", "SilentTest").
        /// Maps directly to the CommandType dropdown in the UI.
        /// </summary>
        public string CommandType { get; set; }
        
        /// <summary>
        /// The actual hex byte value sent to the siren hardware (e.g. 0x01 for Wail).
        /// Stored as an integer. Range: 0–63 (0x00–0x3F).
        /// </summary>
        public int CommandHex { get; set; } = 0;
        
        /// <summary>
        /// Number of status bytes expected in the hardware response (0, 2, 4, 6, or 8).
        /// </summary>
        public int ExpectedResponseBytes { get; set; } = 0;
        
        /// <summary>UI accent color for this command card (e.g. "Blue", "Red").</summary>
        public string Color { get; set; } = "Blue";
        
        /// <summary>Whether this command is enabled for use in the Command Center.</summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// True for the 40 seeded Whelen protocol commands — these appear in the
        /// dropdown only and are NOT shown in the Command Configuration list.
        /// False (default) for user-created command configurations.
        /// </summary>
        public bool IsSystemDefault { get; set; } = false;
        
        public Guid? AudioId { get; set; }
        
        [ForeignKey("AudioId")]
        public AudioFile AudioFile { get; set; }
        
        /// <summary>Duration in seconds. 0 means manual stop.</summary>
        public int Duration { get; set; } = 0;
    }

    public class AudioFile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        
        public ICollection<CommandConfig> CommandConfigs { get; set; } = new List<CommandConfig>();
    }

    // --- Audit Models ---

    public class AuditLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public string Actor { get; set; }
        public string Action { get; set; }
        public string Module { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        [Column(TypeName = "json")]
        public string EntityData { get; set; } // Storing JSON as string
    }
}
