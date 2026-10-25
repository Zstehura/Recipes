using System.Buffers.Binary;
using System.Text.Json;
using Shouldly;

namespace Recipes.Tests.Unit;

/// <summary>
/// Checks the home-screen manifest against the files it points to, so a renamed or resized icon
/// can't silently break "Add to Home Screen".
/// </summary>
public class WebManifestTests
{
    private static readonly string WebRoot = Path.Combine(FindRepoRoot(), "wwwroot");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Recipes.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Recipes.sln not found above the test output");
    }

    private static JsonElement ReadManifest() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(WebRoot, "manifest.webmanifest"))).RootElement;

    // A PNG's width and height are big-endian ints at bytes 16-23, right after the signature and IHDR header
    private static (int Width, int Height) PngSize(string path)
    {
        var header = File.ReadAllBytes(path).AsSpan(0, 24);
        header[..8].ToArray().ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, $"{path} isn't a PNG");
        return (BinaryPrimitives.ReadInt32BigEndian(header[16..20]), BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
    }

    [Fact]
    public void Manifest_HasWhatBrowsersNeedToOfferInstall()
    {
        var manifest = ReadManifest();

        manifest.GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace();
        manifest.GetProperty("short_name").GetString()!.Length.ShouldBeLessThanOrEqualTo(12);   // fits under a home-screen icon
        manifest.GetProperty("start_url").GetString().ShouldNotBeNullOrWhiteSpace();
        manifest.GetProperty("display").GetString().ShouldBe("standalone");
    }

    [Fact]
    public void EveryIcon_ExistsAtTheSizeItClaims()
    {
        foreach (var icon in ReadManifest().GetProperty("icons").EnumerateArray())
        {
            var src = icon.GetProperty("src").GetString()!;
            var path = Path.Combine(WebRoot, src);
            File.Exists(path).ShouldBeTrue($"{src} is missing");

            var (width, height) = PngSize(path);
            icon.GetProperty("sizes").GetString().ShouldBe($"{width}x{height}", $"{src} is {width}x{height}");
        }
    }

    [Fact]
    public void Icons_Include192And512_AndAMaskableOne()
    {
        var icons = ReadManifest().GetProperty("icons").EnumerateArray().ToList();
        var sizes = icons.Select(i => i.GetProperty("sizes").GetString()).ToList();

        sizes.ShouldContain("192x192");
        sizes.ShouldContain("512x512");
        icons.ShouldContain(i => i.GetProperty("purpose").GetString() == "maskable");
    }

    [Fact]
    public void ThemeColor_MatchesTheLayout()
    {
        var layout = File.ReadAllText(Path.Combine(WebRoot, "..", "Pages", "_Layout.cshtml"));
        var color = ReadManifest().GetProperty("theme_color").GetString();

        layout.ShouldContain($"<meta name=\"theme-color\" content=\"{color}\" />");
        layout.ShouldContain("<link rel=\"manifest\" href=\"manifest.webmanifest\" />");
    }
}
