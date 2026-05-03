using POPSManager.DBGenerator.Core.Interfaces;

namespace POPSManager.DBGenerator.Infrastructure.Providers;

/// <summary>
/// Intenta varios proveedores en orden hasta que uno devuelva una URL.
/// </summary>
public class FallbackCoverProvider : ICoverProvider
{
    private readonly IEnumerable<ICoverProvider> _providers;

    public FallbackCoverProvider(params ICoverProvider[] providers)
    {
        _providers = providers;
    }

    public string? GetCoverUrl(string gameId)
    {
        foreach (var provider in _providers)
        {
            var url = provider.GetCoverUrl(gameId);
            if (!string.IsNullOrEmpty(url))
                return url;
        }
        return null;
    }
}