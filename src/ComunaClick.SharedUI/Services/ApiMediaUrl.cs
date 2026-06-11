using ComunaClick.Shared.Http;
using Microsoft.Extensions.Options;

namespace ComunaClick.SharedUI.Services;

public sealed class ApiMediaUrl
{
    private readonly ApiOptions _options;

    public ApiMediaUrl(IOptions<ApiOptions> options)
    {
        _options = options.Value;
    }

    public string Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmed = url.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("_content/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith('/'))
        {
            return $"{_options.ApiBaseUrl.TrimEnd('/')}{trimmed}";
        }

        return trimmed;
    }

    public IReadOnlyList<string> ResolveMany(IEnumerable<string>? urls)
        => urls?.Select(Resolve).Where(x => x.Length > 0).ToList() ?? new List<string>();
}
