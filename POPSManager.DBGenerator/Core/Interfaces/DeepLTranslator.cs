using System.Text.Json;
using POPSManager.DBGenerator.Core.Interfaces;

namespace POPSManager.DBGenerator.Infrastructure.Translators;

public class DeepLTranslator : ITranslator
{
    private readonly HttpClient _httpClient = new();
    private readonly string _apiKey;
    public string Name => "DeepL";

    public DeepLTranslator(string apiKey)
    {
        _apiKey = apiKey;
    }

    public async Task<string?> TranslateAsync(string text, string sourceLang, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Mapear códigos de idioma al formato de DeepL (EN, ES, FR, DE, IT, JA)
        var langMap = new Dictionary<string, string> {
            {"en", "EN"}, {"es", "ES"}, {"fr", "FR"}, {"de", "DE"}, {"it", "IT"}, {"ja", "JA"}
        };
        string? deepLTarget = langMap.TryGetValue(targetLang, out var dlTarget) ? dlTarget : targetLang.ToUpper();

        var content = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("text", text),
            new KeyValuePair<string, string>("target_lang", deepLTarget)
        });

        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("DeepL-Auth-Key", _apiKey);

        try
        {
            var response = await _httpClient.PostAsync("https://api-free.deepl.com/v2/translate", content);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(json);
                var translations = doc.RootElement.GetProperty("translations");
                if (translations.GetArrayLength() > 0)
                {
                    return translations[0].GetProperty("text").GetString();
                }
            }
        }
        catch { }
        return null;
    }
}