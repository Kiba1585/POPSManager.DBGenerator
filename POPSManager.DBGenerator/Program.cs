using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

// ============================================================
// Puedes dejar estas rutas como constantes o leerlas de args
// ============================================================
const string dataPath = "Data";
const string outputPath = "Output";
static readonly string CfgSourceDir = Path.Combine(dataPath, "cfg_database");
static readonly string Ps1DatFile = Path.Combine(dataPath, "psx.dat");
static readonly string Ps2DatFile = Path.Combine(dataPath, "ps2.dat");

// URL base de las carátulas (mirror de OPL Manager)
const string CoverArtBaseUrl = "https://archive.org/download/oplm-art-2023-11/ART/";

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
// 3. Generar los archivos JSON mejorados
// --------------------------------------------------
GenerateDatabaseJson(Path.Combine(outputPath, "ps1db.json"), ps1Entries);
GenerateDatabaseJson(Path.Combine(outputPath, "ps2db.json"), ps2Entries);

// --------------------------------------------------
// 4. Copiar archivos .cfg
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
// 5. Empaquetar en ZIP
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

        // Extraer número de disco si el título contiene "(Disc X)" o "(Disc X of Y)"
        int discNumber = 1;
        string cleanTitle = rawTitle;

        var discMatch = Regex.Match(rawTitle, @"\(Disc\s*(\d+)(?:\s*of\s*\d+)?\)", RegexOptions.IgnoreCase);
        if (discMatch.Success)
        {
            discNumber = int.Parse(discMatch.Groups[1].Value);
            // Eliminar la parte "(Disc X)" del título
            cleanTitle = Regex.Replace(rawTitle, @"\s*\(Disc\s*\d+(?:\s*of\s*\d+)?\)\s*", "", RegexOptions.IgnoreCase).Trim();
        }

        // El Game ID se toma del campo <serial>, si no existe usamos el atributo "id"
        string serial = gameNode.SelectSingleNode("serial")?.InnerText ?? "";
        string gameId = serial.Split(' ')[0].Trim();
        if (string.IsNullOrWhiteSpace(gameId)) gameId = id;

        entries.Add(new RedumpEntry
        {
            GameId = gameId,
            Title = string.IsNullOrWhiteSpace(cleanTitle) ? rawTitle : cleanTitle,
            DiscNumber = discNumber
        });
    }

    return entries;
}

/// <summary>
/// Genera el archivo JSON con las entradas de juegos.
/// </summary>
static void GenerateDatabaseJson(string outputFile, List<RedumpEntry> entries)
{
    var db = new Dictionary<string, object>();
    foreach (var entry in entries)
    {
        string coverUrl = $"{CoverArtBaseUrl}{entry.GameId}.jpg";
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
// CLASE DE DATOS
// ==================================================
public class RedumpEntry
{
    public string GameId { get; set; } = "";
    public string Title { get; set; } = "";
    public int DiscNumber { get; set; } = 1;
}