namespace tsync;

public struct TList
{
   public String Id { get; init; } 
   public String Name { get; init; }
   public Boolean Closed { get; init; }

   public List<TCard> Cards { get; init; }
   
   public TList(String id, String name, Boolean closed, List<TCard> cards)
   {
      Id = id;
      Name = name;
      Closed = closed;
      Cards = cards;
   }
}