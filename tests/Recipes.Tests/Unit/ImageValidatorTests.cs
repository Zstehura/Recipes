using Recipes.Tests.TestData;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Unit;

public class ImageValidatorTests
{
    public static TheoryData<byte[], string> AllowedImages => new()
    {
        { TestImages.Jpeg, "image/jpeg" },
        { TestImages.Png, "image/png" },
        { TestImages.Gif, "image/gif" },
        { "GIF87a\x01\x00"u8.ToArray(), "image/gif" },
        { TestImages.WebP, "image/webp" },
    };

    [Theory]
    [MemberData(nameof(AllowedImages))]
    public void Validate_AllowedFormat_IsValidWithDetectedContentType(byte[] data, string expectedContentType)
    {
        var result = ImageValidator.Validate(data);

        result.IsValid.ShouldBeTrue();
        result.ContentType.ShouldBe(expectedContentType);
        result.ErrorMessage.ShouldBeNull();
    }

    public static TheoryData<byte[]> DisallowedContent => new()
    {
        TestImages.Svg,
        TestImages.Html,
        "%PDF-1.7"u8.ToArray(),
        "MZ\x90\x00"u8.ToArray(),                     // Windows executable
        "RIFF\x24\x00\x00\x00WAVEfmt "u8.ToArray(),     // RIFF container that isn't WebP
        new byte[] { 0xFF, 0xD8 },                      // truncated JPEG signature
        new byte[] { 0x89, 0x50, 0x4E, 0x47 },          // truncated PNG signature
    };

    [Theory]
    [MemberData(nameof(DisallowedContent))]
    public void Validate_DisallowedContent_IsInvalid(byte[] data)
    {
        var result = ImageValidator.Validate(data);

        result.IsValid.ShouldBeFalse();
        result.ContentType.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("Unsupported image format");
    }

    [Fact]
    public void Validate_SvgDisguisedWithImageExtensionAndType_IsStillRejected()
    {
        // The browser-reported type and file name never reach the validator; only bytes do.
        ImageValidator.DetectContentType(TestImages.Svg).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public void Validate_NullOrEmpty_IsInvalid(byte[]? data)
    {
        var result = ImageValidator.Validate(data);

        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("The image file is empty.");
    }

    [Fact]
    public void Validate_LargerThanLimit_IsInvalid()
    {
        var data = new byte[ImageValidator.MaxImageSizeBytes + 1];
        TestImages.Png.CopyTo(data, 0);

        var result = ImageValidator.Validate(data);

        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("10MB");
    }

    [Fact]
    public void Validate_ExactlyAtLimit_IsValid()
    {
        var data = new byte[ImageValidator.MaxImageSizeBytes];
        TestImages.Png.CopyTo(data, 0);

        ImageValidator.Validate(data).IsValid.ShouldBeTrue();
    }
}
