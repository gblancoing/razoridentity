namespace ComunaClick.Api.Modules.Catalog;

public sealed class ProductImageStorage
{
    private const int MaxFileBytes = 4 * 1024 * 1024;
    private const int MaxImagesPerProduct = 8;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public ProductImageStorage(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public int MaxImagesPerProductLimit => MaxImagesPerProduct;

    public async Task<string?> SaveAsync(
        Guid tenantId,
        Guid productId,
        Guid imageId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length is <= 0 or > MaxFileBytes)
        {
            return null;
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return null;
        }

        var extension = ResolveExtension(file.ContentType);
        if (extension is null)
        {
            return null;
        }

        var folder = Path.Combine(
            _environment.ContentRootPath,
            "uploads",
            "product-images",
            tenantId.ToString("N"),
            productId.ToString("N"));
        Directory.CreateDirectory(folder);

        var physicalPath = Path.Combine(folder, $"{imageId:N}{extension}");
        await using (var output = File.Create(physicalPath))
        {
            await file.CopyToAsync(output, cancellationToken);
        }

        return BuildPublicUrl(tenantId, productId, imageId, extension);
    }

    public void DeletePhysical(Guid tenantId, Guid productId, Guid imageId, string url)
    {
        var extension = Path.GetExtension(url);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return;
        }

        var physicalPath = Path.Combine(
            _environment.ContentRootPath,
            "uploads",
            "product-images",
            tenantId.ToString("N"),
            productId.ToString("N"),
            $"{imageId:N}{extension}");
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }

    private string BuildPublicUrl(Guid tenantId, Guid productId, Guid imageId, string extension)
    {
        var relative = $"/uploads/product-images/{tenantId:N}/{productId:N}/{imageId:N}{extension}";
        var configured = _configuration["PublicApi:BaseUrl"]?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return $"{configured}{relative}";
        }

        return relative;
    }

    private static string? ResolveExtension(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => null
    };
}
