using POPSManager.DBGenerator.Core.Interfaces;
using POPSManager.DBGenerator.Core.Models;
using System.Text.RegularExpressions;

namespace POPSManager.DBGenerator.Application.Pipeline;

public class DatabaseGenerationPipeline
{
    private readonly IEnumerable<IGameSource> _sources;
    private readonly ICoverProvider _coverProvider;
    private readonly ITranslator _translator;
    private readonly Dictionary<string, string> _translationCache;
    private readonly string _cacheFilePath;

    public DatabaseGenerationPipeline(
        IEnumerable<IGameSource> sources,
        ICoverProvider coverProvider,
        ITranslator translator,
        string translationCacheFilePath)
    {
        _sources = sources;
        _coverProvider = coverProvider;
        _translator = translator;
        _cacheFilePath = translationCacheFilePath;
        _translationCache = LoadTranslationCache(_cacheFilePath);
    }

    public async Task<List<GameEntry>> BuildAsync()
    {
        // 1. Cargar todos los juegos de las fuentes
        var allGames = new List<GameEntry>();
        foreach (var source in _sources)
        {
            var games = await source.GetGamesAsync();
            allGames.AddRange(games);
        }

        // 2. Normalizar IDs (mayúsculas, guiones bajos)
        foreach (var game in allGames)
        {
            game.GameId = game.GameId.ToUpperInvariant().Replace("-", "_");
        }

        // 3. Fusionar por GameId único (primera fuente prevalece)
        var uniqueGames = allGames
            .GroupBy(g => g.GameId)
            .Select(g => g.First())
            .ToList();

        // 4. Enriquecer: traducción y covers
        uniqueGames = await EnrichWithTranslation(uniqueGames);
        EnrichWithCovers(uniqueGames);

        // 5. Post-procesar: detección de discos múltiples
        uniqueGames = AdjustDiscNumbers(uniqueGames);

        SaveTranslationCache(_cacheFilePath);

        return uniqueGames;
    }

    private async Task<List<GameEntry>> EnrichWithTranslation(List<GameEntry> games)
    {
        int newTranslations = 0;
        foreach (var game in games)
        {
            if (_translationCache.TryGetValue(game.Title, out var cached))
            {
                game.TranslatedTitle = cached != game.Title ? cached : null;
                continue;
            }

            try
            {
                var translated = await _translator.TranslateAsync(game.Title);
                if (!string.IsNullOrEmpty(translated) && translated != game.Title)
                {
                    _translationCache[game.Title] = translated;
                    game.TranslatedTitle = translated;
                    newTranslations++;
                    Console.WriteLine($"  ✓ {game.GameId}: {translated}");
                }
                else
                {
                    _translationCache[game.Title] = game.Title;
                }
            }
            catch
            {
                _translationCache[game.Title] = game.Title;
            }

            if (newTranslations % 10 == 0)
                await Task.Delay(1000);
        }
        Console.WriteLine($"  📝 {newTranslations} nuevos títulos traducidos.");
        return games;
    }

    private void EnrichWithCovers(List<GameEntry> games)
    {
        foreach (var game in games)
        {
            game.CoverUrl = _coverProvider.GetCoverUrl(game.GameId);
        }
    }

    private List<GameEntry> AdjustDiscNumbers(List<GameEntry> entries)
    {
        var groups = entries
            .Where(e => !string.IsNullOrEmpty(e.Title))
            .GroupBy(e => NormalizeTitleForGrouping(e.Title))
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var sorted = group.OrderBy(e => e.GameId).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].DiscNumber == 1)
                    sorted[i].DiscNumber = i + 1;
            }
        }
        return entries;
    }

    private string NormalizeTitleForGrouping(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        return title.ToUpperInvariant()
            .Replace("THE ", "")
            .Replace("A ", "")
            .Replace("AN ", "")
            .Trim();
    }

    private Dictionary<string, string> LoadTranslationCache(string filePath)
    {
        if (!File.Exists(filePath))
            return new Dictionary<string, string>();

        string json = File.ReadAllText(filePath);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
    }

    private void SaveTranslationCache(string filePath)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(_translationCache, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }
}