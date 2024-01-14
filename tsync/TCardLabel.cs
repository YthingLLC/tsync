namespace tsync;

public struct TCardLabel
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Color { get; init; }

    public TCardLabel(string id, string name, string color)
    {
        Id = id;
        Name = name;
        Color = color;
    }
}