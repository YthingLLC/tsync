namespace tsync;

public struct TCheckItem
{
    public String Id { get; init; }
    public String Name { get; init; }
    public String State { get; init; }
    public Boolean Checked => State.Equals("complete", StringComparison.InvariantCulture);

    public TCheckItem(String id, String name, String state)
    {
        Id = id;
        Name = name;
        State = state;
    }
    
}


public struct TCheckList
{
    public String Id { get; init; }
    public String Name { get; init; }
    public List<TCheckItem> CheckItems { get; init; }

    public TCheckList(String id, String name, List<TCheckItem> checkItems)
    {
        Id = id;
        Name = name;
        CheckItems = checkItems;
    }
    
    //MS Planner does not support multiple independent checklists on a task
    //This "flattens" them so that they still look similar in MS Planner
    public static List<TCheckItem> FlattenCheckLists(List<TCheckList> checkLists)
    {
        if (checkLists.Count == 1)
        {
            return checkLists[0].CheckItems;
        }
        var ret = new List<TCheckItem>();
    
        //yes I know LINQ can make this a single line, but this is more readable to me
        foreach (var cl in checkLists)
        {
            foreach (var citem in cl.CheckItems)
            {
                ret.Add(new TCheckItem(citem.Id, $"{cl.Name} - {citem.Name}", citem.State));
            } 
        }
        
        return ret;
    }
}

//public class TFlatList
//{
//    //MS Planner does not support multiple independent checklists on a task
//    //This "flattens" them so that they still look similar in MS Planner
//    public static List<TCheckItem> FlattenCheckLists(List<TCheckList> checkLists)
//    {
//        if (checkLists.Count == 1)
//        {
//            return checkLists[0].CheckItems;
//        }
//        var ret = new List<TCheckItem>();
//
//        foreach (var cl in checkLists)
//        {
//            foreach (var citem in cl.CheckItems)
//            {
//                ret.Add(new TCheckItem(citem.Id, $"{cl.Name} - {citem.Name}", citem.State));
//            } 
//        }
//        
//        return ret;
//    }
//}