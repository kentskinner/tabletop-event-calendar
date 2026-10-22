using EventCalendar.Models;

namespace EventCalendar.Services;

public sealed class GameTemplateCatalog
{
    private static readonly IReadOnlyList<GameTemplate> Templates =
    [
        new("magic", "Magic: The Gathering", ["Standard", "Draft", "Commander"], 180, 16, 30, 4),
        new("pokemon", "Pokémon Trading Card Game", ["Standard", "Expanded", "Prerelease"], 150, 16, 30, 4),
        new("lorcana", "Disney Lorcana", ["Core Constructed", "Booster Draft", "Sealed"], 150, 12, 24, 4)
    ];

    public IReadOnlyList<GameTemplate> All => Templates;
    public GameTemplate? Find(string id) => Templates.SingleOrDefault(x => x.Id == id);
}
