using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

// ============================================================
// CONFIGURACIÓN
// ============================================================
const string dataPath = "Data";
const string outputPath = "Output";
static readonly string CfgSourceDir = Path.Combine(dataPath, "cfg_database");
static readonly string Ps1DatFile = Path.Combine(dataPath, "psx.dat");
static readonly string Ps2DatFile = Path.Combine(dataPath, "ps2.dat");
const string CoverArtBaseUrl = "https://archive.org/download/oplm-art-2023-11/ART/";
static readonly string ExtraUrlsFile = Path.Combine(dataPath, "extra_urls.json");

// Traducción
const bool enableAutoTranslation = true; // Cambia a false si quieres desactivarla
static readonly string TranslationCacheFile = Path.Combine(dataPath, "translation_cache.json");

// Carpetas para archivos individuales
static readonly string DbOutputDir = Path.Combine(outputPath, "db");
static readonly string DbPs1Dir = Path.Combine(DbOutputDir, "ps1");
static readonly string DbPs2Dir = Path.Combine(DbOutputDir, "ps2");
static readonly string DbCfgDir = Path.Combine(DbOutputDir, "cfg");

// --------------------------------------------------
// 1. Preparar directorios de salida
// --------------------------------------------------
Directory.CreateDirectory(outputPath);
Directory.CreateDirectory(Path.Combine(outputPath, "CFG"));
Directory.CreateDirectory(DbPs1Dir);
Directory.CreateDirectory(DbPs2Dir);
Directory.CreateDirectory(DbCfgDir);

// --------------------------------------------------
// 2. Parsear datfiles de Redump
// --------------------------------------------------
List<RedumpEntry> ps1Entries = ParseRedumpDat(Ps1DatFile);
List<RedumpEntry> ps2Entries = ParseRedumpDat(Ps2DatFile);

// --------------------------------------------------
// 3. Cargar URLs extra y caché de traducciones
// --------------------------------------------------
Dictionary<string, string> extraUrls = LoadExtraUrls(ExtraUrlsFile);
Dictionary<string, string> translationCache = LoadTranslationCache(TranslationCacheFile);

// --------------------------------------------------
// 4. Traducir títulos si está activado
// --------------------------------------------------
if (enableAutoTranslation)
{
    Console.WriteLine("🌐 Iniciando traducción automática de títulos (inglés → español)...");
    ps1Entries = await TranslateTitlesAsync(ps1Entries, translationCache);
    ps2Entries = await TranslateTitlesAsync(ps2Entries, translationCache);
    SaveTranslationCache(TranslationCacheFile, translationCache);
    Console.WriteLine("✅ Traducción completada.");
}

// --------------------------------------------------
// 5. Ajustar discos múltiples
// --------------------------------------------------
ps1Entries = AdjustDiscNumbers(ps1Entries);
ps2Entries = AdjustDiscNumbers(ps2Entries);

// --------------------------------------------------
// 6. Generar JSON completos (para el ZIP completo)
// --------------------------------------------------
GenerateDatabaseJson(Path.Combine(outputPath, "ps1db.json"), ps1Entries, extraUrls);
GenerateDatabaseJson(Path.Combine(outputPath, "ps2db.json"), ps2Entries, extraUrls);

// --------------------------------------------------
// 7. Generar índice y archivos individuales
// --------------------------------------------------
var indexData = new Dictionary<string, object>();
GenerateIndividualFiles(ps1Entries, extraUrls, DbPs1Dir, "ps1", indexData);
GenerateIndividualFiles(ps2Entries, extraUrls, DbPs2Dir, "ps2", indexData);

