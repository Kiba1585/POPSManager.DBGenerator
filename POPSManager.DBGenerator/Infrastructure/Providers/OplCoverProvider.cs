using POPSManager.DBGenerator.Core.Interfaces;

namespace POPSManager.DBGenerator.Infrastructure.Providers;

public class OplCoverProvider : ICoverProvider
{
    private const string BaseUrl = "https://archive.org/download/oplm-art-2023-11/ART/";

    public string? GetCoverUrl(string gameId)
    {
        return $"{BaseUrl}{gameId}.jpg";
    }
}
