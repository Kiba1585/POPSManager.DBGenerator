using System.IO.Compression;
using System.Text.Json;
using POPSManager.DBGenerator.Core.Models;

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

    public void GenerateAll(List<GameEntry> entries)
    {
        var indexData = new Dictionary<string, object>();

        var ps1 = entries.Where(e => e.Console == "ps1").ToList();
        var ps2 = entries.Where(e => e.Console == "ps2").ToList();

        GenerateFullJson(Path.Combine(_outputPath, "ps1db.json"), ps1);
        GenerateFullJson(Path.Combine(_outputPath, "ps2db.json"), ps2);

        GenerateIndividualFiles(ps1, _dbPs1Dir, "ps1", indexData);
        GenerateIndividualFiles(ps2, _dbPs2Dir, "ps2", indexData);

        string indexPath = Path.Combine(_dbOutputDir, "index.json");
        File.WriteAllText(indexPath, JsonSerializer.Serialize(indexData, new JsonSerializerOptions { WriteIndented = true }));

        CopyConfigFiles();

        ZipFile.CreateFromDirectory(_outputPath, "POPSManager_DB.zip", CompressionLevel.Optimal, false);
        ZipFile.CreateFromDirectory(_dbOutputDir, "POPSManager_DB_individual.zip", CompressionLevel.Optimal, false);
    }

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

            // Nombres multi-idioma
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

    private void CopyConfigFiles()
    {
        if (!Directory.Exists(_cfgSourceDir))
        {
            Console.WriteLine("⚠️ Carpeta de CFG no encontrada, se omite la copia.");
            return;
        }

        foreach (string cfgFile in Directory.GetFiles(_cfgSourceDir, "*.cfg"))
        {
            string destFile = Path.Combine(_outputPath, "CFG", Path.GetFileName(cfgFile));
            File.Copy(cfgFile, destFile, overwrite: true);

            string indiFile = Path.Combine(_dbCfgDir, Path.GetFileName(cfgFile));
            File.Copy(cfgFile, indiFile, overwrite: true);
        }
    }
}
