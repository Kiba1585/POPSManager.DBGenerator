using System.Collections.Concurrent;
using POPSManager.DBGenerator.Core.Interfaces;
using POPSManager.DBGenerator.Core.Models;
using POPSManager.DBGenerator.Infrastructure.Parsers;

namespace POPSManager.DBGenerator.Application.Pipeline;

public class DatabaseGenerationPipeline
{
    private readonly IEnumerable<IGameSource> _sources;
    private readonly ICoverProvider _coverProvider;
    private readonly ITranslator _translator;      // ← cascada de traductores
    private readonly string _cacheFilePath;
    private readonly string _cfgSourceDir;
    private readonly string[] _targetLanguages;    // ← ahora se inyecta desde fuera

    private ConcurrentDictionary<string, string> _translationCache;

    // Paralelismo controlado (5 peticiones simultáneas)
    private const int MaxDegreeOfParallelism = 5;

    public DatabaseGenerationPipeline(
        IEnumerable<IGameSource> sources,
        ICoverProvider coverProvider,
        ITranslator translator,
        string translationCacheFilePath,
        string cfgSourceDir,
        string[] targetLanguages)
    {
        _sources = sources;
        _coverProvider = coverProvider;
        _translator = translator;
        _cacheFilePath = translationCacheFilePath;
        _translationCache = LoadTranslationCache(_cacheFilePath);
        _cfgSourceDir = cfgSourceDir;
        _targetLanguages = targetLanguages;
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

        // 3. Fusionar por GameId único (la primera fuente que aparezca prevalece)
        var uniqueGames = allGames
            .GroupBy(g => g.GameId)
            .Select(g => g.First())
            .ToList();

        // 4. Enriquecer con datos CFG (metadatos de la comunidad)
        EnrichWithCfgData(uniqueGames);

        // 5. Enriquecer con traducciones (paralelo, multi‑idioma)
        await EnrichWithTranslationsParallelAsync(uniqueGames);

        // 6. Enriquecer con covers (URLs de carátulas)
        EnrichWithCovers(uniqueGames);

        // 7. Post‑procesar discos (agrupa y asigna números de disco)
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

    private async Task EnrichWithTranslationsParallelAsync(List<GameEntry> games)
    {
        int totalNewTranslations = 0;
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism };

        await Parallel.ForEachAsync(games, options, async (game, cancellationToken) =>
        {
            // Para cada idioma deseado
            foreach (var lang in _targetLanguages)
            {
                // -------- Título --------
                string titleKey = $"title_{lang}_{game.Title}";
                string? cachedTitle = _translationCache.GetValueOrDefault(titleKey);

                if (cachedTitle != null)
                {
                    if (cachedTitle != game.Title)
                    {
                        // Guardar en el campo principal o en el diccionario multilingüe
                        if (lang == "es")
                            game.TranslatedTitle = cachedTitle;
                        else
                            game.TranslatedTitles[lang] = cachedTitle;
                    }
                }
                else
                {
                    var translated = await _translator.TranslateAsync(game.Title, "en", lang);
                    if (!string.IsNullOrEmpty(translated) && translated != game.Title)
                    {
                        _translationCache[titleKey] = translated;
                        if (lang == "es")
                            game.TranslatedTitle = translated;
                        else
                            game.TranslatedTitles[lang] = translated;

                        Interlocked.Increment(ref totalNewTranslations);
                    }
                    else
                    {
                        // Guardamos el original para no reintentarlo
                        _translationCache[titleKey] = game.Title;
                    }
                }

                // -------- Descripción (si existe) --------
                if (!string.IsNullOrEmpty(game.Description))
                {
                    string descKey = $"desc_{lang}_{game.Description}";
                    string? cachedDesc = _translationCache.GetValueOrDefault(descKey);

                    if (cachedDesc != null)
                    {
                        if (cachedDesc != game.Description)
                            game.TranslatedDescriptions[lang] = cachedDesc;
                    }
                    else
                    {
                        var translatedDesc = await _translator.TranslateAsync(game.Description, "en", lang);
                        if (!string.IsNullOrEmpty(translatedDesc) && translatedDesc != game.Description)
                        {
                            _translationCache[descKey] = translatedDesc;
                            game.TranslatedDescriptions[lang] = translatedDesc;
                        }
                        else
                        {
                            _translationCache[descKey] = game.Description;
                        }
                    }
                }
            }

            // Pausa opcional cada 50 juegos para no saturar las APIs
            if (Interlocked.Add(ref totalNewTranslations, 0) % 50 == 0)
                await Task.Delay(200, cancellationToken);
        });

        Console.WriteLine($"  🌐 {totalNewTranslations} nuevas traducciones en esta ejecución.");
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

    private ConcurrentDictionary<string, string> LoadTranslationCache(string filePath)
    {
        if (!File.Exists(filePath))
            return new ConcurrentDictionary<string, string>();

        string json = File.ReadAllText(filePath);
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return dict != null ? new ConcurrentDictionary<string, string>(dict) : new ConcurrentDictionary<string, string>();
    }

    private void SaveTranslationCache()
    {
        var normalDict = _translationCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        string json = System.Text.Json.JsonSerializer.Serialize(normalDict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_cacheFilePath, json);
    }
}
