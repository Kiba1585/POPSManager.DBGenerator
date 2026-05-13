namespace POPSManager.DBGenerator.Core.Interfaces;

public interface ITranslator
{
    // Nuevo método para identificar el traductor en logs
    string Name { get; }
    Task<string?> TranslateAsync(string text, string sourceLang, string targetLang);
}