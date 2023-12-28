using System.Net.Http.Headers;
using System.Text.Json;

namespace tsync;


public static class TrelloHelper
{
    //having to do this makes me half tempted to do the same thing with Graph...
    //I've done it before, I can do it again... but at least Microsoft's library isn't broken like Manatee.Trello!
   private static HttpClient _trelloClient = new()
   {
       BaseAddress = new Uri("https://api.trello.com/1/"),
       //Microsoft, this is one of the dumbest things I've ever seen,
       //Why can't this be much simpler... I mean... why MediaTyperWithQualityHeaderValue????
       DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") }}
   };

   private static HttpClient _attachmentClient = new()
   {
       //afaik only cards can have attachments
       //at least that's what I'm going with
       BaseAddress = new Uri("https://trello.com/1/cards/"),
   };

   private static String? TrelloApiKey;
   private static String? TrelloUserToken;
   
   
   //https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-casing
   //https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options?pivots=dotnet-8-0#web-defaults-for-jsonserializeroptions
   private static readonly JsonSerializerOptions JsonDefaultOpts = new(JsonSerializerDefaults.Web);
   
   private static String downloadPath = "./attachments/";
   
   public static void SetCredentials(String apiKey, String userToken)
   {
       TrelloApiKey = apiKey;
       TrelloUserToken = userToken;
   }

   //returns the path to where the file was saved on disk
   //file name on disk will not match the filename that was set by trello, it will be a new Guid
   async private static Task<String?> DownloadAttachment(TAttachment attachment)
   {
       return null;
   }

   async private static Task<String?> TrelloApiReq(String endpoint, String? param = null)
   {
       var url = $"{endpoint}?key={TrelloApiKey}&token={TrelloUserToken}";
       if (param is not null)
       {
           url += $"&{param}";
       }


       var resp = await _trelloClient.GetAsync(url);

       if (!resp.IsSuccessStatusCode)
       {
           Console.WriteLine($"Error: {resp.StatusCode} on endpoint: {endpoint}");
           var errMsg = await resp.Content.ReadAsStringAsync();
           Console.WriteLine($"Server Said: {errMsg}");
           return null;
       }

       return await resp.Content.ReadAsStringAsync();

   }

   private static T? Deserialize<T>(String json)
   {
       return JsonSerializer.Deserialize<T>(json, JsonDefaultOpts);
   }

   

   async public static Task<List<TOrg>?> GetAllOrgs()
   {
       var resp = await TrelloApiReq("members/me/organizations");

       if (resp is null)
       {
           return null;
       }

       //Console.WriteLine(resp);
       
       var orgs = Deserialize<List<TOrg>>(resp);

       //                                     yes, this should always be false
       //                                     but jsonserializer.deserialize doesn't agree!
       //                                     this may be null if deserialization quietly fails
       //                  which sets this == 1, because reasons I guess?
       // and makes this not null, just for fun debugging times
       if (orgs is null || orgs.Count == 0 || orgs[0].Id is null)
       {
           Console.WriteLine("Error: Unable to retrieve orgs from Trello");
           return null;
       }

       //Console.WriteLine($"Total Orgs: {orgs.Count}");
       
       //foreach (var org in orgs)
       //{
       //    Console.WriteLine($"{org.Id} - {org.DisplayName}");
       //    Console.WriteLine($"{org.DomainName} - {org.MembersCount}");

       //    foreach (var board in org.IdBoards)
       //    {
       //        Console.WriteLine($"----{board}");
       //    }
       //}

       return orgs;

   }

   async public static Task<List<TBoard>?> GetAllOrgBoards(List<TOrg> orgs)
   {
       var resps = new List<Task<String?>>();
       foreach (var org in orgs)
       {
           //resps.Add(TrelloApiReq($"organizations/{org.Id}/boards"));
           foreach (var board in org.IdBoards)
           {
               resps.Add(TrelloApiReq($"boards/{board}", "lists=all"));
           }
       }

       var ret = new List<TBoard>();
       foreach (var resp in resps)
       {
           var json = await resp;
           if (json is null)
           {
               continue;
           }
           var board = Deserialize<TBoard>(json);
           ret.Add(board);
       }

       return ret;
   }

   public async static Task<List<TCard>?> GetCardsForBoard(String boardId)
   {

       var resp = await TrelloApiReq($"boards/{boardId}/cards/all");

       if (resp is null)
       {
           Console.WriteLine($"Error: Unable to retrieve cards for board {boardId}");
           return null;
       }

       var ret = Deserialize<List<TCard>>(resp);

       return ret;

   }

   static TBoard ReassembleTBoardWithCardsDictionary(TBoard input, Dictionary<String, List<TCard>)
   {
       
   }
   
   async public static Task<List<TBoard>> GetCardsForBoardList(List<TBoard> boards)
   {
       var ret = new List<TBoard>();
       foreach (var b in boards)
       {
           var cardDict = new Dictionary<String, List<TCard>>();

           foreach (var l in b.Lists)
           {
               cardDict.Add(l.Id, new List<TCard>());
           }

           var cards = await GetCardsForBoard(b.Id);

           if (cards is null)
           {
               Console.WriteLine($"INFO: No cards for board {b.Id}");
               continue;
           }
           
           foreach (var c in cards)
           {
               List<TCard>? list;
               if (!cardDict.TryGetValue(c.IdList, out list))
               {
                   cardDict.Add(c.IdList, new List<TCard>());
                   list = cardDict[c.IdList];
               }

               if (list is null)
               {
                   Console.WriteLine("Error: Unable to retrieve list to store card! Internal Application Error!");
               }
               
               
               list.Add(c);
           }

           Console.WriteLine($"{cardDict.Count}");
           
           foreach (var l in b.Lists.ToList())
           {
               Console.WriteLine($"{l.Id}");
               List<TCard>? cardsD;
               
               if (!cardDict.TryGetValue(l.Id, out cardsD))
               {
                   Console.WriteLine($"Error: {l.Id} Unknown List!");
               }

               if (cardsD is null)
               {
                   Console.WriteLine($"INFO: No cards for list {l.Id}");
                   continue;
               }

               l.AddCards(cardsD);
               Console.WriteLine($"{l.Cards.Count}");
           }
           
           ret.Add(b);

           Console.WriteLine($"Got all cards for {b.Id} - {b.Name}");
       }

       
       Console.WriteLine("Got All Cards");

       throw new NotImplementedException();
   }
}