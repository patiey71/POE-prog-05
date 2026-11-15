namespace RaceDay.Api.Services;

/// <summary>Checks an uploaded image: size, extension and the real file signature (not just the name).</summary>
public static class ImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public record Result(bool Ok, string? Error, string? Extension, string? ContentType)
    {
        public static Result Fail(string error) => new(false, error, null, null);
    }

    public static async Task<Result> ValidateAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return Result.Fail("Please choose an image file to upload.");
        if (file.Length > MaxBytes) return Result.Fail("The image is too large. The maximum size is 5 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".gif" or ".webp"))
            return Result.Fail("Only JPG, PNG, GIF or WEBP images are allowed.");

        var head = new byte[12];
        await using (var s = file.OpenReadStream())
        {
            var read = 0;
            while (read < head.Length)
            {
                var n = await s.ReadAsync(head.AsMemory(read, head.Length - read));
                if (n == 0) break;
                read += n;
            }
        }

        var isJpeg = head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;
        var isPng = head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47;
        var isGif = head[0] == 0x47 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x38;
        var isWebp = head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46
                     && head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50;

        string? type = isJpeg ? "image/jpeg" : isPng ? "image/png" : isGif ? "image/gif" : isWebp ? "image/webp" : null;
        if (type is null) return Result.Fail("The file is not a valid image.");

        return new Result(true, null, ext, type);
    }
}
