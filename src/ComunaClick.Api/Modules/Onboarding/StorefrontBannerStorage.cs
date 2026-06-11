namespace ComunaClick.Api.Modules.Onboarding;

public sealed class StorefrontBannerStorage
{
  private const int MaxBannerBytes = 5 * 1024 * 1024;
  private const int MaxLogoBytes = 4 * 1024 * 1024;

  private static readonly HashSet<string> AllowedBannerContentTypes = new(StringComparer.OrdinalIgnoreCase)
  {
    "image/jpeg",
    "image/png",
    "image/webp"
  };

  private static readonly HashSet<string> AllowedLogoContentTypes = new(StringComparer.OrdinalIgnoreCase)
  {
    "image/jpeg",
    "image/png",
    "image/webp",
    "image/gif"
  };

  private readonly IWebHostEnvironment _environment;
  private readonly IConfiguration _configuration;

  public StorefrontBannerStorage(IWebHostEnvironment environment, IConfiguration configuration)
  {
    _environment = environment;
    _configuration = configuration;
  }

  public async Task<string?> SavePartnerBannerAsync(
    Guid tenantId,
    Guid partnerId,
    IFormFile file,
    CancellationToken cancellationToken = default)
    => await SaveAsync("partner-banners", tenantId, partnerId, file, MaxBannerBytes, AllowedBannerContentTypes, cancellationToken);

  public async Task<string?> SavePartnerLogoAsync(
    Guid tenantId,
    Guid partnerId,
    IFormFile file,
    CancellationToken cancellationToken = default)
    => await SaveAsync("partner-logos", tenantId, partnerId, file, MaxLogoBytes, AllowedLogoContentTypes, cancellationToken);

  public void DeletePartnerLogo(Guid tenantId, Guid partnerId, string url)
    => DeletePhysical("partner-logos", tenantId, partnerId, url);

  public async Task<string?> SaveProfessionalBannerAsync(
    Guid tenantId,
    Guid professionalId,
    IFormFile file,
    CancellationToken cancellationToken = default)
    => await SaveAsync("professional-banners", tenantId, professionalId, file, MaxBannerBytes, AllowedBannerContentTypes, cancellationToken);

  public async Task<string?> SaveProfessionalPhotoAsync(
    Guid tenantId,
    Guid professionalId,
    IFormFile file,
    CancellationToken cancellationToken = default)
    => await SaveAsync("professional-photos", tenantId, professionalId, file, MaxLogoBytes, AllowedLogoContentTypes, cancellationToken);

  public void DeletePartnerBanner(Guid tenantId, Guid partnerId, string url)
    => DeletePhysical("partner-banners", tenantId, partnerId, url);

  public void DeleteProfessionalBanner(Guid tenantId, Guid professionalId, string url)
    => DeletePhysical("professional-banners", tenantId, professionalId, url);

  public void DeleteProfessionalPhoto(Guid tenantId, Guid professionalId, string url)
    => DeletePhysical("professional-photos", tenantId, professionalId, url);

  private async Task<string?> SaveAsync(
    string folderName,
    Guid tenantId,
    Guid entityId,
    IFormFile file,
    int maxBytes,
    HashSet<string> allowedContentTypes,
    CancellationToken cancellationToken)
  {
    if (file.Length <= 0 || file.Length > maxBytes)
    {
      return null;
    }

    if (!allowedContentTypes.Contains(file.ContentType))
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
      folderName,
      tenantId.ToString("N"));
    Directory.CreateDirectory(folder);

    foreach (var existing in Directory.EnumerateFiles(folder, $"{entityId:N}.*"))
    {
      File.Delete(existing);
    }

    var physicalPath = Path.Combine(folder, $"{entityId:N}{extension}");
    await using (var output = File.Create(physicalPath))
    {
      await file.CopyToAsync(output, cancellationToken);
    }

    return BuildPublicUrl(folderName, tenantId, entityId, extension);
  }

  private void DeletePhysical(string folderName, Guid tenantId, Guid entityId, string url)
  {
    var extension = Path.GetExtension(url);
    if (string.IsNullOrWhiteSpace(extension))
    {
      return;
    }

    var physicalPath = Path.Combine(
      _environment.ContentRootPath,
      "uploads",
      folderName,
      tenantId.ToString("N"),
      $"{entityId:N}{extension}");
    if (File.Exists(physicalPath))
    {
      File.Delete(physicalPath);
    }
  }

  private string BuildPublicUrl(string folderName, Guid tenantId, Guid entityId, string extension)
  {
    var relative = $"/uploads/{folderName}/{tenantId:N}/{entityId:N}{extension}";
    var configured = _configuration["PublicApi:BaseUrl"]?.TrimEnd('/');
    return !string.IsNullOrWhiteSpace(configured) ? $"{configured}{relative}" : relative;
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
