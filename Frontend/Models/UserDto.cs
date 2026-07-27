using System;

namespace Keemya.Frontend.Models
{
    public class UserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string LastLogin { get; set; } = "-";
        public string Created { get; set; } = "-";
        public string TemporaryPassword { get; set; } = "-";
    }
}
