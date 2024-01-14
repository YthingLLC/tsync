using System.Net.Http.Headers;
using System.Text.Json;
using ComposableAsync;
using Microsoft.Kiota.Abstractions.Extensions;
using RateLimiter;

namespace tsync;

public static class TrelloHelper
{
    //having to do this makes me half tempted to do the same thing with Graph...
    //I've done it before, I can do it again... but at least Microsoft's library isn't broken like Manatee.Trello!
    private static readonly HttpClient _trelloClient = new()
    {
        BaseAddress = new Uri("https://api.trello.com/1/"),
        //Microsoft, this is one of the dumbest things I've ever seen,
        //Why can't this be much simpler... I mean... why MediaTyperWithQualityHeaderValue????
        DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
    };

    private static readonly HttpClient _attachmentClient = new()
    {
        //afaik only cards can have attachments
        //at least that's what I'm going with
        BaseAddress = new Uri("https://trello.com/1/cards/")
    };

    private static string? TrelloApiKey;
    private static string? TrelloUserToken;

    private static string? DownloadPath;


    //https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-casing
    //https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options?pivots=dotnet-8-0#web-defaults-for-jsonserializeroptions
    private static readonly JsonSerializerOptions JsonDefaultOpts = new(JsonSerializerDefaults.Web);

    //https://developer.atlassian.com/cloud/trello/guides/rest-api/rate-limits/
    //Official rate limit is 100 per 10 seconds, generally.
    //set to 9/sec to allow for "smoother" requests. 90/10secs gets "bursty" traffic
    private static readonly TimeLimiter TrelloApiLimiter =
        TimeLimiter.GetFromMaxCountByInterval(9, TimeSpan.FromSeconds(1));

    private static string NowFile => $"{DateTime.UtcNow:yyyyMMdd.HHmmss.fff}";

    public static Dictionary<string, FileMeta>? FileMeta { get; private set; }

    public static bool FileMetasLoaded => FileMeta is not null;

    public static void SetCredentials(string? apiKey, string? userToken)
    {
        TrelloApiKey = apiKey;
        TrelloUserToken = userToken;
    }

    public static void SetDownloadPath(string? path)
    {
        DownloadPath = path;
    }

    public static string GetDownloadFilePath(string filename)
    {
        if (DownloadPath is null) return $"./{filename}";
        return $"{DownloadPath}/{filename}";
    }

    private static FileStream? GetFileStreamForOpenFile(string filename, bool relativeFilename = true)
    {
        if (relativeFilename) filename = GetDownloadFilePath(filename);

        try
        {
            var fs = new FileStream(filename, FileMode.Open);
            return fs;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unable to open file: {e.Message}");
            return null;
        }
    }

    //returns the path to where the file was saved on disk
    //file name on disk will not match the filename that was set by trello, it will be a new Guid
    private static FileStream? GetFileStreamForCreateFile(string filename, bool relativeFilename = true)
    {
        if (relativeFilename) filename = GetDownloadFilePath(filename);

        try
        {
            var fs = new FileStream(filename, FileMode.Create);
            return fs;
        }
        catch (Exception e)
        {
            Console.WriteLine(
                $"Unable to open {filename} for writing, check to ensure that this is writable. Actual Exception: {e.Message}");
            return null;
        }
    }

    private static StreamWriter? GetStreamWriterForCreateFile(string filename, bool relativeFilename = true)
    {
        var fs = GetFileStreamForCreateFile(filename, relativeFilename);
        if (fs is null)
        {
            Console.WriteLine("Null stream, unable to write file.");
            return null;
        }

        var sw = new StreamWriter(fs);

        return sw;
    }

    private static void WriteToFileSync(string filename, string contents)
    {
        var sw = GetStreamWriterForCreateFile(filename);

        if (sw is null)
        {
            Console.WriteLine($"Unable to write file {filename}, null streamwriter");
            return;
        }

        sw.Write(contents);
        sw.Flush();
        sw.Close();
    }

    private static async Task WriteToFile(string filename, string contents)
    {
        var sw = GetStreamWriterForCreateFile(filename);
        if (sw is null)
        {
            Console.WriteLine($"Unable to write file {filename}, null streamwriter");
            return;
        }

        await sw.WriteAsync(contents);
        await sw.FlushAsync();
        sw.Close();
    }

    public static async Task WriteToFileAsync(string filename, string contents)
    {
        await WriteToFile(filename, contents);
    }

