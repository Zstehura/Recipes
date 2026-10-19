namespace RecipesApp.Services;

/// <summary>
/// Validates uploaded images by their content, not by the file name or the content type the
/// browser reports, both of which the client controls.
/// </summary>
/// <remarks>
/// Only formats on the allow-list are accepted. SVG is deliberately excluded: it is XML that
/// can contain scripts, and since images are served from the app's own origin, an uploaded
/// SVG opened directly would run in the app's context (stored XSS).
/// </remarks>
public static class ImageValidator
{
    public const int MaxImageSizeBytes = 10 * 1024 * 1024;

    public const string SupportedFormatsDescription = "JPEG, PNG, GIF, or WebP";

    /// <summary>
    /// Identifies the image format from its leading bytes ("magic numbers").
    /// Returns the matching content type, or null if the data isn't an allowed image format.
    /// </summary>
    public static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
            return "image/jpeg";

        if (data.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";

        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
            return "image/gif";

        // WebP: "RIFF", 4-byte file size, then "WEBP"
        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";

        return null;
    }

    public static ImageValidationResult Validate(byte[]? data)
    {
        if (data == null || data.Length == 0)
            return ImageValidationResult.Invalid("The image file is empty.");

        if (data.Length > MaxImageSizeBytes)
            return ImageValidationResult.Invalid($"The image is larger than the {MaxImageSizeBytes / (1024 * 1024)}MB limit.");

        var contentType = DetectContentType(data);
        return contentType == null
            ? ImageValidationResult.Invalid($"Unsupported image format. Please upload a {SupportedFormatsDescription} image.")
            : ImageValidationResult.Valid(contentType);
    }
}

public sealed record ImageValidationResult(bool IsValid, string? ContentType, string? ErrorMessage)
{
    public static ImageValidationResult Valid(string contentType) => new(true, contentType, null);

    public static ImageValidationResult Invalid(string errorMessage) => new(false, null, errorMessage);
}
