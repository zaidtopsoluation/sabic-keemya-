using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace Keemya.Frontend.Services
{
    public class LicenseService
    {
        // Embedded Public Key (Corresponding to the Private Key in the generator)
        private const string PublicKeyXml = @"<RSAKeyValue><Modulus>wlTe/B715hhai75GThJTOnEF2H58J66LRPR0VyB6k4IO3bATvkqnB92kK+sPvMgGviU9BXypStOh2VW991isOUtiMSLXNXzLJTo//qIfDb6fbzvmQOXsN4vvgEAp3egOmCVMyCCffGCe8lkpV6PDMbASYXhjQtOSfK7UWygEHNBeBibDGmLjfWh6O57MzfTrkUZv0lSU/VGpPyUMxGW/jOvz1GHmLd6Lje5pCq4N4FKSvrY8JFJxuebCvx3rpE9q15863377RtgUg1wcMFXswVhJr3okVdHpknjLkbISW+Cy5EqRcBJABJzTneWs8uTC5SGhJNZlwnR2BJ5j3BqbXQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        // Path where license is saved locally: %localappdata%/KeemyaSystem/license.lic
        private static readonly string LicenseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "KeemyaSystem"
        );

        private static readonly string LicenseFilePath = Path.Combine(LicenseDirectory, "license.lic");

        private static LicenseData? _cachedLicense;

        /// <summary>
        /// Gets a unique hardware fingerprint for the machine (locked to the hardware).
        /// </summary>
        public static string GetMachineId()
        {
            try
            {
                // 1. Get Windows MachineGuid (unique per OS installation)
                string machineGuid = "";
                using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    using (var subKey = key.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    {
                        machineGuid = subKey?.GetValue("MachineGuid")?.ToString() ?? "";
                    }
                }

                // 2. Add static hardware parameters (Processor Count + Machine Name)
                string processorInfo = Environment.ProcessorCount.ToString() + Environment.MachineName;

                // 3. Combine and SHA-256 hash to create a secure, fixed-length hardware key
                string rawId = $"{machineGuid}-{processorInfo}";
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(rawId));
                    var sb = new StringBuilder();
                    foreach (byte b in hash)
                    {
                        sb.Append(b.ToString("X2"));
                    }
                    
                    // Format as XXXX-XXXX-XXXX-XXXX-XXXX for readable copy-pasting
                    string hex = sb.ToString();
                    return $"{hex.Substring(0, 4)}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}";
                }
            }
            catch
            {
                // Fallback in case of permissions or environment issues
                return "Keemya-FALLBACK-9999";
            }
        }

        /// <summary>
        /// Reads and validates the local license file.
        /// </summary>
        public static bool VerifyLicense(out LicenseData? licenseInfo)
        {
            return VerifyLicense(out licenseInfo, out _);
        }

        /// <summary>
        /// Reads and validates the local license file with a detailed failure reason.
        /// </summary>
        public static bool VerifyLicense(out LicenseData? licenseInfo, out string failureReason)
        {
            licenseInfo = null;
            failureReason = "";

            if (_cachedLicense != null)
            {
                licenseInfo = _cachedLicense;
                return true;
            }

            try
            {
                if (!File.Exists(LicenseFilePath))
                {
                    failureReason = "License file 'license.lic' not found.";
                    return false;
                }

                // 1. Read license contents
                string licenseContent = File.ReadAllText(LicenseFilePath);
                var signedLicense = JsonSerializer.Deserialize<SignedLicense>(licenseContent);
                if (signedLicense == null || string.IsNullOrEmpty(signedLicense.DataJson) || string.IsNullOrEmpty(signedLicense.Signature))
                {
                    failureReason = "License file format is corrupted or invalid.";
                    return false;
                }

                // 2. Verify signature with Public Key
                using (var rsa = RSA.Create())
                {
                    try
                    {
                        rsa.FromXmlString(PublicKeyXml);
                    }
                    catch (Exception ex)
                    {
                        failureReason = $"Error loading embedded public key: {ex.Message}";
                        return false;
                    }

                    byte[] dataBytes = Encoding.UTF8.GetBytes(signedLicense.DataJson);
                    byte[] signatureBytes;
                    try
                    {
                        signatureBytes = Convert.FromBase64String(signedLicense.Signature);
                    }
                    catch
                    {
                        failureReason = "Signature string in file is not valid Base64.";
                        return false;
                    }

                    bool isSignatureValid = rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    if (!isSignatureValid)
                    {
                        failureReason = "Signature verification failed. The license may have been modified, or signed using a different Private Key.";
                        return false; // Signature does not match data
                    }
                }

                // 3. Extract license fields
                var data = JsonSerializer.Deserialize<LicenseData>(signedLicense.DataJson);
                if (data == null)
                {
                    failureReason = "Unable to read license parameters from JSON.";
                    return false;
                }

                // 4. Validate Expiration
                if (DateTime.TryParseExact(data.ExpiryDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime expiryDate))
                {
                    if (expiryDate < DateTime.Today)
                    {
                        failureReason = $"License expired on {data.ExpiryDate}.";
                        return false; // License expired
                    }
                }
                else
                {
                    failureReason = $"License expiration date format '{data.ExpiryDate}' is invalid. Must be YYYY-MM-DD.";
                    return false; // Invalid date format
                }

                // 5. Validate Machine ID (Hardware Lock)
                // If the license contains a Machine ID, ensure it matches the current computer
                if (!string.IsNullOrEmpty(data.MachineId))
                {
                    string currentMachineId = GetMachineId();
                    if (!string.Equals(data.MachineId.Trim(), currentMachineId.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        failureReason = $"Machine ID mismatch. License locked to '{data.MachineId}', but current machine is '{currentMachineId}'.";
                        return false; // Copied to another computer!
                    }
                }

                // Everything is valid! Cache the license
                _cachedLicense = data;
                licenseInfo = data;
                return true;
            }
            catch (Exception ex)
            {
                failureReason = $"Unexpected error checking license: {ex.Message}";
                return false; // Any parsing or security exception triggers validation failure
            }
        }

        /// <summary>
        /// Installs a new license by copying it to the local appdata path and verifying it.
        /// </summary>
        public static bool InstallLicense(string licenseFilePath, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                if (!File.Exists(licenseFilePath))
                {
                    errorMessage = "The selected file does not exist.";
                    return false;
                }

                // Create directory if missing
                if (!Directory.Exists(LicenseDirectory))
                {
                    Directory.CreateDirectory(LicenseDirectory);
                }

                // Copy to local appdata (overwrite if exists)
                File.Copy(licenseFilePath, LicenseFilePath, true);

                // Clear cache so it forces a re-read
                _cachedLicense = null;

                // Validate the newly copied license
                if (VerifyLicense(out var info, out errorMessage))
                {
                    return true;
                }
                else
                {
                    // If invalid, delete the copied file so it doesn't leave garbage
                    if (File.Exists(LicenseFilePath))
                    {
                        File.Delete(LicenseFilePath);
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Error installing license: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Retrieves the maximum configurable sirens allowed by the license.
        /// Returns 0 if no valid license exists.
        /// </summary>
        public static int GetMaxSirensAllowed()
        {
            if (VerifyLicense(out var info) && info != null)
            {
                return info.MaxSirensAllowed;
            }
            return 0;
        }
    }

    // --- Data Transfer Objects ---
    public class LicenseData
    {
        public string CustomerName { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public int MaxSirensAllowed { get; set; }
        public string MachineId { get; set; } = string.Empty;
    }

    public class SignedLicense
    {
        public string DataJson { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}
