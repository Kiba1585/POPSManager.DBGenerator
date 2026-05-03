using System.Text.Json;
using POPSManager.DBGenerator.Core.Interfaces;

namespace POPSManager.DBGenerator.Infrastructure.Translators;

public class MyMemoryTranslator : ITranslator
{
    private readonly HttpClient _httpClient = new();

    public async Task<string?> TranslateAsync(string text, string sourceLang, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string truncatedText = text.Length > 400 ? text[..400] : text;
        string url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(truncatedText)}&langpair={sourceLang}|{targetLang}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("responseData", out var responseData) &&
                responseData.TryGetProperty("translatedText", out var translatedText))
            {
                return translatedText.GetString();
            }
        }
        catch
        {
            // Silencioso
        }
        return null;
    }
}
