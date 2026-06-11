namespace ComunaClick.Api.Modules.Catalog;

public sealed class ServiceImageStorage
{
  private const int MaxFileBytes = 4 * 1024 * 1024;
  private const int MaxImagesPerService = 8;

  private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
  {
    "image/jpeg",
    "image/png",
    "image/webp",
    "image/gif"
  };

  private readonly IWebHostEnvironment _environment;
  private readonly IConfiguration _configuration;

  public ServiceImageStorage(IWebHostEnvironment environment, IConfiguration configuration)
  {
    _environment = environment;
    _configuration = configuration;
  }

  public int MaxImagesPerServiceLimit => MaxImagesPerService;

  public async Task<string?> SaveAsync(
    Guid tenantId,
    Guid serviceId,
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
      "service-images",
      tenantId.ToString("N"),
      serviceId.ToString("N"));
    Directory.CreateDirectory(folder);

    var physicalPath = Path.Combine(folder, $"{imageId:N}{extension}");
    await using (var output = File.Create(physicalPath))
    {
      await file.CopyToAsync(output, cancellationToken);
    }

    return BuildPublicUrl(tenantId, serviceId, imageId, extension);
  }

  public void DeletePhysical(Guid tenantId, Guid serviceId, Guid imageId, string url)
  {
    var extension = Path.GetExtension(url);
    if (string.IsNullOrWhiteSpace(extension))
    {
      return;
    }

    var physicalPath = Path.Combine(
      _environment.ContentRootPath,
      "uploads",
      "service-images",
      tenantId.ToString("N"),
      serviceId.ToString("N"),
      $"{imageId:N}{extension}");
    if (File.Exists(physicalPath))
    {
      File.Delete(physicalPath);
    }
  }

  private string BuildPublicUrl(Guid tenantId, Guid serviceId, Guid imageId, string extension)
  {
    var relative = $"/uploads/service-images/{tenantId:N}/{serviceId:N}/{imageId:N}{extension}";
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
