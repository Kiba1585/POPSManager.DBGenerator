using System.Text.Json;
using POPSManager.DBGenerator.Core.Interfaces;

namespace POPSManager.DBGenerator.Infrastructure.Providers;

public class ExtraUrlsCoverProvider : ICoverProvider
{
    private readonly Dictionary<string, string> _extraUrls;

    public ExtraUrlsCoverProvider(string extraUrlsFilePath)
    {
        _extraUrls = new Dictionary<string, string>();
        if (File.Exists(extraUrlsFilePath))
        {
            string json = File.ReadAllText(extraUrlsFilePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null) _extraUrls = dict;
        }
    }

    public string? GetCoverUrl(string gameId)
    {
        return _extraUrls.TryGetValue(gameId, out var url) ? url : null;
    }
}