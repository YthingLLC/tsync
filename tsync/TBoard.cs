namespace tsync;

public struct TBoard
{
    public string Id { get; init; }
    public string Name { get; init; }
    public List<TList> Lists { get; init; }

    public TBoard(string id, string name, List<TList> lists)
    {
        Id = id;
        Name = name;
        Lists = lists;
    }
}