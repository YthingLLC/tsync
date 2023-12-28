using System.Net.Http.Headers;
using System.Text.Json;
using ComposableAsync;
using RateLimiter;

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

   private static String? DownloadPath;
   
   
   //https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-casing
   //https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options?pivots=dotnet-8-0#web-defaults-for-jsonserializeroptions
   private static readonly JsonSerializerOptions JsonDefaultOpts = new(JsonSerializerDefaults.Web);

   //https://developer.atlassian.com/cloud/trello/guides/rest-api/rate-limits/
   //Official rate limit is 100 per 10 seconds, generally.
   //set to 9/sec to allow for "smoother" requests. 90/10secs gets "bursty" traffic
   private static readonly TimeLimiter TrelloApiLimiter = TimeLimiter.GetFromMaxCountByInterval(9, TimeSpan.FromSeconds(1));
   
   public static void SetCredentials(String? apiKey, String? userToken)
   {
       TrelloApiKey = apiKey;
       TrelloUserToken = userToken;
   }

   public static void SetDownloadPath(String? path)
   {
       DownloadPath = path;
   }
   
   public static String GetDownloadFilePath(String filename)
   {
       if (DownloadPath is null)
       {
           return $"./{filename}";
       }
       return $"{DownloadPath}/{filename}";
   }
   //returns the path to where the file was saved on disk
   //file name on disk will not match the filename that was set by trello, it will be a new Guid
   async private static Task<String?> DownloadAttachment(TAttachment attachment)
   {
       await TrelloApiLimiter;
       return null;
   }

   async private static Task<String?> TrelloApiReq(String endpoint, String? param = null)
   {
       await TrelloApiLimiter;
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

   async static Task<TCard> GetCommentsForTCard(TCard card)
   {
       //await TrelloApiLimiter;
       //var comments = new List<TComment>();

       var resp = await TrelloApiReq($"cards/{card.Id}/actions", "filter=commentCard");
       
       Console.Write(".");

       if (resp is null)
       {
           Console.WriteLine($"Error retrieving comments for card {card.Id}");
           //we're returning the original card, because no comments could be retrieved
           return card;
       }
       
       var respComm = Deserialize<List<TComment>>(resp);
       
       //comments.Add(new TComment("123", new TMember("123", "456", "789"), new TCommentData("Test Comment. Please Ignore")));

       if (respComm is null)
       {
           Console.WriteLine("Error: Internal application error, unable to deserialize response from Trello");
           //same here, can't get comments, so returning the original card
           return card;
       }

       //return new TCard(card, comments);
       return new TCard(card, respComm);
   }

   public async static Task<List<TCard>?> GetCardsForBoard(String boardId)
   {

       var resp = await TrelloApiReq($"boards/{boardId}/cards/all", "attachments=true&checklists=all");

       if (resp is null)
       {
           Console.WriteLine($"Error: Unable to retrieve cards for board {boardId}");
           return null;
       }

       var respCards = Deserialize<List<TCard>>(resp);

       var ret = new List<TCard>();

       var commentTasks = new List<Task<TCard>>();

       Console.WriteLine($"Getting Comments for cards on board {boardId}");
       
       foreach (var c in respCards)
       {
           commentTasks.Add(GetCommentsForTCard(c));
       }

       foreach (var t in commentTasks)
       {
           ret.Add(await t);
       }

       return ret;

   }

   static TBoard ReassembleTBoardWithCardsDictionary(TBoard input, Dictionary<String, List<TCard>> cardsDict)
   {
       var tlists = new List<TList>();
       foreach (var kvp in cardsDict)
       {
           TList? listInfo = input.Lists.Find(l => l.Id == kvp.Key);

           if (listInfo is null)
           {
               Console.WriteLine($"Error: Internal - Unknown List {kvp.Key}");
               continue;
           }
           
           tlists.Add(new TList(listInfo.Value.Id, listInfo.Value.Name, listInfo.Value.Closed, kvp.Value));
       }

       return new TBoard(input.Id, input.Name, tlists);
   }
   
   
   async public static Task<List<TBoard>> GetCardsForBoardList(List<TBoard> boards)
   {
       var ret = new List<TBoard>();
       Int32 totalCards = 0;
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

           Int32 attachmentCount = 0, commentCount = 0, checkListcount = 0;
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
                   Console.WriteLine($"Error: Unable to retrieve list {c.IdList} to store card! Internal Application Error!");
                   continue;
               }
               
               
               list.Add(c);
           }

           ret.Add(ReassembleTBoardWithCardsDictionary(b, cardDict));

           totalCards += cards.Count;
           Console.WriteLine($"Got all cards ({cards.Count}) for {b.Id} - {b.Name}");
       }

       Console.WriteLine($"Complete: Got All ({totalCards}) Cards");

       FileStream fs = new FileStream(GetDownloadFilePath($"data-export-{DateTime.UtcNow:yyyyMMdd.HHmmss.fff}.json"), FileMode.Create);
       
       var serial = JsonSerializer.Serialize(ret);

       StreamWriter sw = new StreamWriter(fs);
       
       sw.Write(serial);
       
       sw.Close();
       
       fs.Close();
       
       return ret;
   }
}