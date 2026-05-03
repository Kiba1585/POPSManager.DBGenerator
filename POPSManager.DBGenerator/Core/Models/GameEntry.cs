namespace POPSManager.DBGenerator.Core.Models;

public class GameEntry
{
    public string GameId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? TranslatedTitle { get; set; }
    public int DiscNumber { get; set; } = 1;
    public string Console { get; set; } = "";
    public string? CoverUrl { get; set; }
    
    // Metadatos del CFG
    public string? Genre { get; set; }
    public string? Players { get; set; }
    public string? Description { get; set; }
    public string? Developer { get; set; }
    public string? ReleaseDate { get; set; }
    
    // Traducciones multi-idioma
    public Dictionary<string, string> TranslatedTitles { get; set; } = new();
    public Dictionary<string, string> TranslatedDescriptions { get; set; } = new();
    public Dictionary<string, string> TranslatedGenres { get; set; } = new();
}
