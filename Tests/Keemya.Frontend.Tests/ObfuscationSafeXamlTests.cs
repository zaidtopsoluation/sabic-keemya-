using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Keemya.Frontend.Tests;

// The installer obfuscates TS_EMS.dll (obfuscar.xml keeps only Views, ViewModels, Models, App and MainWindow).
// XAML that names a type from any other namespace works in Debug but crashes the installed build
// (Command Center crashed on RCB through {x:Static services:SirenCommunicationService.Instance}).
public class ObfuscationSafeXamlTests
{
    private static readonly string[] SafeNamespaces =
        { "Keemya.Frontend.Views", "Keemya.Frontend.ViewModels", "Keemya.Frontend.Models" };

    private static string FrontendDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "Frontend"));

    [Fact]
    public void Xaml_OnlyReferencesNamespacesKeptByObfuscar()
    {
        var offenders = Directory.GetFiles(FrontendDir(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "clr-namespace:(Keemya\\.Frontend[\\w.]*)")
                .Select(m => m.Groups[1].Value)
                // the root namespace is only used for x:Class / local: of App and windows, which code-behind wires up
                .Where(ns => ns != "Keemya.Frontend" && !SafeNamespaces.Contains(ns))
                .Select(ns => $"{Path.GetFileName(f)} -> {ns}"))
            .ToList();

        Assert.Empty(offenders);
    }
}
