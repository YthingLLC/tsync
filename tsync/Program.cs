using tsync;

Console.WriteLine("tsync - trello to ms planner sync tool\n");

var settings = Settings.LoadSettings();

List<TBoard>? boards = null;

// Initialize Graph
InitializeGraph(settings);

// Greet the user by name
await GreetUserAsync();

int choice = -1;

while (choice != 0)
{
    
    Console.WriteLine("Please choose one of the following options:");
    Console.WriteLine("0. Exit");
    
    Console.WriteLine("---Trello Options---");
    if (boards is not null)
    {
        Console.WriteLine("+++Trello Boards Loaded+++");
    }
    Console.WriteLine("1. Download Latest Trello Boards");
    Console.WriteLine("2. Load Previously Downloaded Data");
    Console.WriteLine("3. Print Board Statistics");

    Console.WriteLine("---Graph Options---");
    Console.WriteLine("10. Make a Graph call");
    
    Console.WriteLine("---Graph Debug Opts---");
    Console.WriteLine("101. Display access token");
    Console.WriteLine("102. List my inbox");
    Console.WriteLine("103. Send mail");
    try
    {
        choice = int.Parse(Console.ReadLine() ?? string.Empty);
    }
    catch (System.FormatException)
    {
        // Set to invalid value
        choice = -1;
    }

    switch(choice)
    {
        case 0:
            // Exit the program
            Console.WriteLine("Goodbye...");
            break;
        
        case 1:
            boards = await DownloadTrelloBoards();
            break;
        case 2:
            await LoadDownloadedTrelloData();
            break;
        case 3:
            TrelloHelper.PrintBoardStatistics(boards);
            break;
        
        case 10:
            await MakeGraphCallAsync();
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

void InitializeGraph(Settings settings)
{
    GraphHelper.InitializeGraphForUserAuth(settings,
        (info, cancel) =>
        {
            Console.WriteLine(info.Message);
            return Task.FromResult(0);
        });
}

async Task GreetUserAsync()
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

async Task DisplayAccessTokenAsync()
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

async Task ListInboxAsync()
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

async Task SendMailAsync()
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

async Task LoadDownloadedTrelloData()
{
    TrelloHelper.SetDownloadPath(settings.DownloadPath);
    
    Console.WriteLine("Enter filename, or press enter to load `data-export-latest.json`");
    Console.WriteLine($"File must be in {settings.DownloadPath} directory!");

    String? filename = Console.ReadLine();

    if (filename is null || filename.Equals(String.Empty, StringComparison.InvariantCulture))
    {
        filename = "data-export-latest.json";
    }

    boards = await TrelloHelper.LoadBoardsFromFile(filename);

    if (boards is not null)
    {
        Console.WriteLine("Boards Loaded Successfully");
        Console.WriteLine();
        TrelloHelper.PrintBoardStatistics(boards);
    }
    
}

async Task<List<TBoard>> DownloadTrelloBoards()
{
    TrelloHelper.SetCredentials(settings.TrelloApiKey, settings.TrelloUserToken);
    TrelloHelper.SetDownloadPath(settings.DownloadPath);
    
    var orgs = await TrelloHelper.GetAllOrgs();
    var boards = await TrelloHelper.GetAllOrgBoards(orgs);
    boards = await TrelloHelper.GetCardsForBoardList(boards);
    
    TrelloHelper.PrintBoardStatistics(boards);
    
    return boards;
}

async Task MakeGraphCallAsync()
{
    await GraphHelper.MakeGraphCallAsync();
}
