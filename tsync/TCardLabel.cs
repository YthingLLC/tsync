namespace tsync;

public struct TCardLabel
{
    public String Id { get; init; }
    public String Name { get; init; }
    public String Color { get; init; }

    public TCardLabel(String id, String name, String color)
    {
        Id = id;
        Name = name;
        Color = color;
    }
}