namespace Overflow.EstimationService.Models;

public class DeckDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] Values { get; set; } = [];
}

public static class Decks
{
    public static readonly DeckDefinition Fibonacci = new()
    {
        Id = "fibonacci",
        Name = "Fibonacci",
        Values = ["0", "1", "2", "3", "5", "8", "13", "21", "34", "55", "89", "?", "☕"]
    };

    public static readonly DeckDefinition TShirts = new()
    {
        Id = "tshirts",
        Name = "T-Shirts",
        Values = ["XS", "S", "M", "L", "XL", "XXL", "?", "☕"]
    };

    public static readonly Dictionary<string, DeckDefinition> All = new(StringComparer.OrdinalIgnoreCase)
    {
        [Fibonacci.Id] = Fibonacci,
        [TShirts.Id] = TShirts
    };

    public static DeckDefinition GetOrDefault(string? deckType) =>
        deckType is not null && All.TryGetValue(deckType, out var deck) ? deck : Fibonacci;
}