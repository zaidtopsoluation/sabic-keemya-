using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static void Main()
    {
        var frontendPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Frontend");
        if (!Directory.Exists(frontendPath))
        {
            frontendPath = Path.Combine(Directory.GetCurrentDirectory(), "Frontend");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project Keemya.Frontend.csproj",
                WorkingDirectory = frontendPath,
                UseShellExecute = false,
                CreateNoWindow = false
            }
        };
        process.Start();
        process.WaitForExit();
    }
}