// Guardar índice
string indexPath = Path.Combine(DbOutputDir, "index.json");
File.WriteAllText(indexPath, JsonSerializer.Serialize(indexData, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"✔️ Índice generado con {indexData.Count} juegos");

// --------------------------------------------------
// 8. Copiar archivos .cfg
// --------------------------------------------------
if (Directory.Exists(CfgSourceDir))
{
    foreach (string cfgFile in Directory.GetFiles(CfgSourceDir, "*.cfg"))
    {
        string destFile = Path.Combine(outputPath, "CFG", Path.GetFileName(cfgFile));
        File.Copy(cfgFile, destFile, overwrite: true);
        string indiCfgFile = Path.Combine(DbCfgDir, Path.GetFileName(cfgFile));
        File.Copy(cfgFile, indiCfgFile, overwrite: true);
    }
}
else
{
    Console.WriteLine("⚠️ Carpeta de CFG no encontrada, se omite la copia.");
}

// --------------------------------------------------
// 9. Empaquetar en ZIPs
// --------------------------------------------------
string fullZipPath = "POPSManager_DB.zip";
if (File.Exists(fullZipPath)) File.Delete(fullZipPath);
ZipFile.CreateFromDirectory(outputPath, fullZipPath, CompressionLevel.Optimal, false);
Console.WriteLine($"✅ Base de datos completa generada en {fullZipPath}");

string indiZipPath = "POPSManager_DB_individual.zip";
if (File.Exists(indiZipPath)) File.Delete(indiZipPath);
ZipFile.CreateFromDirectory(DbOutputDir, indiZipPath, CompressionLevel.Optimal, false);
Console.WriteLine($"✅ Base de datos individual generada en {indiZipPath}");

// ==================================================
// MÉTODOS AUXILIARES
// ==================================================

static List<RedumpEntry> ParseRedumpDat(string filePath)
{
    var entries = new List<RedumpEntry>();
    if (!File.Exists(filePath))
    {
        Console.WriteLine($"❌ No se encontró {filePath}");
        return entries;
    }

    var doc = new XmlDocument();
    doc.Load(filePath);

    foreach (XmlNode gameNode in doc.SelectNodes("//game")!)
    {
        string id = gameNode.Attributes?["name"]?.Value ?? "";
        string rawTitle = gameNode.SelectSingleNode("description")?.InnerText ?? id;

        int discNumber = 1;
        string cleanTitle = rawTitle;

        var discMatch = Regex.Match(rawTitle, @"\(Disc\s*(\d+)(?:\s*of\s*\d+)?\)", RegexOptions.IgnoreCase);
        if (discMatch.Success)
        {
            discNumber = int.Parse(discMatch.Groups[1].Value);
            cleanTitle = Regex.Replace(rawTitle, @"\s*\(Disc\s*\d+(?:\s*of\s*\d+)?\)\s*", "", RegexOptions.IgnoreCase).Trim();
        }

        string serial = gameNode.SelectSingleNode("serial")?.InnerText ?? "";
        string gameId = serial.Split(' ')[0].Trim();
        if (string.IsNullOrWhiteSpace(gameId)) gameId = id;

        entries.Add(new RedumpEntry
        {
            GameId = gameId,
            Title = string.IsNullOrWhiteSpace(cleanTitle) ? rawTitle : cleanTitle,
            DiscNumber = discNumber,
            Serial = serial
        });
    }

    return entries;
}

static List<RedumpEntry> AdjustDiscNumbers(List<RedumpEntry> entries)
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

static string NormalizeTitleForGrouping(string title)
{
    if (string.IsNullOrWhiteSpace(title)) return "";
    return title.ToUpperInvariant()
        .Replace("THE ", "")
        .Replace("A ", "")
        .Replace("AN ", "")
        .Trim();
}

static Dictionary<string, string> LoadExtraUrls(string filePath)
{
    if (!File.Exists(filePath))
    {
        Console.WriteLine("ℹ️ No se encontró extra_urls.json, usando solo mirror OPL.");
        return new Dictionary<string, string>();
    }

    string json = File.ReadAllText(filePath);
    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
    Console.WriteLine($"✔️ Cargadas {dict?.Count ?? 0} URLs extra.");
    return dict ?? new Dictionary<string, string>();
}

// ==================================================
// TRADUCCIÓN AUTOMÁTICA
// ==================================================

/// <summary>
/// Carga la caché de traducciones guardada (título original → título traducido).
/// </summary>
static Dictionary<string, string> LoadTranslationCache(string filePath)
{
    if (!File.Exists(filePath))
        return new Dictionary<string, string>();

    string json = File.ReadAllText(filePath);
    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
}

/// <summary>
/// Guarda la caché de traducciones en disco.
/// </summary>
static void SaveTranslationCache(string filePath, Dictionary<string, string> cache)
{
    string json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(filePath, json);
}

/// <summary>
/// Traduce una lista de títulos usando MyMemory (gratuito, sin API key).
/// </summary>
static async Task<List<RedumpEntry>> TranslateTitlesAsync(List<RedumpEntry> entries, Dictionary<string, string> cache)
{
    using var httpClient = new HttpClient();
    var translatedEntries = new List<RedumpEntry>();
    int newTranslations = 0;

    foreach (var entry in entries)
    {
        // Si ya está en caché, usamos la traducción guardada
        if (cache.TryGetValue(entry.Title, out string? cachedTranslation))
        {
            entry.Title = cachedTranslation;
            translatedEntries.Add(entry);
            continue;
        }

        // Intentar traducir
        try
        {
            string? translated = await TranslateTextAsync(httpClient, entry.Title, "en", "es");

            if (!string.IsNullOrEmpty(translated) && translated != entry.Title)
            {
                cache[entry.Title] = translated;
                entry.Title = translated;
                newTranslations++;
                Console.WriteLine($"  ✓ {entry.GameId}: {translated}");
            }
            else
            {
                // Si la traducción falla, guardamos el original para no reintentar
                cache[entry.Title] = entry.Title;
            }
        }
        catch
        {
            // Si hay error, guardamos el original para no reintentar
            cache[entry.Title] = entry.Title;
        }

        translatedEntries.Add(entry);

        // Pequeña pausa para no saturar la API (límite: 5000 chars/día sin email)
        if (newTranslations % 10 == 0)
            await Task.Delay(1000);
    }

    Console.WriteLine($"  📝 {newTranslations} títulos nuevos traducidos en esta ejecución.");
    return translatedEntries;
}

/// <summary>
/// Traduce un texto usando la API gratuita de MyMemory.
/// Límites: 5000 caracteres/día (anónimo), 50000 caracteres/día (con email).
/// Máximo 500 bytes por solicitud.
/// </summary>
static async Task<string?> TranslateTextAsync(HttpClient client, string text, string sourceLang, string targetLang)
{
    if (string.IsNullOrWhiteSpace(text))
        return text;

    // MyMemory tiene un límite de 500 bytes por solicitud
    string truncatedText = text.Length > 400 ? text[..400] : text;

    string url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(truncatedText)}&langpair={sourceLang}|{targetLang}";

    try
    {
        var response = await client.GetStringAsync(url);
        using var doc = JsonDocument.Parse(response);

        if (doc.RootElement.TryGetProperty("responseData", out var responseData) &&
            responseData.TryGetProperty("translatedText", out var translatedText))
        {
            return translatedText.GetString();
        }
    }
    catch
    {
        // Silencioso: si falla, simplemente no se traduce
    }

    return null;
}

