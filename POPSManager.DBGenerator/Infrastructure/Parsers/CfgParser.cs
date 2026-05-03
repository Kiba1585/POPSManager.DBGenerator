using POPSManager.DBGenerator.Core.Models;

namespace POPSManager.DBGenerator.Infrastructure.Parsers;

public static class CfgParser
{
    public static Dictionary<string, string> Parse(string cfgFilePath)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(cfgFilePath))
            return result;

        foreach (string line in File.ReadAllLines(cfgFilePath))
        {
            int idx = line.IndexOf('=');
            if (idx < 0) continue;

            string key = line[..idx].Trim().ToLowerInvariant();
            string value = line[(idx + 1)..].Trim();

            if (!string.IsNullOrEmpty(value))
                result[key] = value;
        }
        return result;
    }

    public static void ApplyToGameEntry(GameEntry entry, Dictionary<string, string> cfgData)
    {
        if (string.IsNullOrEmpty(entry.Genre) && cfgData.TryGetValue("genre", out var genre))
            entry.Genre = genre;
        if (string.IsNullOrEmpty(entry.Players) && cfgData.TryGetValue("players", out var players))
            entry.Players = players;
        if (string.IsNullOrEmpty(entry.Description) && cfgData.TryGetValue("description", out var desc))
            entry.Description = desc;
        if (string.IsNullOrEmpty(entry.Developer) && cfgData.TryGetValue("developer", out var dev))
            entry.Developer = dev;
        if (string.IsNullOrEmpty(entry.ReleaseDate) && cfgData.TryGetValue("release", out var rel))
            entry.ReleaseDate = rel;
    }
}
