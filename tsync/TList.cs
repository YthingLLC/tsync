namespace tsync;

public struct TList
{
    public string Id { get; init; }
    public string Name { get; init; }
    public bool Closed { get; init; }

    public List<TCard> Cards { get; init; }

    public TList(string id, string name, bool closed, List<TCard> cards)
    {
        Id = id;
        Name = name;
        Closed = closed;
        Cards = cards;
    }
}