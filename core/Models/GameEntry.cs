namespace POPSManager.DBGenerator.Core.Models;

public class GameEntry
{
    public string GameId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? TranslatedTitle { get; set; }
    public int DiscNumber { get; set; } = 1;
    public string Console { get; set; } = "";
    public string? CoverUrl { get; set; }
}