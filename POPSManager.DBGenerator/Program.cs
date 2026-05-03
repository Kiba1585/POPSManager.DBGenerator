using POPSManager.DBGenerator.Application.Builders;
using POPSManager.DBGenerator.Application.Pipeline;
using POPSManager.DBGenerator.Infrastructure.Parsers;
using POPSManager.DBGenerator.Infrastructure.Providers;
using POPSManager.DBGenerator.Infrastructure.Translators;

// CONFIGURACIÓN
const string dataPath = "Data";
const string outputPath = "Output";
string cfgSourceDir = Path.Combine(dataPath, "cfg_database");
string ps1Dat = Path.Combine(dataPath, "psx.dat");
string ps2Dat = Path.Combine(dataPath, "ps2.dat");
string extraUrlsFile = Path.Combine(dataPath, "extra_urls.json");
string translationCacheFile = Path.Combine(dataPath, "translation_cache.json");

// FUENTES DE JUEGOS
var gameSources = new List<POPSManager.DBGenerator.Core.Interfaces.IGameSource>
{
    new RedumpParser(ps1Dat, "ps1"),
    new RedumpParser(ps2Dat, "ps2")
};

// PROVEEDORES DE CARÁTULAS
var coverProvider = new FallbackCoverProvider(
    new ExtraUrlsCoverProvider(extraUrlsFile),
    new OplCoverProvider()
);

// TRADUCTOR
var translator = new MyMemoryTranslator();

// PIPELINE
var pipeline = new DatabaseGenerationPipeline(gameSources, coverProvider, translator, translationCacheFile, cfgSourceDir);
var games = await pipeline.BuildAsync();

// BUILDERS
var builder = new OutputBuilder(outputPath, cfgSourceDir);
builder.GenerateAll(games);

Console.WriteLine("✅ Base de datos generada con éxito.");