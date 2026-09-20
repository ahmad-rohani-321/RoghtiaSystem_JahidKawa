namespace RoghtiaSystem_JahidKawa.Server.Services
{
    public static class DoctorImages
    {
        public const int MaxFileBytes = 2 * 1024 * 1024;
        public const int MaxRequestBytes = 7 * 1024 * 1024;

        public static string? GetContentType(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                return "image/png";
            if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255)
                return "image/jpeg";
            if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
                return "image/webp";
            return null;
        }

        public static async Task<byte[]?> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file == null) return null;
            if (file.Length > MaxFileBytes)
                throw new ImageValidationException("IMAGE_TOO_LARGE", "د هر انځور اندازه باید تر ۲ مېګابایټه زیاته نه وي.");
            if (file.Length == 0)
                throw new ImageValidationException("INVALID_IMAGE", "مهرباني وکړئ یو سم PNG، JPEG یا WebP انځور وټاکئ.");

            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            var contentType = GetContentType(bytes);
            if (contentType == null || !string.Equals(contentType, file.ContentType, StringComparison.OrdinalIgnoreCase))
                throw new ImageValidationException("INVALID_IMAGE", "مهرباني وکړئ یو سم PNG، JPEG یا WebP انځور وټاکئ.");
            return bytes;
        }
    }

    public sealed class ImageValidationException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
