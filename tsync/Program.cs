using System.Text.Json;
using Microsoft.Graph.Print.Shares.Item.Jobs.Item.Start;
using tsync;

internal static class Tsync
{
    private static readonly Settings _settings = Settings.LoadSettings();
    private static List<TBoard>? _boards;

    private static List<FileMeta> _uploadedMetas = new();
    
    struct BoardMap (String trelloBoard, String graphGroup, String graphPlan)
    {
        public String TrelloBoardID = trelloBoard;
        public String GraphGroupID = graphGroup;
        public String GraphPlanID = graphPlan;

        public override string ToString()
        {
            return $"Trello Board: {TrelloBoardID} = MS Plan Group: {GraphGroupID}, Plan: {GraphPlanID}";
        }
    }

    static List<BoardMap> _boardMaps = new();
    async static Task Main()
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
                    break;
                case 25:
                    PrintUploadedMetas();
                    break;
                case 26:
                    _uploadedMetas = new();
                    break;
                case 27:
                    await SaveGraphUploadState();
                    break;
                case 28:
                    await LoadGraphUploadState();
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
    }

    static void InitializeGraph(Settings settings)
    {
        GraphHelper.InitializeGraphForUserAuth(settings,
            (info, cancel) =>
            {
                Console.WriteLine(info.Message);
                return Task.FromResult(0);
            });
    }

    static async Task GreetUserAsync()
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

    static async Task DisplayAccessTokenAsync()
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

    static async Task ListInboxAsync()
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

    static async Task SendMailAsync()
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

    static async Task LoadDownloadedTrelloData()
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

    static async Task LoadLatestFileMeta()
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

    static async Task RenderAndSaveFileMetas()
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

    static async Task<List<TBoard>> DownloadTrelloBoards()
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

    static async Task GetAllGraphPlans()
    {
        await GraphHelper.GetAllGraphPlans();
    }

    
    static void MapBoardsToPlans()
    {
        if (_boards is null || GraphHelper.Plans.Count == 0)
        {
            Console.WriteLine("Error: Need to load both: Trello Boards and Graph Plans!");
            Console.WriteLine("Note: You need to also create the plans first, this tool does not create Planner Plans");
            return;
        }

        _boardMaps = new();

        List<GraphHelper.GroupPlan> plannerPlans = new();

        foreach (var p in GraphHelper.Plans)
        {
            plannerPlans.Add(p);
        }

        foreach (var b in _boards)
        {
            Start:
            Console.WriteLine($"Trello Board: {b.Id} - {b.Name}");
            Console.WriteLine("Select Planner Plan to Sync To:");
            Console.WriteLine("");
            for (int i = 0; i < plannerPlans.Count; i++)
            {
                Console.WriteLine($"{i}. {plannerPlans[i].ToString()}");
            }

            var input = Console.ReadLine();
            Int32 result = -1;
            
            if (!Int32.TryParse(input, out result) || result < 0 || result > plannerPlans.Count)
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

    static void PrintBoardMaps()
    {
        if (_boardMaps.Count < 1)
        {
            Console.WriteLine("No board maps defined.");
            return;
        }
        
        foreach (var bm in _boardMaps)
        {
            Console.WriteLine(bm.ToString());
        }
    }

    static String GetPlanForBoard(String boardId)
    {
        var planMap = from board in _boardMaps
            where board.TrelloBoardID.Equals(boardId, StringComparison.InvariantCulture)
            select board;

        var plan = planMap.FirstOrDefault();

        return plan.GraphPlanID;
    }

    static void PrintUploadedMetas()
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
            Console.WriteLine($"{m.FileID}:{m.AttachmentData.Id}/{m.AttachmentData.FileName} ({m.Complete}):({m.AttachmentData.IsUpload}) == {m.GraphUrl}");
        }
    }
    
    async static Task UploadTrelloFilesToGraph()
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
            Console.WriteLine($"Files already uploaded this session.");
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
                Console.WriteLine($"Unable to upload {fm.Key} - {fm.Value.FileID} - {fm.Value.AttachmentData.FileName}");
                continue;
            }

            var webUrl = await GraphHelper.UploadFileToPlanGroup(GetPlanForBoard(fm.Value.OriginBoard), fm.Value.AttachmentData.FileName, fs);

            var fm_new = fm.Value;
            fm_new.GraphUrl = webUrl;
            
            lock (_uploadedMetas)
            {
                _uploadedMetas.Add(fm_new);
            }

        }
        
    }

    async static Task SaveGraphUploadState()
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

    async static Task LoadGraphUploadState()
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
}