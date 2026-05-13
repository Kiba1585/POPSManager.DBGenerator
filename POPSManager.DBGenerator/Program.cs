using POPSManager.DBGenerator.Application.Builders;
using POPSManager.DBGenerator.Application.Pipeline;
using POPSManager.DBGenerator.Infrastructure.Parsers;
using POPSManager.DBGenerator.Infrastructure.Providers;
using POPSManager.DBGenerator.Infrastructure.Translators;

// ============================================================
// CONFIGURACIÓN
// ============================================================
const string dataPath = "Data";
const string outputPath = "Output";
string cfgSourceDir = Path.Combine(dataPath, "cfg_database");
string ps1Dat = Path.Combine(dataPath, "psx.dat");
string ps2Dat = Path.Combine(dataPath, "ps2.dat");
string extraUrlsFile = Path.Combine(dataPath, "extra_urls.json");
string translationCacheFile = Path.Combine(dataPath, "translation_cache.json");

// Clave de DeepL (gratuita, 500 000 caracteres/mes)
// Se obtiene de variable de entorno para no exponerla en el código.
// En GitHub Actions se configura como secreto DEEPL_API_KEY.
string deeplApiKey = Environment.GetEnvironmentVariable("DEEPL_API_KEY") ?? "";

// Idiomas a los que traducir (códigos ISO 639‑1)
var targetLanguages = new[] { "es", "fr", "de", "it", "ja" };

// ============================================================
// FUENTES DE JUEGOS
// ============================================================
var gameSources = new List<POPSManager.DBGenerator.Core.Interfaces.IGameSource>
{
    new RedumpParser(ps1Dat, "ps1"),
    new RedumpParser(ps2Dat, "ps2")
};

// ============================================================
// PROVEEDORES DE CARÁTULAS (orden de prioridad)
// ============================================================
var coverProvider = new FallbackCoverProvider(
    new ExtraUrlsCoverProvider(extraUrlsFile),
    new OplCoverProvider()
);

// ============================================================
// TRADUCTORES EN CASCADA
// ============================================================
var translators = new List<POPSManager.DBGenerator.Core.Interfaces.ITranslator>();

// Si hay clave de DeepL, lo usamos como primera opción
if (!string.IsNullOrEmpty(deeplApiKey))
{
    translators.Add(new DeepLTranslator(deeplApiKey));
    Console.WriteLine("🌐 DeepL configurado como traductor principal.");
}
else
{
    Console.WriteLine("⚠️ DeepL no configurado (falta DEEPL_API_KEY).");
}

// MyMemory siempre como respaldo (sin clave)
translators.Add(new MyMemoryTranslator());

var fallbackTranslator = new FallbackTranslator(translators.ToArray());

// ============================================================
// PIPELINE
// ============================================================
var pipeline = new DatabaseGenerationPipeline(
    gameSources,
    coverProvider,
    fallbackTranslator,
    translationCacheFile,
    cfgSourceDir,
    targetLanguages);   // ← se inyectan los idiomas

var games = await pipeline.BuildAsync();

// ============================================================
// BUILDERS
// ============================================================
var builder = new OutputBuilder(outputPath, cfgSourceDir);
builder.GenerateAll(games);

Console.WriteLine("✅ Base de datos generada con éxito.");