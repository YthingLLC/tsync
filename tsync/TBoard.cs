namespace tsync;

public struct TBoard
{
    public String Id { get; init; }
    public String Name { get; init; }
    public List<TList> Lists { get; init; }

    public TBoard(String id, String name, List<TList> lists)
    {
        Id = id;
        Name = name;
        Lists = lists;
    }
    
}