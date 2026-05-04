using System.IO.Compression;
using System.Text.Json;
using POPSManager.DBGenerator.Core.Models;
using POPSManager.DBGenerator.Infrastructure.Parsers;

namespace POPSManager.DBGenerator.Application.Builders;

public class OutputBuilder
{
    private readonly string _outputPath;
    private readonly string _dbOutputDir;
    private readonly string _dbPs1Dir;
    private readonly string _dbPs2Dir;
    private readonly string _dbCfgDir;
    private readonly string _cfgSourceDir;

    public OutputBuilder(string outputPath, string cfgSourceDir)
    {
        _outputPath = outputPath;
        _cfgSourceDir = cfgSourceDir;
        _dbOutputDir = Path.Combine(outputPath, "db");
        _dbPs1Dir = Path.Combine(_dbOutputDir, "ps1");
        _dbPs2Dir = Path.Combine(_dbOutputDir, "ps2");
        _dbCfgDir = Path.Combine(_dbOutputDir, "cfg");

        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(Path.Combine(outputPath, "CFG"));
        Directory.CreateDirectory(_dbPs1Dir);
        Directory.CreateDirectory(_dbPs2Dir);
        Directory.CreateDirectory(_dbCfgDir);
    }

    // ------------------------------------------------
    // MÉTODO PRINCIPAL (recibe la lista de juegos del pipeline)
    // ------------------------------------------------
    public void GenerateAll(List<GameEntry> entries)
    {
        var indexData = new Dictionary<string, object>();

        var ps1 = entries.Where(e => e.Console == "ps1").ToList();
        var ps2 = entries.Where(e => e.Console == "ps2").ToList();

        GenerateFullJson(Path.Combine(_outputPath, "ps1db.json"), ps1);
        GenerateFullJson(Path.Combine(_outputPath, "ps2db.json"), ps2);

        GenerateIndividualFiles(ps1, _dbPs1Dir, "ps1", indexData);
        GenerateIndividualFiles(ps2, _dbPs2Dir, "ps2", indexData);

        // Guardar índice
        string indexPath = Path.Combine(_dbOutputDir, "index.json");
        File.WriteAllText(indexPath, JsonSerializer.Serialize(indexData, new JsonSerializerOptions { WriteIndented = true }));

        // Copiar y enriquecer/crear CFGs (pasándole la lista completa)
        CopyConfigFiles(entries);

        // Empaquetar
        ZipFile.CreateFromDirectory(_outputPath, "POPSManager_DB.zip", CompressionLevel.Optimal, false);
        ZipFile.CreateFromDirectory(_dbOutputDir, "POPSManager_DB_individual.zip", CompressionLevel.Optimal, false);
    }

