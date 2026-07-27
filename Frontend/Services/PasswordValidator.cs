using System;
using System.Linq;

namespace Keemya.Frontend.Services
{
    public static class PasswordValidator
    {
        public static bool IsValid(string password, out string errorMessage)
        {
            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "Password cannot be empty.";
                return false;
            }

            if (password.Length < 8)
            {
                errorMessage = "Password must be at least 8 characters long.";
                return false;
            }

            if (!password.Any(char.IsLetter))
            {
                errorMessage = "Password must contain at least one letter.";
                return false;
            }

            if (!password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
            {
                errorMessage = "Password must contain at least one special character (e.g. @, #, $, %, etc.).";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static string GenerateTempPassword()
        {
            var random = new Random();
            // e.g. "Temp@2938" (Length = 9, has letters, special character '@', and digits)
            return $"Temp@{random.Next(1000, 9999)}";
        }
    }
}