// ==================================================
// GENERACIÓN DE JSON
// ==================================================

static void GenerateDatabaseJson(string outputFile, List<RedumpEntry> entries, Dictionary<string, string> extraUrls)
{
    var db = new Dictionary<string, object>();
    foreach (var entry in entries)
    {
        string coverUrl = extraUrls.TryGetValue(entry.GameId, out string? extraUrl) ? extraUrl : $"{CoverArtBaseUrl}{entry.GameId}.jpg";
        db[entry.GameId] = new
        {
            name = entry.Title,
            discNumber = entry.DiscNumber,
            coverUrl
        };
    }

    string json = JsonSerializer.Serialize(db, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(outputFile, json);
    Console.WriteLine($"✔️ {outputFile} generado con {db.Count} entradas.");
}

static void GenerateIndividualFiles(List<RedumpEntry> entries, Dictionary<string, string> extraUrls, string outputDir, string console, Dictionary<string, object> index)
{
    foreach (var entry in entries)
    {
        string coverUrl = extraUrls.TryGetValue(entry.GameId, out string? extraUrl) ? extraUrl : $"{CoverArtBaseUrl}{entry.GameId}.jpg";
        var gameData = new
        {
            name = entry.Title,
            discNumber = entry.DiscNumber,
            coverUrl,
            console
        };

        string fileName = $"{entry.GameId}.json";
        string filePath = Path.Combine(outputDir, fileName);
        File.WriteAllText(filePath, JsonSerializer.Serialize(gameData, new JsonSerializerOptions { WriteIndented = true }));

        index[entry.GameId] = new
        {
            name = entry.Title,
            console,
            jsonPath = $"{console}/{fileName}",
            cfgPath = $"cfg/{entry.GameId}.cfg"
        };
    }
    Console.WriteLine($"✔️ Archivos individuales generados en {outputDir} ({entries.Count} juegos)");
}

// ==================================================
// CLASE DE DATOS
// ==================================================
public class RedumpEntry
{
    public string GameId { get; set; } = "";
    public string Title { get; set; } = "";
    public int DiscNumber { get; set; } = 1;
    public string Serial { get; set; } = "";
}