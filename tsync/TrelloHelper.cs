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

   

   async public static Task GetAllOrgs()
   {
       var resp = await TrelloApiReq("members/me/organizations");

       if (resp is null)
       {
           return;
       }

       //Console.WriteLine(resp);
       
       var orgs = Deserialize<List<TOrg>>(resp);

       if (orgs is null || orgs.Count == 0)
       {
           Console.WriteLine("Error: Unable to retrieve orgs from Trello");
           return;
       }

       Console.WriteLine($"Total Orgs: {orgs.Count}");
       
       foreach (var org in orgs)
       {
           Console.WriteLine($"{org.Id} - {org.DisplayName}");
           Console.WriteLine($"{org.DomainName} - {org.MembersCount}");

           foreach (var board in org.IdBoards)
           {
               Console.WriteLine($"----{board}");
           }
       }

   }
}