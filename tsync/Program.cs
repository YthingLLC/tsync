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


using System.Text.Json;
using Microsoft.Graph.Models;
using tsync;

internal static class Tsync
{
    private static readonly Settings _settings = Settings.LoadSettings();
    private static List<TBoard>? _boards;

    private static List<FileMeta> _uploadedMetas = new();

    private static List<BoardMap> _boardMaps = new();

    private static async Task Main()
    {
        Console.WriteLine("tsync - trello to ms planner sync tool\n");
        //Initialize TrelloHelper
        TrelloHelper.SetDownloadPath(_settings.DownloadPath);
        TrelloHelper.SetCredentials(_settings.TrelloApiKey, _settings.TrelloUserToken);

        // Initialize Graph
        InitializeGraph(_settings);

        // Greet the user by name
        await GreetUserAsync();

        var choice = -1;

        while (choice != 0)
        {
            Console.WriteLine("Please choose one of the following options:");
            Console.WriteLine("0. Exit");

            Console.WriteLine("---Trello Options---");
            if (_boards is not null) Console.WriteLine("+++Trello Boards Loaded+++");
            Console.WriteLine("1. Download Latest Trello Boards");
            Console.WriteLine("2. Load Previously Downloaded Data");
            Console.WriteLine("3. Print Board Statistics");
            if (TrelloHelper.FileMetasLoaded) Console.WriteLine("+++FileMetas Rendered+++");
            Console.WriteLine("4. Render Attachment metadata and save");
            Console.WriteLine("5. Load Attachment metadata from file");
            Console.WriteLine("6. Print Metadata Statistics");
            Console.WriteLine("7. Download Incomplete Attachments");

            Console.WriteLine("---Graph Options---");
            if (GraphHelper.PlansLoaded) Console.WriteLine("+++Graph Plans Loaded+++");
            Console.WriteLine("10. Download list of plans from Graph");
            Console.WriteLine("11. Print Graph Plans");


            Console.WriteLine("---Migration Options---");
            Console.WriteLine("20. Map Trello Boards to Plans");
            Console.WriteLine("21. Show Current Board Mapping");
            Console.WriteLine("22. Upload Attachments to Plan Groups");
            Console.WriteLine("23. Show Plan Drives");
            Console.WriteLine("24. Sync Boards to Plans");
            Console.WriteLine("25. Show Detailed Upload Data");
            Console.WriteLine("26. Reset Upload State");
            Console.WriteLine("27. Save Upload State to File");
            Console.WriteLine("28. Load Upload State from File");
            Console.WriteLine("29. Clean Mapped Boards");

            Console.WriteLine("---Graph Debug Opts---");
            Console.WriteLine("101. Display access token");
            Console.WriteLine("102. List my inbox");
            Console.WriteLine("103. Send mail");
            try
            {
                choice = int.Parse(Console.ReadLine() ?? string.Empty);
            }
            catch (FormatException)
            {
                // Set to invalid value
                choice = -1;
            }

            try
            {
                switch (choice)
                {
                    case 0:
                        // Exit the program
                        Console.WriteLine("Goodbye...");
                        break;

                    case 1:
                        _boards = await DownloadTrelloBoards();
                        break;
                    case 2:
                        await LoadDownloadedTrelloData();
                        break;
                    case 3:
                        TrelloHelper.PrintBoardStatistics(_boards);
                        break;
                    case 4:
                        await RenderAndSaveFileMetas();
                        break;
                    case 5:
                        await LoadLatestFileMeta();
                        break;
                    case 6:
                        TrelloHelper.PrintFileMetaStatistics();
                        break;
                    case 7:
                        await TrelloHelper.DownloadAttachments();
                        break;

                    case 10:
                        await GetAllGraphPlans();
                        break;

                    case 11:
                        GraphHelper.PrintPlans();
                        break;

                    case 20:
                        MapBoardsToPlans();
                        break;
                    case 21:
                        PrintBoardMaps();
                        break;
                    case 22:
                        await UploadTrelloFilesToGraph();
                        break;
                    case 23:
                        GraphHelper.PrintGroupDrives();
                        break;
                    case 24:
                        await SyncBoardsToPlans();
                        break;
                    case 25:
                        PrintUploadedMetas();
                        break;
                    case 26:
                        _uploadedMetas = new List<FileMeta>();
                        break;
                    case 27:
                        await SaveGraphUploadState();
                        break;
                    case 28:
                        await LoadGraphUploadState();
                        break;
                    case 29:
                        await CleanBoards();
                        break;
                    case 101:
                        // Display access token
                        await DisplayAccessTokenAsync();
                        break;
                    case 102:
                        // List emails from user's inbox
                        await ListInboxAsync();
                        break;
                    case 103:
                        // Send an email message
                        await SendMailAsync();
                        break;

                    default:
                        Console.WriteLine("Invalid choice! Please try again.");
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
            }
        }
    }

    private static void InitializeGraph(Settings settings)
    {
        GraphHelper.InitializeGraphForUserAuth(settings,
            (info, cancel) =>
            {
                Console.WriteLine(info.Message);
                return Task.FromResult(0);
            });
    }

    private static async Task GreetUserAsync()
    {
        try
        {
            var user = await GraphHelper.GetUserAsync();
            Console.WriteLine($"Hello, {user?.DisplayName}!");
            Console.WriteLine($"Email: {user?.Mail ?? user?.UserPrincipalName ?? ""}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting user: {ex.Message}");
        }
    }

    private static async Task DisplayAccessTokenAsync()
    {
        try
        {
            var userToken = await GraphHelper.GetUserTokenAsync();
            Console.WriteLine($"User token: {userToken}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting user access token: {ex.Message}");
        }
    }

    private static async Task ListInboxAsync()
    {
        try
        {
            var messagePage = await GraphHelper.GetInboxAsync();

            if (messagePage?.Value == null)
            {
                Console.WriteLine("No results returned.");
                return;
            }

            foreach (var message in messagePage.Value)
            {
                Console.WriteLine($"Message: {message.Subject ?? "NO SUBJECT"}");
                Console.WriteLine($"  From: {message.From?.EmailAddress?.Name}");
                Console.WriteLine($"  Status: {(message.IsRead!.Value ? "Read" : "Unread")}");
                Console.WriteLine($"  Received: {message.ReceivedDateTime?.ToLocalTime().ToString()}");
            }

            // If NextPageRequest is not null, there are more messages
            // available on the server
            // Access the next page like:
            // var nextPageRequest = new MessagesRequestBuilder(messagePage.OdataNextLink, _userClient.RequestAdapter);
            // var nextPage = await nextPageRequest.GetAsync();
            var moreAvailable = !string.IsNullOrEmpty(messagePage.OdataNextLink);

            Console.WriteLine($"\nMore messages available? {moreAvailable}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting user's inbox: {ex.Message}");
        }
    }

    private static async Task SendMailAsync()
    {
        try
        {
            // Send mail to the signed-in user
            // Get the user for their email address
            var user = await GraphHelper.GetUserAsync();

            var userEmail = user?.Mail ?? user?.UserPrincipalName;

            if (string.IsNullOrEmpty(userEmail))
            {
                Console.WriteLine("Couldn't get your email address, canceling...");
                return;
            }

            await GraphHelper.SendMailAsync("Testing Microsoft Graph",
                "Hello world!", userEmail);

            Console.WriteLine("Mail sent.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending mail: {ex.Message}");
        }
    }

    private static async Task LoadDownloadedTrelloData()
    {
        Console.WriteLine("Enter filename, or press enter to load `data-export-latest.json`");
        Console.WriteLine($"File must be in {_settings.DownloadPath} directory!");

        var filename = Console.ReadLine();

        if (filename is null || filename.Equals(string.Empty, StringComparison.InvariantCulture))
            filename = "data-export-latest.json";

        _boards = await TrelloHelper.LoadBoardsFromFile(filename);

        if (_boards is not null)
        {
            Console.WriteLine("Boards Loaded Successfully");
            Console.WriteLine();
            TrelloHelper.PrintBoardStatistics(_boards);
        }
    }

    private static async Task LoadLatestFileMeta()
    {
        Console.WriteLine("Enter filename, or press enter to load `filemeta/file-metadata-latest.json");
        Console.WriteLine($"File must be in {_settings.DownloadPath} directory!");

        var filename = Console.ReadLine();

        if (filename is null || filename.Equals(string.Empty, StringComparison.InvariantCulture))
            filename = "filemeta/file-metadata-latest.json";

        await TrelloHelper.LoadFileMetaFromFile(filename);

        if (TrelloHelper.FileMetasLoaded)
        {
            Console.WriteLine("File meta loaded successfully");
            Console.WriteLine();
        }
    }

    private static async Task RenderAndSaveFileMetas()
    {
        if (_boards is null)
        {
            Console.WriteLine("Boards not yet loaded, download or restore previously downloaded");
            return;
        }

        TrelloHelper.RenderFileMeta(_boards);

        await TrelloHelper.SaveFileMeta();

        Console.WriteLine("File metadata rendered and saved");
    }

    private static async Task<List<TBoard>> DownloadTrelloBoards()
    {
        var orgs = await TrelloHelper.GetAllOrgs();
        //yeah, yeah, I know. Whatever. I don't care. If this fails, oh well. Just try it again.
        //It's not important at this stage.
#pragma warning disable CS8604 // Possible null reference argument.
        var boards = await TrelloHelper.GetAllOrgBoards(orgs);
        boards = await TrelloHelper.GetCardsForBoardList(boards);
#pragma warning restore CS8604 // Possible null reference argument.

        TrelloHelper.RenderFileMeta(boards);

        await TrelloHelper.SaveFileMeta();

        TrelloHelper.PrintBoardStatistics(boards);

        await TrelloHelper.SaveBoardsToFile(boards);

        return boards;
    }

    private static async Task GetAllGraphPlans()
    {
        await GraphHelper.GetAllGraphPlans();
    }


    private static void MapBoardsToPlans()
    {
        if (_boards is null || GraphHelper.Plans.Count == 0)
        {
            Console.WriteLine("Error: Need to load both: Trello Boards and Graph Plans!");
            Console.WriteLine("Note: You need to also create the plans first, this tool does not create Planner Plans");
            return;
        }

        _boardMaps = new List<BoardMap>();

        List<GraphHelper.GroupPlan> plannerPlans = new();

        foreach (var p in GraphHelper.Plans) plannerPlans.Add(p);

        foreach (var b in _boards)
        {
            Start:
            Console.WriteLine($"Trello Board: {b.Id} - {b.Name}");
            Console.WriteLine("Select Planner Plan to Sync To:");
            Console.WriteLine("");
            for (var i = 0; i < plannerPlans.Count; i++) Console.WriteLine($"{i}. {plannerPlans[i].ToString()}");

            var input = Console.ReadLine();
            var result = -1;

            if (!int.TryParse(input, out result) || result < 0 || result > plannerPlans.Count)
            {
                Console.WriteLine("Invalid input, please try again.");
                //yes, I know, goto. Too bad. Makes more sense than putting the entire logic into another loop
                //and it's only this one...
                goto Start;
            }

            _boardMaps.Add(new BoardMap(b.Id, plannerPlans[result].groupId, plannerPlans[result].planId));

            plannerPlans.RemoveAt(result);
        }

        Console.WriteLine("Success! All Trello Boards Mapped to MS Planner Plans!");

        PrintBoardMaps();
    }

    private static void PrintBoardMaps()
    {
        if (_boardMaps.Count < 1)
        {
            Console.WriteLine("No board maps defined.");
            return;
        }

        foreach (var bm in _boardMaps) Console.WriteLine(bm.ToString());
    }

    private static (string, string) GetPlanForBoard(string boardId)
    {
        var planMap = from board in _boardMaps
            where board.TrelloBoardID.Equals(boardId, StringComparison.InvariantCulture)
            select board;

        var plan = planMap.FirstOrDefault();

        return (plan.GraphPlanID, plan.GraphGroupID);
    }

    private static void PrintUploadedMetas()
    {
        if (_uploadedMetas.Count < 1)
        {
            Console.WriteLine("No files uploaded to Graph!");
            return;
        }

        foreach (var m in _uploadedMetas)
        {
            Console.WriteLine("Guid:AttachmentId/AttachmentFileName (complete):(isUpload) == GraphUrl");
            Console.WriteLine("If file is not upload, complete will be false, and no GraphUrl will be provided.");
            Console.WriteLine(
                $"{m.FileID}:{m.AttachmentData.Id}/{m.AttachmentData.FileName} ({m.Complete}):({m.AttachmentData.IsUpload}) == {m.GraphUrl}");
        }
    }

    private static async Task UploadTrelloFilesToGraph()
    {
        if (_boardMaps.Count < 1)
        {
            Console.WriteLine("No board maps defined");
            return;
        }

        if (TrelloHelper.FileMeta is null)
        {
            Console.WriteLine("File meta not loaded");
            return;
        }

        if (_uploadedMetas.Count > 0)
        {
            Console.WriteLine("Files already uploaded this session.");
            return;
        }

        foreach (var fm in TrelloHelper.FileMeta)
        {
            if (fm.Value.AttachmentData.Bytes is null || fm.Value.AttachmentData.Bytes < 1)
            {
                Console.WriteLine($"{fm.Value.AttachmentData.Id} - Empty attachment, skipping upload.");
                lock (_uploadedMetas)
                {
                    _uploadedMetas.Add(fm.Value);
                }

                continue;
            }

            var fs = TrelloHelper.OpenAttachmentAsStream(fm.Value);
            if (fs is null)
            {
                Console.WriteLine(
                    $"Unable to upload {fm.Key} - {fm.Value.FileID} - {fm.Value.AttachmentData.FileName}");
                continue;
            }

            var webUrl = await GraphHelper.UploadFileToPlanGroup(GetPlanForBoard(fm.Value.OriginBoard).Item1,
                fm.Value.AttachmentData.FileName, fs);

            var fm_new = fm.Value;
            fm_new.GraphUrl = webUrl;

            lock (_uploadedMetas)
            {
                _uploadedMetas.Add(fm_new);
            }
        }
    }

    private static async Task SaveGraphUploadState()
    {
        if (_uploadedMetas.Count < 1)
        {
            Console.WriteLine("No upload metas to save.");
            return;
        }

        var serial = JsonSerializer.Serialize(_uploadedMetas);

        await TrelloHelper.WriteToFileAsync("graph-upload-state-latest.json", serial);

        Console.WriteLine("State saved to File!");
    }

    private static async Task LoadGraphUploadState()
    {
        var deserial = await TrelloHelper.LoadFileMetasFromFile("graph-upload-state-latest.json");

        if (deserial is not null)
        {
            _uploadedMetas = deserial;
            Console.WriteLine("State Loaded from File!");
        }
        else
        {
            Console.WriteLine("Error loading latest state from disk, ensure graph-upload-state-latest.json exists!");
        }
    }

    private static async Task SyncBoardsToPlans()
    {
        if (_boards is null || _boardMaps.Count < 1 || _uploadedMetas.Count < 1 || !GraphHelper.PlansLoaded)
        {
            Console.WriteLine("Error: Missing required information to continue.");
            Console.WriteLine("Please ensure that boards are loaded, mapped, and attachments uploaded.");
            return;
        }
        
        Console.WriteLine("WARNING WARNING WARNING: This does not (currently) check any existing tasks, buckets, etc.");
        Console.WriteLine("This tool will be creating all new buckets, tasks, etc, on the mapped plans.");
        Console.WriteLine("A future release may include correlation, but not right now!");
        Console.WriteLine();
        Console.WriteLine("Please enter 'understood' and press enter to continue.");

        var input = Console.ReadLine();
        if (input is null || !input.Equals("understood", StringComparison.InvariantCulture))
        {
            Console.WriteLine("Aborting...");
            return;
        }

        Dictionary<String, PlannerTask> postedTasks = new();
        Dictionary<String, PlannerTask> postedTasksWithAttachments = new();
        Int32 boardCounter = 0, listCounter = 0, cardCounter = 0;
        List<String> failedCardIds = new();
        foreach (var b in _boards)
        {
            boardCounter++;
            var planId = GetPlanForBoard(b.Id);

            foreach (var list in b.Lists)
            {
                listCounter++;
                Console.WriteLine($"Creating bucket {list.Name}");
                var bucket = await GraphHelper.CreatePlanBucket(planId.Item1, list.Name);
                if (bucket is null)
                {
                    Console.WriteLine($"Error: Unable to create bucket {list.Name} in {planId}");
                    Console.WriteLine("Bailing out...");
                    return;
                }
                Console.WriteLine($"Bucket {bucket} created");
                
                foreach (var card in list.Cards)
                {
                    cardCounter++;
                    Console.WriteLine($"Creating task {card.Name}");
                    String title;
                    if (card.Name.Length > 255)
                    {
                        title = card.Name.Substring(0, 255);
                        Console.WriteLine($"Truncating name to 255 chars: {title}");
                    }
                    else
                    {
                        title = card.Name;
                    }
                    var task = new PlannerTask
                    {
                        PlanId = planId.Item1,
                        BucketId = bucket,
                        Title = title,
                        Details = new PlannerTaskDetails
                        {
                            Description = card.Description
                        }
                    };

                    if (card.CheckLists.Count > 0)
                    {
                        Console.WriteLine("Card has checklists... Adding.");
                        //https://stackoverflow.com/a/72734231
                        //I think this is really dumb, but whatever
                        String orderHint = " !";
                        task.Details.Checklist = new();
                        foreach (var clist in card.CheckLists)
                        {
                            foreach (var citem in clist.CheckItems)
                            {
                                Console.WriteLine($"Item: {citem.Name}");
                                String ctitle;
                                if (citem.Name.Length > 100)
                                {
                                    ctitle = citem.Name.Substring(0, 100);
                                    Console.WriteLine($"Truncating checklist item to 100 chars: {ctitle}");
                                }
                                else
                                {
                                    ctitle = citem.Name;
                                }
                                task.Details.Checklist.AdditionalData.Add(Guid.NewGuid().ToString(), new PlannerChecklistItem
                                {
                                    IsChecked = citem.Checked,
                                    Title = ctitle,
                                    OrderHint = orderHint
                                });
                                orderHint += " !";
                            }
                        }
                    }

                    if (card.Attachments.Count > 0)
                    {
                        Console.WriteLine($"Card has {card.Attachments.Count} attachments... attaching.");
                        task.Details.References = new();

                        foreach (var a in card.Attachments)
                        {
                            Console.Write("."); 
                            //probably a rather inefficient search, I should probably make a dictionary if this ends up being slow
                            var meta = (from m in _uploadedMetas where m.AttachmentData.Id == a.Id select m).First();
                            String refUrl;
                            if (meta.GraphUrl is not null)
                            {
                                refUrl = meta.GraphUrl;
                            }
                            else
                            {
                                refUrl = meta.AttachmentData.Url;
                            }

                            //Nope, see the note on EncodeUrlForExternalRef in GraphHelper for reason why
                            //refUrl = System.Web.HttpUtility.UrlEncode(refUrl);
                            refUrl = GraphHelper.EncodeUrlForExternalRef(refUrl);
                            task.Details.References.AdditionalData.Add($"@{refUrl}", new PlannerExternalReference
                            {
                                Alias = meta.AttachmentData.FileName,
                                Type = "other",
                                LastModifiedDateTime = meta.AttachmentData.Date,
                                OdataType = "microsoft.graph.externalReference"
                            });
                        }
                    }
                    
                    var resp = await GraphHelper.CreateTask(task);

                    if (resp is not null && resp.Value.Item1 is not null)
                    {
                        Console.WriteLine($"Task {resp.Value.Item1} created!");
                        postedTasks.Add(resp.Value.Item1, task);
                        if (card.Attachments.Count > 0)
                        {
                            postedTasksWithAttachments.Add(resp.Value.Item1, task);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Task failed to create!");
                        failedCardIds.Add(card.Id);
                        continue;
                    }

                    Console.WriteLine($"TID: {resp.Value.Item2} == {card.Comments.Count}");

                    if (card.Comments.Count > 0 && resp.Value.Item2 is not null)
                    {
                        Console.WriteLine("Posting comments...");
                        foreach (var c in card.Comments)
                        {
                            Console.Write(".");
                            await GraphHelper.PostReplyToGroupThread(planId.Item2, resp.Value.Item2,
                                c.ToString());
                        }

                        Console.WriteLine($"Comments posted! Task {resp.Value.Item1} created!");
                        //I don't remember why I'm breaking here...
                        //break;
                    }
                }
            }
        }

        Console.WriteLine($"Total posted tasks: {postedTasks.Count}");
        Console.WriteLine($"Total cards with attachments: {postedTasksWithAttachments.Count}");

        Console.WriteLine($"Counted Boards: {boardCounter}");
        Console.WriteLine($" Counted Lists: {listCounter}");
        Console.WriteLine($" Counted Cards: {cardCounter}");

        Console.WriteLine("The following cards had unrecoverable errors:");
        foreach (var c in failedCardIds)
        {
            Console.WriteLine(c);
        }
        
    }

    private static async Task CleanBoards()
    {
        if (_boardMaps.Count < 1)
        {
            Console.WriteLine("Error: Need board maps to clean boards!");
        }

        Console.WriteLine("WARNING WARNING WARNING: This will delete all tasks from the mapped boards.");
        Console.WriteLine("Please type 'understood' and press enter to continue");

        var input = Console.ReadLine();

        if (input is null || !input.Equals("understood", StringComparison.InvariantCulture))
        {
            Console.WriteLine("Aborting!");
        }

        foreach (var b in _boardMaps)
        {
            var tasks = await GraphHelper.GetPlanTaskIds(b.GraphPlanID);

            if (tasks is null)
            {
                continue;
            }

            foreach (var t in tasks)
            {
                Console.Write(".");
                await GraphHelper.DeleteTask(t.Item1, t.Item2);
            }

            var buckets = await GraphHelper.GetBucketIds(b.GraphPlanID);

            if (buckets is null)
            {
                continue;
            }

            foreach (var bucket in buckets)
            {
                Console.Write(".");
                await GraphHelper.DeleteBucket(bucket.Item1, bucket.Item2);
            }
        }
    }
    
    private struct BoardMap(string trelloBoard, string graphGroup, string graphPlan)
    {
        public readonly string TrelloBoardID = trelloBoard;
        public readonly string GraphGroupID = graphGroup;
        public readonly string GraphPlanID = graphPlan;

        public override string ToString()
        {
            return $"Trello Board: {TrelloBoardID} = MS Plan Group: {GraphGroupID}, Plan: {GraphPlanID}";
        }
    }
}