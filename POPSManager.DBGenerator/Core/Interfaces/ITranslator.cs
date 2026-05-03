namespace POPSManager.DBGenerator.Core.Interfaces;

public interface ITranslator
{
    Task<string?> TranslateAsync(string text, string sourceLang, string targetLang);
}