    private static async Task WriteToFile(string filename, Stream contents)
    {
        var fs = GetFileStreamForCreateFile(filename);
        if (fs is null)
        {
            Console.WriteLine($"Error opening filestream. Can not write to file {filename}");
            return;
        }

        await contents.CopyToAsync(fs);
        fs.Close();
    }

    private static async Task<FileMeta> DownloadAttachment(FileMeta attachment)
    {
        if (!attachment.AttachmentData.IsUpload || attachment.Complete) return attachment;

        await TrelloApiLimiter;

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{attachment.AttachmentData.Url}"));
        request.Headers.Add("Authorization",
            $"OAuth oauth_consumer_key=\"{TrelloApiKey}\", oauth_token=\"{TrelloUserToken}\"");

        var resp = await _attachmentClient.SendAsync(request);

        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine(
                $"Error: Failed to download {attachment.AttachmentData.Id} - [{resp.StatusCode}] Server said: {resp.ReasonPhrase}");
            return attachment;
        }

        var respBody = await resp.Content.ReadAsStreamAsync();

        await WriteToFile($"{attachment.FileID}.file", respBody);

        attachment.Complete = true;

        return attachment;
    }

    public static FileStream? OpenAttachmentAsStream(FileMeta attachment)
    {
        return GetFileStreamForOpenFile($"{attachment.FileID}.file");
    }

    public static async Task DownloadAttachments()
    {
        if (FileMeta is null)
        {
            Console.WriteLine("File metas not loaded, please load or download boards from Trello");
            return;
        }

        foreach (var f in FileMeta)
        {
            Console.WriteLine($"Downloading {f.Key} - {f.Value.AttachmentData.Bytes} bytes");
            var resp = await DownloadAttachment(f.Value);
            if (resp.Complete) UpdateFileStatus(f.Key, true);
        }
    }

    private static async Task<string?> TrelloApiReq(string endpoint, string? param = null)
    {
        await TrelloApiLimiter;
        var url = $"{endpoint}?key={TrelloApiKey}&token={TrelloUserToken}";
        if (param is not null) url += $"&{param}";


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
    //TODO: Move all of this serialization related stuff into a separate class / lib

    private static string Serialize<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonDefaultOpts);
    }

    private static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonDefaultOpts);
    }

    private static async Task<T?> DeserializeFromFile<T>(string filename, bool relativeFilename = true)
    {
        if (relativeFilename) filename = GetDownloadFilePath(filename);

        try
        {
            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);

            var deserial = await JsonSerializer.DeserializeAsync<T>(fs, JsonDefaultOpts);

            fs.Close();

            return deserial;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unable to load file: {e.Message}");
            return default;
        }
    }

    public static async Task<List<FileMeta>?> LoadFileMetasFromFile(string filename, bool relativeFilename = true)
    {
        return await DeserializeFromFile<List<FileMeta>?>(filename, relativeFilename);
    }

    public static async Task<List<TOrg>?> GetAllOrgs()
    {
        var resp = await TrelloApiReq("members/me/organizations");

        if (resp is null) return null;

        //Console.WriteLine(resp);

        var orgs = Deserialize<List<TOrg>>(resp);

        //                                     yes, this should always be false
        //                                     but jsonserializer.deserialize doesn't agree!
        //                                     this may be null if deserialization quietly fails
        //                  which sets this == 1, because reasons I guess?
        // and makes this not null, just for fun debugging times
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
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

    public static async Task<List<TBoard>?> GetAllOrgBoards(List<TOrg> orgs)
    {
        var resps = new List<Task<string?>>();
        foreach (var org in orgs)
            //resps.Add(TrelloApiReq($"organizations/{org.Id}/boards"));
        foreach (var board in org.IdBoards)
            resps.Add(TrelloApiReq($"boards/{board}", "lists=all"));

        var ret = new List<TBoard>();
        foreach (var resp in resps)
        {
            var json = await resp;
            if (json is null) continue;
            var board = Deserialize<TBoard>(json);
            ret.Add(board);
        }

        return ret;
    }

    private static async Task<TCard> GetCommentsForTCard(TCard card)
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

    public static async Task<List<TCard>?> GetCardsForBoard(string boardId)
    {
        var resp = await TrelloApiReq($"boards/{boardId}/cards/all", "attachments=true&checklists=all");

        if (resp is null)
        {
            Console.WriteLine($"Error: Unable to retrieve cards for board {boardId}");
            return null;
        }

        var respCards = Deserialize<List<TCard>>(resp);

        if (respCards is null)
        {
            Console.WriteLine($"Error: Unable to deserialize cards for {boardId}");
            return null;
        }

        var ret = new List<TCard>();

        var commentTasks = new List<Task<TCard>>();

        Console.WriteLine($"Getting Comments for cards on board {boardId}");

        foreach (var c in respCards) commentTasks.Add(GetCommentsForTCard(c));

        foreach (var t in commentTasks) ret.Add(await t);

        return ret;
    }

    private static TBoard ReassembleTBoardWithCardsDictionary(TBoard input, Dictionary<string, List<TCard>> cardsDict)
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

    public static async Task<List<TBoard>> GetCardsForBoardList(List<TBoard> boards)
    {
        var ret = new List<TBoard>();
        var totalCards = 0;
        foreach (var b in boards)
        {
            var cardDict = new Dictionary<string, List<TCard>>();

            foreach (var l in b.Lists) cardDict.Add(l.Id, new List<TCard>());

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
                    //This may look redundant, but this is updating the reference list is pointing to
                    //I want a reference to cardDict's version, as the out version is apparently immutable
                    //but doesn't tell you until runtime!
                    list = cardDict[c.IdList];
                }

                //Yes, ReSharper, this can be null. Do you not see the ? on list above?
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (list is null)
                {
                    Console.WriteLine(
                        $"Error: Unable to retrieve list {c.IdList} to store card! Internal Application Error!");
                    continue;
                }


                list.Add(c);
            }

            ret.Add(ReassembleTBoardWithCardsDictionary(b, cardDict));

            totalCards += cards.Count;
            Console.WriteLine($"Got all cards ({cards.Count}) for {b.Id} - {b.Name}");
        }

        Console.WriteLine($"Complete: Got All ({totalCards}) Cards");

        return ret;
    }

    public static async Task SaveBoardsToFile(List<TBoard> boards)
    {
        var serial = Serialize(boards);

        Console.WriteLine("Exporting data to json file...");

        await WriteToFile($"data-export-{NowFile}.json", serial);
        await WriteToFile("data-export-latest.json", serial);

        Console.WriteLine("Export completed.");
    }

    public static async Task<List<TBoard>?> LoadBoardsFromFile(string filename, bool relativeFilename = true)
    {
        return await DeserializeFromFile<List<TBoard>?>(filename, relativeFilename);
    }

    public static void PrintBoardStatistics(List<TBoard>? boards)
    {
        if (boards is null)
        {
            Console.WriteLine("Boards are not loaded!");
            return;
        }

        long totalAttachments = 0,
            totalAttachmentsCheck = 0,
            totalAttachmentsSize = 0,
            totalComments = 0,
            totalCommentsCheck = 0,
            totalCommentsLength = 0,
            totalCards = 0,
            totalCardsCheck = 0,
            totalLists = 0,
            totalListsCheck = 0,
            totalBoards = 0;

        Console.WriteLine("-----BOARD STATISTICS SUMMARY-----");

        foreach (var b in boards)
        {
            totalBoards++;

            Console.WriteLine($"Board {b.Id} - {b.Name}; Lists: {b.Lists.Count}");
            totalLists += b.Lists.Count;

            int cardAttachments = 0, cardComments = 0, cardCount = 0;

            foreach (var l in b.Lists)
            {
                totalListsCheck++;

                totalCards += l.Cards.Count;

                foreach (var c in l.Cards)
                {
                    totalCardsCheck++;
                    cardCount++;

                    //Console.WriteLine($"----Card: {c.Id}");
                    //Console.WriteLine($"+++++----Comments: {c.Comments.Count}");
                    totalComments += c.Comments.Count;

                    foreach (var comm in c.Comments)
                    {
                        totalCommentsCheck++;
                        cardComments++;
                        totalCommentsLength += comm.Data.Text.Length;
                    }

                    //Console.WriteLine($"++++----Attachments: {c.Attachments.Count}");
                    totalAttachments += c.Attachments.Count;

                    foreach (var attch in c.Attachments)
                    {
                        totalAttachmentsCheck++;
                        cardAttachments++;

                        if (attch.Bytes is not null) totalAttachmentsSize += attch.Bytes.Value;
                    }
                }
            }

            Console.WriteLine($"----Cards: {cardCount}");
            Console.WriteLine($"----Comments: {cardComments}");
            Console.WriteLine($"----Attachments: {cardAttachments}");
        }


        Console.WriteLine("Summary Statistics Check:");
        Console.WriteLine($"       Boards: {totalBoards} == Reported Boards {boards.Count}");
        Console.WriteLine($"        Lists: {totalListsCheck} == {totalLists}");
        Console.WriteLine($"        Cards: {totalCardsCheck} == {totalCards}");
        Console.WriteLine($"     Comments: {totalCommentsCheck} == {totalComments}");
        Console.WriteLine($"Comment Chars: {totalCommentsLength} (from app, total lengths all comments)");
        Console.WriteLine($"  Attachments: {totalAttachmentsCheck} == {totalAttachments}");
        var gigasize = Convert.ToDouble(totalAttachmentsSize) / 1024 / 1024 / 1024;
        Console.WriteLine(
            $" Total Size: {totalAttachmentsSize} bytes ~~ {gigasize} gigabytes (from API, actual may be different)");
    }

    public static void RenderFileMeta(List<TBoard> boards)
    {
        var ret = new Dictionary<string, FileMeta>();
        foreach (var b in boards)
        foreach (var l in b.Lists)
        foreach (var c in l.Cards)
        foreach (var a in c.Attachments)
        {
            var meta = new FileMeta(a, b.Id);
            ret.Add(a.Id, meta);
        }

        FileMeta = ret;
    }

    private static void UpdateFileStatus(string fileId, bool complete, string? hash = null)
    {
        if (FileMeta is null)
        {
            Console.WriteLine(
                "Unable to update file status, file meta has not yet been rendered. Load state, or download latest boards");
            return;
        }

        lock (FileMeta)
        {
            FileMeta value;
            if (FileMeta.TryGetValue(fileId, out value))
            {
                value.Complete = complete;
                value.Hash = hash;
                FileMeta.AddOrReplace(fileId, value);
                SaveFileMetaSync();
            }
        }
    }

    private static bool CheckDirExists(string path)
    {
        if (Directory.Exists(path)) return true;

        try
        {
            //rumors on the internet say that this may leave dangling file handles open, hence why check for existence
            //is done first, even though documentation says that this isn't strictly required
            Directory.CreateDirectory(path);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(
                $"Unable to create directory, ensure that this location is writable: {path}. Exception: {e.Message}");
            return false;
        }
    }

    private static void SaveFileMetaSync()
    {
        if (FileMeta is null)
        {
            Console.WriteLine("Error: File Metas not loaded or rendered.");
            return;
        }

        var serial = Serialize(FileMeta);

        var metaPath = GetDownloadFilePath("filemeta");

        if (!CheckDirExists(metaPath)) return;

        WriteToFileSync($"filemeta/file-metadata-{NowFile}.json", serial);
        WriteToFileSync("filemeta/file-metadata-latest.json", serial);
    }

    public static async Task SaveFileMeta()
    {
        if (FileMeta is null)
        {
            Console.WriteLine("Error: File Metas not loaded or rendered.");
            return;
        }

        var serial = Serialize(FileMeta);

        var metaPath = GetDownloadFilePath("filemeta");

        if (!CheckDirExists(metaPath)) return;

        await WriteToFile($"filemeta/file-metadata-{NowFile}.json", serial);
        await WriteToFile("filemeta/file-metadata-latest.json", serial);
    }

    public static async Task LoadFileMetaFromFile(string filename, bool relativeFilename = true)
    {
        FileMeta = await DeserializeFromFile<Dictionary<string, FileMeta>?>(filename, relativeFilename);
    }

    private static bool CheckFileHash(string fileId, Dictionary<string, FileMeta>? fileMetas)
    {
        if (fileMetas is null) return false;

        //TODO: Implement this
        return false;
    }

    public static void PrintFileMetaStatistics()
    {
        if (FileMeta is null)
        {
            Console.WriteLine("File metas not yet loaded! Please restore or render file metas!");
            return;
        }

        //TODO: Remove this when adding in file hashing check
#pragma warning disable CS0219 // Variable is assigned but its value is never used
        long attachmentCount = 0, completedCount = 0, verifiedHashes = 0, bytesRemaing = 0;
#pragma warning restore CS0219 // Variable is assigned but its value is never used

        foreach (var f in FileMeta)
        {
            attachmentCount++;

            if (f.Value.Complete)
                completedCount++;
            //TODO: Implement file hash checking
            //TODO: Implement file size checking against metadata
            else
                bytesRemaing += f.Value.AttachmentData.Bytes.GetValueOrDefault();
        }

        Console.WriteLine(
            $"There are {attachmentCount} attachments in the metadata, {completedCount} are completed, with {attachmentCount - completedCount} ({bytesRemaing} bytes) remaining.");
    }
}