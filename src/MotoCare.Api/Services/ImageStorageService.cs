using System.Text.RegularExpressions;

namespace MotoCare.Api.Services;

public sealed partial class ImageStorageService(IWebHostEnvironment environment)
{
    private const long MaxFileSize = 4 * 1024 * 1024;
    private readonly string uploadRoot = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads");

    public async Task<string> SaveAsync(
        Stream stream,
        string fileName,
        string? contentType,
        string category,
        long length,
        CancellationToken cancellationToken = default)
    {
        if (length <= 0 || length > MaxFileSize)
            throw new InvalidOperationException("Ảnh tải lên phải có dung lượng từ 1 byte đến 4 MB.");
        var extension = Extension(contentType, Path.GetExtension(fileName));
        var safeCategory = CategoryRegex().IsMatch(category) ? category.ToLowerInvariant() : "general";
        var relativeDirectory = Path.Combine(safeCategory, DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var physicalDirectory = Path.Combine(uploadRoot, relativeDirectory);
        Directory.CreateDirectory(physicalDirectory);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(physicalDirectory, storedName);
        await using var output = new FileStream(
            physicalPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);
        await stream.CopyToAsync(output, cancellationToken);
        return "/uploads/" + Path.Combine(relativeDirectory, storedName).Replace('\\', '/');
    }

    public async Task<string> SaveDataUrlAsync(
        string dataUrl,
        string category,
        CancellationToken cancellationToken = default)
    {
        var match = DataUrlRegex().Match(dataUrl);
        if (!match.Success) throw new InvalidOperationException("Dữ liệu ảnh cũ không hợp lệ.");
        var bytes = Convert.FromBase64String(match.Groups[2].Value);
        await using var stream = new MemoryStream(bytes, writable: false);
        return await SaveAsync(
            stream,
            $"legacy.{match.Groups[1].Value}",
            $"image/{match.Groups[1].Value}",
            category,
            bytes.LongLength,
            cancellationToken);
    }

    public bool Delete(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath) || !storedPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return false;
        var relative = storedPath["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(uploadRoot, relative));
        var root = Path.GetFullPath(uploadRoot) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) return false;
        File.Delete(fullPath);
        return true;
    }

    private static string Extension(string? contentType, string suppliedExtension) =>
        contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => suppliedExtension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ".jpg",
                ".png" => ".png",
                ".webp" => ".webp",
                ".gif" => ".gif",
                _ => throw new InvalidOperationException("Chỉ hỗ trợ ảnh JPG, PNG, WEBP hoặc GIF.")
            }
        };

    [GeneratedRegex("^[a-z0-9-]{1,40}$", RegexOptions.IgnoreCase)]
    private static partial Regex CategoryRegex();

    [GeneratedRegex("^data:image/(jpeg|jpg|png|webp|gif);base64,(.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DataUrlRegex();
}
