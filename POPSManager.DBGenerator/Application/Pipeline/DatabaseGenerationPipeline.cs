using POPSManager.DBGenerator.Core.Interfaces;
using POPSManager.DBGenerator.Core.Models;
using POPSManager.DBGenerator.Infrastructure.Parsers;

namespace POPSManager.DBGenerator.Application.Pipeline;

public class DatabaseGenerationPipeline
{
    private readonly IEnumerable<IGameSource> _sources;
    private readonly ICoverProvider _coverProvider;
    private readonly ITranslator _translator;
    private readonly string _cacheFilePath;
    private readonly string _cfgSourceDir;
    private Dictionary<string, string> _translationCache;
    private readonly string[] _targetLanguages = { "es", "fr", "de", "it", "ja" }; // configurable

    public DatabaseGenerationPipeline(
        IEnumerable<IGameSource> sources,
        ICoverProvider coverProvider,
        ITranslator translator,
        string translationCacheFilePath,
        string cfgSourceDir)
    {
        _sources = sources;
        _coverProvider = coverProvider;
        _translator = translator;
        _cacheFilePath = translationCacheFilePath;
        _translationCache = LoadTranslationCache(_cacheFilePath);
        _cfgSourceDir = cfgSourceDir;
    }

    public async Task<List<GameEntry>> BuildAsync()
    {
        // 1. Cargar fuentes
        var allGames = new List<GameEntry>();
        foreach (var source in _sources)
        {
            var games = await source.GetGamesAsync();
            allGames.AddRange(games);
        }

        // 2. Normalizar IDs
        foreach (var game in allGames)
        {
            game.GameId = game.GameId.ToUpperInvariant().Replace("-", "_");
        }

        // 3. Fusionar por GameId único
        var uniqueGames = allGames
            .GroupBy(g => g.GameId)
            .Select(g => g.First())
            .ToList();

        // 4. Enriquecer con datos CFG (necesario antes de traducir descripciones)
        EnrichWithCfgData(uniqueGames);

        // 5. Enriquecer con traducciones (multilenguaje)
        await EnrichWithTranslations(uniqueGames);

        // 6. Enriquecer con covers
        EnrichWithCovers(uniqueGames);

        // 7. Post-procesar discos
        uniqueGames = AdjustDiscNumbers(uniqueGames);

        SaveTranslationCache();

        return uniqueGames;
    }

    private void EnrichWithCfgData(List<GameEntry> games)
    {
        if (!Directory.Exists(_cfgSourceDir))
        {
            Console.WriteLine("⚠️ Carpeta de CFG no encontrada, omitiendo enriquecimiento.");
            return;
        }

        int enriched = 0;
        foreach (var game in games)
        {
            string cfgFile = Path.Combine(_cfgSourceDir, $"{game.GameId}.cfg");
            if (!File.Exists(cfgFile))
                continue;

            var cfgData = CfgParser.Parse(cfgFile);
            CfgParser.ApplyToGameEntry(game, cfgData);
            enriched++;
        }
        Console.WriteLine($"  📋 {enriched} juegos enriquecidos con datos CFG.");
    }

    private async Task EnrichWithTranslations(List<GameEntry> games)
    {
        int newTranslations = 0;
        foreach (var game in games)
        {
            // Título principal al español (para mantener compatibilidad)
            if (!_translationCache.TryGetValue($"title_es_{game.Title}", out var cachedTitleEs))
            {
                var translated = await _translator.TranslateAsync(game.Title, "en", "es");
                if (!string.IsNullOrEmpty(translated) && translated != game.Title)
                {
                    _translationCache[$"title_es_{game.Title}"] = translated;
                    game.TranslatedTitle = translated;
                    newTranslations++;
                }
                else
                {
                    _translationCache[$"title_es_{game.Title}"] = game.Title;
                }
            }
            else
            {
                game.TranslatedTitle = cachedTitleEs != game.Title ? cachedTitleEs : null;
            }

            // Títulos en otros idiomas
            foreach (var lang in _targetLanguages.Except(new[] { "es" }))
            {
                string cacheKey = $"title_{lang}_{game.Title}";
                if (!_translationCache.TryGetValue(cacheKey, out var cachedTitle))
                {
                    var translated = await _translator.TranslateAsync(game.Title, "en", lang);
                    if (!string.IsNullOrEmpty(translated) && translated != game.Title)
                    {
                        _translationCache[cacheKey] = translated;
                        game.TranslatedTitles[lang] = translated;
                    }
                    else
                    {
                        _translationCache[cacheKey] = game.Title;
                    }
                    await Task.Delay(200); // pausa entre peticiones
                }
                else if (cachedTitle != game.Title)
                {
                    game.TranslatedTitles[lang] = cachedTitle;
                }
            }

            // Descripción en español (principal)
            if (!string.IsNullOrEmpty(game.Description))
            {
                string descCacheKey = $"desc_es_{game.Description}";
                if (!_translationCache.TryGetValue(descCacheKey, out var cachedDescEs))
                {
                    var translatedDesc = await _translator.TranslateAsync(game.Description, "en", "es");
                    if (!string.IsNullOrEmpty(translatedDesc) && translatedDesc != game.Description)
                    {
                        _translationCache[descCacheKey] = translatedDesc;
                        game.TranslatedDescriptions["es"] = translatedDesc;
                    }
                    else
                    {
                        _translationCache[descCacheKey] = game.Description;
                    }
                }
                else if (cachedDescEs != game.Description)
                {
                    game.TranslatedDescriptions["es"] = cachedDescEs;
                }

                // Descripciones en otros idiomas
                foreach (var lang in _targetLanguages.Except(new[] { "es" }))
                {
                    string descLangKey = $"desc_{lang}_{game.Description}";
                    if (!_translationCache.TryGetValue(descLangKey, out var cachedDescLang))
                    {
                        var translatedDescLang = await _translator.TranslateAsync(game.Description, "en", lang);
                        if (!string.IsNullOrEmpty(translatedDescLang) && translatedDescLang != game.Description)
                        {
                            _translationCache[descLangKey] = translatedDescLang;
                            game.TranslatedDescriptions[lang] = translatedDescLang;
                        }
                        else
                        {
                            _translationCache[descLangKey] = game.Description;
                        }
                        await Task.Delay(200);
                    }
                    else if (cachedDescLang != game.Description)
                    {
                        game.TranslatedDescriptions[lang] = cachedDescLang;
                    }
                }
            }

            if (newTranslations % 5 == 0)
                await Task.Delay(1000);
        }
        Console.WriteLine($"  🌐 {newTranslations} nuevos títulos traducidos al español.");
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

    private void SaveTranslationCache()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(_translationCache, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_cacheFilePath, json);
    }
}
