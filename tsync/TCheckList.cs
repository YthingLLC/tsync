/*
Copyright 2024 Ything LLC

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/


namespace tsync;

public struct TCheckItem
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string State { get; init; }
    public bool Checked => State.Equals("complete", StringComparison.InvariantCulture);

    public TCheckItem(string id, string name, string state)
    {
        Id = id;
        Name = name;
        State = state;
    }
}

public struct TCheckList
{
    public string Id { get; init; }
    public string Name { get; init; }
    public List<TCheckItem> CheckItems { get; init; }

    public TCheckList(string id, string name, List<TCheckItem> checkItems)
    {
        Id = id;
        Name = name;
        CheckItems = checkItems;
    }

    //MS Planner does not support multiple independent checklists on a task
    //This "flattens" them so that they still look similar in MS Planner
    public static List<TCheckItem> FlattenCheckLists(List<TCheckList> checkLists)
    {
        if (checkLists.Count == 1) return checkLists[0].CheckItems;
        var ret = new List<TCheckItem>();

        //yes I know LINQ can make this a single line, but this is more readable to me
        foreach (var cl in checkLists)
        foreach (var citem in cl.CheckItems)
            ret.Add(new TCheckItem(citem.Id, $"{cl.Name} - {citem.Name}", citem.State));

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