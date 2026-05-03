using POPSManager.DBGenerator.Core.Models;

namespace POPSManager.DBGenerator.Core.Interfaces;

public interface IGameSource
{
    Task<IEnumerable<GameEntry>> GetGamesAsync();
}
