using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Dnui.PublishTests;

/// <summary>Spec 003 US2 / FR-007, FR-014, FR-015: every package carries complete metadata.</summary>
[Collection(nameof(PublishCollection))]
public class PackageMetadataTests
{
    private static string Metadata(string key) => typeof(PackageMetadataTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value!;

    private static string RepoRoot => Metadata("DnuiRepoRoot");

    private const string Repo = "https://github.com/cmdless/dnui";

    [Fact]
    public async Task LibraryAndTool_HaveMetadata_Symbols_AndNoPlatformBinaries()
    {
        var output = Path.Combine(Path.GetTempPath(), "dnui-pack-" + Guid.NewGuid().ToString("N"));
        foreach (var project in new[] { "Dnui", "Dnui.Tool" })
        {
            var (code, log) = await PublishModeTests.RunAsync("dotnet", ["pack", Path.Combine(RepoRoot, "src", project), "-c", "Release", "-o", output], null, TimeSpan.FromMinutes(5));
            Assert.True(code == 0, log);
        }

        foreach (var id in new[] { "Cmdless.Dnui", "Cmdless.Dnui.Tool" })
        {
            var nupkg = Path.Combine(output, $"{id}.{DnuiRuntime.Version}.nupkg");
            AssertCommonMetadata(nupkg, id);
            Assert.True(File.Exists(Path.Combine(output, $"{id}.{DnuiRuntime.Version}.snupkg")), $"{id} has no symbol package");
            var nuspec = ReadNuspec(nupkg);
            Assert.Equal(Repo, nuspec.Repository?.Attribute("url")?.Value);
            Assert.False(string.IsNullOrEmpty(nuspec.Repository?.Attribute("commit")?.Value), $"{id}: SourceLink commit missing");
        }

        using var tool = ZipFile.OpenRead(Path.Combine(output, $"Cmdless.Dnui.Tool.{DnuiRuntime.Version}.nupkg"));
        var entries = tool.Entries.Select(e => e.FullName).ToList();
        Assert.Contains(entries, e => e.EndsWith("/Dnui.Tool.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.Contains("/runtimes/", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.Contains("Avalonia.Desktop", StringComparison.Ordinal) || e.Contains("Avalonia.Native", StringComparison.Ordinal)
            || e.Contains("Avalonia.Win32", StringComparison.Ordinal) || e.Contains("Avalonia.X11", StringComparison.Ordinal) || e.Contains("SkiaSharp", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimePackage_HasMetadata_AndSaysNotToReferenceIt()
    {
        var id = $"Cmdless.Dnui.Runtime.{RuntimeInformation.RuntimeIdentifier}";
        var nupkg = Path.Combine(RepoRoot, "artifacts", "packages", $"{id}.{DnuiRuntime.Version}.nupkg");
        Assert.SkipUnless(File.Exists(nupkg), $"Run PackRuntime for {RuntimeInformation.RuntimeIdentifier} first.");

        AssertCommonMetadata(nupkg, id);
        Assert.Contains("not meant to be referenced directly", ReadNuspec(nupkg).Description);
    }

    private static void AssertCommonMetadata(string nupkg, string id)
    {
        var nuspec = ReadNuspec(nupkg);
        Assert.Equal(id, nuspec.Id);
        Assert.Equal("Cmdless", nuspec.Authors);
        Assert.Equal("MIT", nuspec.License);
        Assert.Equal(Repo, nuspec.ProjectUrl);
        Assert.False(string.IsNullOrWhiteSpace(nuspec.Description));
        Assert.Equal("icon.png", nuspec.Icon);
        Assert.Equal("README.md", nuspec.Readme);

        using var zip = ZipFile.OpenRead(nupkg);
        Assert.NotNull(zip.GetEntry("icon.png"));
        Assert.NotNull(zip.GetEntry("README.md"));
    }

    private sealed record Nuspec(string? Id, string? Authors, string? License, string? ProjectUrl, string? Description, string? Icon, string? Readme, XElement? Repository);

    private static Nuspec ReadNuspec(string nupkg)
    {
        using var zip = ZipFile.OpenRead(nupkg);
        var entry = zip.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal) && !e.FullName.Contains('/'));
        using var stream = entry.Open();
        var metadata = XDocument.Load(stream).Root!.Elements().Single(e => e.Name.LocalName == "metadata");
        string? Get(string name) => metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
        return new(Get("id"), Get("authors"), Get("license"), Get("projectUrl"), Get("description"), Get("icon"), Get("readme"),
            metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "repository"));
    }
}
