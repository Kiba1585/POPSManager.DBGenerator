using System.Text.RegularExpressions;
using System.Xml;
using POPSManager.DBGenerator.Core.Interfaces;
using POPSManager.DBGenerator.Core.Models;

namespace POPSManager.DBGenerator.Infrastructure.Parsers;

public class RedumpParser : IGameSource
{
    private readonly string _datFilePath;
    private readonly string _console;

    public RedumpParser(string datFilePath, string console)
    {
        _datFilePath = datFilePath;
        _console = console;
    }

    public Task<IEnumerable<GameEntry>> GetGamesAsync()
    {
        if (!File.Exists(_datFilePath))
        {
            Console.WriteLine($"❌ No se encontró {_datFilePath}");
            return Task.FromResult(Enumerable.Empty<GameEntry>());
        }

        var doc = new XmlDocument();
        doc.Load(_datFilePath);

        var entries = new List<GameEntry>();
        foreach (XmlNode gameNode in doc.SelectNodes("//game")!)
        {
            string id = gameNode.Attributes?["name"]?.Value ?? "";
            string rawTitle = gameNode.SelectSingleNode("description")?.InnerText ?? id;

            int discNumber = 1;
            string cleanTitle = rawTitle;

            var discMatch = Regex.Match(rawTitle, @"\(Disc\s*(\d+)(?:\s*of\s*\d+)?\)", RegexOptions.IgnoreCase);
            if (discMatch.Success)
            {
                discNumber = int.Parse(discMatch.Groups[1].Value);
                cleanTitle = Regex.Replace(rawTitle, @"\s*\(Disc\s*\d+(?:\s*of\s*\d+)?\)\s*", "", RegexOptions.IgnoreCase).Trim();
            }

            string serial = gameNode.SelectSingleNode("serial")?.InnerText ?? "";
            string gameId = serial.Split(' ')[0].Trim();
            if (string.IsNullOrWhiteSpace(gameId)) gameId = id;

            entries.Add(new GameEntry
            {
                GameId = gameId,
                Title = string.IsNullOrWhiteSpace(cleanTitle) ? rawTitle : cleanTitle,
                DiscNumber = discNumber,
                Console = _console
            });
        }

        return Task.FromResult(entries.AsEnumerable());
    }
}