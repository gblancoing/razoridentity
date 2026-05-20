namespace ComunaClick.Api.Modules.Crm;

public sealed class CustomerAvatarStorage
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public CustomerAvatarStorage(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public async Task<string?> SaveAsync(
        Guid tenantId,
        Guid customerId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length is <= 0 or > 4 * 1024 * 1024)
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

        var folder = Path.Combine(_environment.ContentRootPath, "uploads", "customer-avatars", tenantId.ToString("N"));
        Directory.CreateDirectory(folder);

        foreach (var existing in Directory.EnumerateFiles(folder, $"{customerId:N}.*"))
        {
            File.Delete(existing);
        }

        var physicalPath = Path.Combine(folder, $"{customerId:N}{extension}");
        await using (var output = File.Create(physicalPath))
        {
            await file.CopyToAsync(output, cancellationToken);
        }

        return BuildPublicUrl(tenantId, customerId, extension);
    }

    private string BuildPublicUrl(Guid tenantId, Guid customerId, string extension)
    {
        var relative = $"/uploads/customer-avatars/{tenantId:N}/{customerId:N}{extension}";
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
