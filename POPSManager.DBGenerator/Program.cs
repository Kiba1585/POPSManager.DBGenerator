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

// Archivo opcional con URLs alternativas (GameFAQs, etc.)
static readonly string ExtraUrlsFile = Path.Combine(dataPath, "extra_urls.json");

// --------------------------------------------------
// 1. Preparar directorios de salida
// --------------------------------------------------
Directory.CreateDirectory(outputPath);
Directory.CreateDirectory(Path.Combine(outputPath, "CFG"));

// --------------------------------------------------
// 2. Parsear datfiles de Redump
// --------------------------------------------------
List<RedumpEntry> ps1Entries = ParseRedumpDat(Ps1DatFile);
List<RedumpEntry> ps2Entries = ParseRedumpDat(Ps2DatFile);

// --------------------------------------------------
// 3. Cargar URLs extra si existen
// --------------------------------------------------
Dictionary<string, string> extraUrls = LoadExtraUrls(ExtraUrlsFile);

// --------------------------------------------------
// 4. Ajustar discos múltiples y URLs finales
// --------------------------------------------------
ps1Entries = AdjustDiscNumbers(ps1Entries);
ps2Entries = AdjustDiscNumbers(ps2Entries);

// --------------------------------------------------
// 5. Generar los archivos JSON mejorados
// --------------------------------------------------
GenerateDatabaseJson(Path.Combine(outputPath, "ps1db.json"), ps1Entries, extraUrls);
GenerateDatabaseJson(Path.Combine(outputPath, "ps2db.json"), ps2Entries, extraUrls);

// --------------------------------------------------
// 6. Copiar archivos .cfg
// --------------------------------------------------
if (Directory.Exists(CfgSourceDir))
{
    foreach (string cfgFile in Directory.GetFiles(CfgSourceDir, "*.cfg"))
    {
        string destFile = Path.Combine(outputPath, "CFG", Path.GetFileName(cfgFile));
        File.Copy(cfgFile, destFile, overwrite: true);
    }
}
else
{
    Console.WriteLine("⚠️ Carpeta de CFG no encontrada, se omite la copia.");
}

// --------------------------------------------------
// 7. Empaquetar en ZIP
// --------------------------------------------------
string zipPath = "POPSManager_DB.zip";
if (File.Exists(zipPath)) File.Delete(zipPath);
ZipFile.CreateFromDirectory(outputPath, zipPath);
Console.WriteLine($"✅ Base de datos generada en {zipPath}");

// ==================================================
// MÉTODOS AUXILIARES
// ==================================================

/// <summary>
/// Parsea un datfile de Redump y extrae GameId, título y número de disco.
/// </summary>
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

        // Número de disco desde "(Disc X)"
        int discNumber = 1;
        string cleanTitle = rawTitle;

        var discMatch = Regex.Match(rawTitle, @"\(Disc\s*(\d+)(?:\s*of\s*\d+)?\)", RegexOptions.IgnoreCase);
        if (discMatch.Success)
        {
            discNumber = int.Parse(discMatch.Groups[1].Value);
            cleanTitle = Regex.Replace(rawTitle, @"\s*\(Disc\s*\d+(?:\s*of\s*\d+)?\)\s*", "", RegexOptions.IgnoreCase).Trim();
        }

        // Game ID desde <serial>
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

/// <summary>
/// Ajusta discos múltiples agrupando por título base y seriales consecutivos.
/// </summary>
static List<RedumpEntry> AdjustDiscNumbers(List<RedumpEntry> entries)
{
    // Agrupar por título normalizado (sin versión, sin región si se desea)
    var groups = entries
        .Where(e => !string.IsNullOrEmpty(e.Title))
        .GroupBy(e => NormalizeTitleForGrouping(e.Title))
        .Where(g => g.Count() > 1);

    foreach (var group in groups)
    {
        // Ordenar por Game ID o por serial para asignar discNumber secuencial
        var sorted = group.OrderBy(e => e.GameId).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            // Solo si aún no tiene discNumber > 1 desde el título
            if (sorted[i].DiscNumber == 1)
                sorted[i].DiscNumber = i + 1;
        }
    }

    return entries;
}

/// <summary>
/// Normaliza el título para agrupar variantes de discos (ignora mayúsculas y ciertas palabras).
/// </summary>
static string NormalizeTitleForGrouping(string title)
{
    if (string.IsNullOrWhiteSpace(title)) return "";
    string norm = title.ToUpperInvariant()
        .Replace("THE ", "")
        .Replace("A ", "")
        .Replace("AN ", "");
    return norm.Trim();
}

/// <summary>
/// Carga URLs de carátulas alternativas desde un JSON { "GAMEID": "url" }.
/// </summary>
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

/// <summary>
/// Genera el archivo JSON con las entradas de juegos, usando URLs extra si están disponibles.
/// </summary>
static void GenerateDatabaseJson(string outputFile, List<RedumpEntry> entries, Dictionary<string, string> extraUrls)
{
    var db = new Dictionary<string, object>();
    foreach (var entry in entries)
    {
        string coverUrl;
        if (extraUrls.TryGetValue(entry.GameId, out string? extraUrl))
        {
            coverUrl = extraUrl;
        }
        else
        {
            coverUrl = $"{CoverArtBaseUrl}{entry.GameId}.jpg";
        }

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

// ==================================================
// CLASE DE DATOS MEJORADA
// ==================================================
public class RedumpEntry
{
    public string GameId { get; set; } = "";
    public string Title { get; set; } = "";
    public int DiscNumber { get; set; } = 1;
    public string Serial { get; set; } = "";
}