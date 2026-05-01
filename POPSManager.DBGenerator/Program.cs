using System.IO.Compression;
using System.Text.Json;
using System.Xml;

const string dataPath = "Data";
const string outputPath = "Output";
const string cfgSourceDir = Path.Combine(dataPath, "cfg_database");
const string ps1DatFile = Path.Combine(dataPath, "psx.dat");
const string ps2DatFile = Path.Combine(dataPath, "ps2.dat");
const string coverArtBaseUrl = "ART/";

Directory.CreateDirectory(outputPath);
Directory.CreateDirectory(Path.Combine(outputPath, "CFG"));

List<RedumpEntry> ps1Entries = ParseRedumpDat(ps1DatFile);
List<RedumpEntry> ps2Entries = ParseRedumpDat(ps2DatFile);

GenerateDatabaseJson(Path.Combine(outputPath, "ps1db.json"), ps1Entries);
GenerateDatabaseJson(Path.Combine(outputPath, "ps2db.json"), ps2Entries);

if (Directory.Exists(cfgSourceDir))
{
    foreach (string cfgFile in Directory.GetFiles(cfgSourceDir, "*.cfg"))
    {
        string destFile = Path.Combine(outputPath, "CFG", Path.GetFileName(cfgFile));
        File.Copy(cfgFile, destFile, overwrite: true);
    }
}
else
{
    Console.WriteLine("⚠️ Carpeta de CFG no encontrada, se omite la copia.");
}

string zipPath = "POPSManager_DB.zip";
if (File.Exists(zipPath)) File.Delete(zipPath);
ZipFile.CreateFromDirectory(outputPath, zipPath);
Console.WriteLine($"✅ Base de datos generada en {zipPath}");

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
        string title = gameNode.SelectSingleNode("description")?.InnerText ?? id;
        string serial = gameNode.SelectSingleNode("serial")?.InnerText ?? "";
        string gameId = serial.Split(' ')[0];
        if (string.IsNullOrWhiteSpace(gameId)) gameId = id;

        entries.Add(new RedumpEntry { GameId = gameId, Title = title });
    }

    return entries;
}

static void GenerateDatabaseJson(string outputFile, List<RedumpEntry> entries)
{
    var db = new Dictionary<string, object>();
    foreach (var entry in entries)
    {
        string coverUrl = $"{coverArtBaseUrl}{entry.GameId}.jpg";
        db[entry.GameId] = new
        {
            name = entry.Title,
            discNumber = 1,
            coverUrl
        };
    }

    string json = JsonSerializer.Serialize(db, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(outputFile, json);
    Console.WriteLine($"✔️ {outputFile} generado con {db.Count} entradas.");
}

public class RedumpEntry
{
    public string GameId { get; set; } = "";
    public string Title { get; set; } = "";
}