    // ------------------------------------------------
    // GENERACIÓN DE JSON COMPLETO (con multi-idioma)
    // ------------------------------------------------
    private void GenerateFullJson(string filePath, List<GameEntry> entries)
    {
        var dict = new Dictionary<string, object>();
        foreach (var g in entries)
        {
            var gameObj = new Dictionary<string, object>
            {
                ["name"] = g.TranslatedTitle ?? g.Title,
                ["discNumber"] = g.DiscNumber,
                ["coverUrl"] = g.CoverUrl ?? ""
            };

            // Nombres multi-idioma
            if (g.TranslatedTitles.Count > 0)
            {
                var names = new Dictionary<string, string> { ["en"] = g.Title };
                if (!string.IsNullOrEmpty(g.TranslatedTitle))
                    names["es"] = g.TranslatedTitle;
                foreach (var (lang, title) in g.TranslatedTitles)
                    names[lang] = title;
                gameObj["names"] = names;
            }

            // Metadatos CFG
            if (!string.IsNullOrEmpty(g.Genre)) gameObj["genre"] = g.Genre;
            if (!string.IsNullOrEmpty(g.Players)) gameObj["players"] = g.Players;
            if (!string.IsNullOrEmpty(g.Developer)) gameObj["developer"] = g.Developer;
            if (!string.IsNullOrEmpty(g.ReleaseDate)) gameObj["releaseDate"] = g.ReleaseDate;

            // Descripción multi-idioma
            if (!string.IsNullOrEmpty(g.Description) || g.TranslatedDescriptions.Count > 0)
            {
                var descriptions = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(g.Description))
                    descriptions["en"] = g.Description;
                foreach (var (lang, desc) in g.TranslatedDescriptions)
                    descriptions[lang] = desc;
                if (descriptions.Count > 0)
                    gameObj["description"] = descriptions.Count == 1 && descriptions.ContainsKey("en") ? descriptions["en"] : descriptions;
            }

            dict[g.GameId] = gameObj;
        }
        File.WriteAllText(filePath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"✔️ {filePath} generado con {dict.Count} entradas.");
    }

    // ------------------------------------------------
    // ARCHIVOS INDIVIDUALES
    // ------------------------------------------------
    private void GenerateIndividualFiles(List<GameEntry> entries, string outDir, string console, Dictionary<string, object> index)
    {
        foreach (var g in entries)
        {
            var data = new Dictionary<string, object>
            {
                ["name"] = g.TranslatedTitle ?? g.Title,
                ["discNumber"] = g.DiscNumber,
                ["coverUrl"] = g.CoverUrl ?? "",
                ["console"] = console
            };

            if (g.TranslatedTitles.Count > 0)
            {
                var names = new Dictionary<string, string> { ["en"] = g.Title };
                if (!string.IsNullOrEmpty(g.TranslatedTitle))
                    names["es"] = g.TranslatedTitle;
                foreach (var (lang, title) in g.TranslatedTitles)
                    names[lang] = title;
                data["names"] = names;
            }

            if (!string.IsNullOrEmpty(g.Genre)) data["genre"] = g.Genre;
            if (!string.IsNullOrEmpty(g.Players)) data["players"] = g.Players;
            if (!string.IsNullOrEmpty(g.Developer)) data["developer"] = g.Developer;
            if (!string.IsNullOrEmpty(g.ReleaseDate)) data["releaseDate"] = g.ReleaseDate;

            if (!string.IsNullOrEmpty(g.Description) || g.TranslatedDescriptions.Count > 0)
            {
                var descriptions = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(g.Description))
                    descriptions["en"] = g.Description;
                foreach (var (lang, desc) in g.TranslatedDescriptions)
                    descriptions[lang] = desc;
                data["description"] = descriptions.Count == 1 && descriptions.ContainsKey("en") ? descriptions["en"] : descriptions;
            }

            string fileName = $"{g.GameId}.json";
            string filePath = Path.Combine(outDir, fileName);
            File.WriteAllText(filePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

            index[g.GameId] = new
            {
                name = g.TranslatedTitle ?? g.Title,
                console,
                jsonPath = $"{console}/{fileName}",
                cfgPath = $"cfg/{g.GameId}.cfg"
            };
        }
        Console.WriteLine($"✔️ Archivos individuales generados en {outDir} ({entries.Count} juegos)");
    }

    // ------------------------------------------------
    // COPIA Y ENRIQUECIMIENTO/CRECCIÓN DE CFGs
    // ------------------------------------------------
    private void CopyConfigFiles(List<GameEntry> allEntries)
    {
        if (!Directory.Exists(_cfgSourceDir))
        {
            Console.WriteLine("⚠️ Carpeta de CFG no encontrada, se omite la copia.");
            return;
        }

        var entryDict = allEntries.ToDictionary(e => e.GameId, e => e);
        int enriched = 0, created = 0;

        // Procesar los CFGs existentes (enriquecerlos si faltan campos)
        foreach (string cfgFile in Directory.GetFiles(_cfgSourceDir, "*.cfg"))
        {
            string gameId = Path.GetFileNameWithoutExtension(cfgFile);
            var cfgData = CfgParser.Parse(cfgFile);

            if (entryDict.TryGetValue(gameId, out var gameEntry))
            {
                bool modified = false;
                if (!cfgData.ContainsKey("title") && !string.IsNullOrEmpty(gameEntry.TranslatedTitle ?? gameEntry.Title))
                {
                    cfgData["title"] = gameEntry.TranslatedTitle ?? gameEntry.Title;
                    modified = true;
                }
                if (!cfgData.ContainsKey("genre") && !string.IsNullOrEmpty(gameEntry.Genre))
                {
                    cfgData["genre"] = gameEntry.Genre;
                    modified = true;
                }
                if (!cfgData.ContainsKey("players") && !string.IsNullOrEmpty(gameEntry.Players))
                {
                    cfgData["players"] = gameEntry.Players;
                    modified = true;
                }
                if (!cfgData.ContainsKey("developer") && !string.IsNullOrEmpty(gameEntry.Developer))
                {
                    cfgData["developer"] = gameEntry.Developer;
                    modified = true;
                }
                if (!cfgData.ContainsKey("release") && !string.IsNullOrEmpty(gameEntry.ReleaseDate))
                {
                    cfgData["release"] = gameEntry.ReleaseDate;
                    modified = true;
                }
                if (!cfgData.ContainsKey("description") && !string.IsNullOrEmpty(gameEntry.Description))
                {
                    cfgData["description"] = gameEntry.Description;
                    modified = true;
                }
                if (modified) enriched++;
            }

            // Escribir el CFG (enriquecido o no) a las dos salidas
            string destFile = Path.Combine(_outputPath, "CFG", Path.GetFileName(cfgFile));
            WriteCfgFile(destFile, cfgData);

            string indiFile = Path.Combine(_dbCfgDir, Path.GetFileName(cfgFile));
            WriteCfgFile(indiFile, cfgData);
        }

        // Crear CFGs mínimos para juegos que no tienen archivo .cfg en la fuente
        foreach (var gameEntry in allEntries.Where(e => !File.Exists(Path.Combine(_cfgSourceDir, $"{e.GameId}.cfg"))))
        {
            var minCfg = new Dictionary<string, string>
            {
                ["title"] = gameEntry.TranslatedTitle ?? gameEntry.Title,
                ["disc"] = gameEntry.DiscNumber.ToString()
            };

            string newCfgFile = Path.Combine(_outputPath, "CFG", $"{gameEntry.GameId}.cfg");
            WriteCfgFile(newCfgFile, minCfg);

            string indiNewCfgFile = Path.Combine(_dbCfgDir, $"{gameEntry.GameId}.cfg");
            WriteCfgFile(indiNewCfgFile, minCfg);
            created++;
        }

        Console.WriteLine($"  📄 CFGs enriquecidos: {enriched}, nuevos: {created}");
    }

    // Método auxiliar para escribir un archivo .cfg
    private void WriteCfgFile(string filePath, Dictionary<string, string> data)
    {
        using var writer = new StreamWriter(filePath);
        foreach (var (key, value) in data)
        {
            writer.WriteLine($"{key}={value}");
        }
    }
}