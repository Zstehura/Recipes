namespace Recipes.Tests.TestData;

/// <summary>
/// Minimal byte sequences that start with each format's signature ("magic number").
/// Validation only looks at the signature, so the rest doesn't need to be a full image.
/// </summary>
public static class TestImages
{
    public static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
    public static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    public static readonly byte[] Gif = "GIF89a\x01\x00\x01\x00"u8.ToArray();
    public static readonly byte[] WebP = "RIFF\x24\x00\x00\x00WEBPVP8 "u8.ToArray();

    public static readonly byte[] Svg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(document.cookie)</script></svg>"u8.ToArray();

    public static readonly byte[] Html = "<!DOCTYPE html><script>alert(1)</script>"u8.ToArray();
}
