using POPSManager.DBGenerator.Core.Interfaces;

namespace POPSManager.DBGenerator.Infrastructure.Translators;

/// <summary>
/// Intenta varios traductores en orden. Si uno falla, pasa al siguiente.
/// </summary>
public class FallbackTranslator : ITranslator
{
    private readonly IEnumerable<ITranslator> _translators;
    public string Name => "Fallback";

    public FallbackTranslator(params ITranslator[] translators)
    {
        _translators = translators;
    }

    public async Task<string?> TranslateAsync(string text, string sourceLang, string targetLang)
    {
        foreach (var translator in _translators)
        {
            var result = await translator.TranslateAsync(text, sourceLang, targetLang);
            if (!string.IsNullOrEmpty(result) && result != text)
            {
                Console.WriteLine($"     ✓ Traducido por {translator.Name}");
                return result;
            }
        }
        return null;
    }
}