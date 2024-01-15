using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Groups.Item.Threads.Item.Reply;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using tsync;
using RateLimiter;
using ComposableAsync;

internal class GraphHelper
{
    // Settings object
    private static Settings? _settings;

    // User auth token credential
    private static DeviceCodeCredential? _deviceCodeCredential;

    // Client configured with user authentication
    private static GraphServiceClient? _userClient;

    public static List<GroupPlan> Plans { get; private set; } = new();
    
    public static bool PlansLoaded => Plans.Count > 0;
    
    //Pretty much only used with the task posting
    //Microsoft's docs dont say that there is a limit, but observably, there is.
    //Setting to the same as Trello
    private static readonly TimeLimiter GraphApiLimiter =
        TimeLimiter.GetFromMaxCountByInterval(9, TimeSpan.FromSeconds(1));
    public static void InitializeGraphForUserAuth(Settings settings,
        Func<DeviceCodeInfo, CancellationToken, Task> deviceCodePrompt)
    {
        _settings = settings;

        var options = new DeviceCodeCredentialOptions
        {
            ClientId = settings.ClientId,
            TenantId = settings.TenantId,
            DeviceCodeCallback = deviceCodePrompt
        };

        _deviceCodeCredential = new DeviceCodeCredential(options);

        _userClient = new GraphServiceClient(_deviceCodeCredential, settings.GraphUserScopes);
        
    }

    public static async Task<string> GetUserTokenAsync()
    {
        // Ensure credential isn't null
        _ = _deviceCodeCredential ??
            throw new NullReferenceException("Graph has not been initialized for user auth");

        // Ensure scopes isn't null
        _ = _settings?.GraphUserScopes ?? throw new ArgumentNullException("Argument 'scopes' cannot be null");

        // Request token with given scopes
        var context = new TokenRequestContext(_settings.GraphUserScopes);
        var response = await _deviceCodeCredential.GetTokenAsync(context);
        return response.Token;
    }

    public static Task<User?> GetUserAsync()
    {
        // Ensure client isn't null
        _ = _userClient ??
            throw new NullReferenceException("Graph has not been initialized for user auth");

        return _userClient.Me.GetAsync(config =>
        {
            // Only request specific properties
            config.QueryParameters.Select = new[] { "displayName", "mail", "userPrincipalName" };
        });
    }

    public static Task<MessageCollectionResponse?> GetInboxAsync()
    {
        // Ensure client isn't null
        _ = _userClient ??
            throw new NullReferenceException("Graph has not been initialized for user auth");

        return _userClient.Me
            // Only messages from Inbox folder
            .MailFolders["Inbox"]
            .Messages
            .GetAsync(config =>
            {
                // Only request specific properties
                config.QueryParameters.Select = new[] { "from", "isRead", "receivedDateTime", "subject" };
                // Get at most 25 results
                config.QueryParameters.Top = 25;
                // Sort by received time, newest first
                config.QueryParameters.Orderby = new[] { "receivedDateTime DESC" };
            });
    }

    public static async Task SendMailAsync(string subject, string body, string recipient)
    {
        // Ensure client isn't null
        _ = _userClient ??
            throw new NullReferenceException("Graph has not been initialized for user auth");

        // Create a new message
        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody
            {
                Content = body,
                ContentType = BodyType.Text
            },
            ToRecipients = new List<Recipient>
            {
                new()
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = recipient
                    }
                }
            }
        };

        // Send the message
        await _userClient.Me
            .SendMail
            .PostAsync(new SendMailPostRequestBody
            {
                Message = message
            });
    }

    private static KeyValuePair<K, Task<V>> TaskKVP<K, V>(K id, Task<V> task)
    {
        return new KeyValuePair<K, Task<V>>(id, task);
    }

    private static void PrintLine()
    {
        Console.WriteLine("--------------------------------------------------------------------------------");
    }

    private static async Task<List<GraphUser>?> GetAllUsers()
    {
        await GraphApiLimiter;
        if (_userClient is null)
        {
            Console.WriteLine("Graph client has not been initialized for user auth");
            return null;
        }

        var users = await _userClient.Users.GetAsync();

        if (users?.Value is null) return null;

        var ret = new List<GraphUser>();

        foreach (var u in users.Value)
            if (u.DisplayName is not null && u.Id is not null && u.Mail is not null)
                ret.Add(new GraphUser(u.DisplayName, u.Id, u.Mail));

        return ret;
    }


    //I hate this function
    //You have to request a list of plans for each group
    //As there is no endpoint from Graph that contains a list of all plans
    //So after requesting a list of all groups
    //You have to make a request in each group to see if it contains any plans
    //There is no such method to get just groups with plans
    //or just a list of plans, and what groups they belong to
    //Why? idk, ask Microsoft.
    //yes /me/planner/plans exists, but this will not find plans that aren't owned by the user running tsync
    private static async Task<List<GroupPlans>?> GetAllPlans()
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph client has not been initialized for user auth");
            return null;
        }

        await GraphApiLimiter;
        
        var groups = await _userClient.Groups.GetAsync();

        if (groups?.Value is null)
        {
            Console.WriteLine("Unable to get Groups from Graph");
            return null;
        }

        var grplans = new List<KeyValuePair<(string, string), Task<PlannerPlanCollectionResponse?>>>(100);

        foreach (var g in groups.Value)
        {
            if (g.Id is null || g.DisplayName is null) continue;
            //put the tasks in the list, we are going to await them later
            //allows for multiple concurrent requests in flight
            await GraphApiLimiter;
            grplans.Add(TaskKVP((g.Id, g.DisplayName), _userClient.Groups[g.Id].Planner.Plans.GetAsync()));
        }

        //var groupId = "Group ID";
        //var groupName = "Group Name";
        //var planCount = "Plan Count";
        //grr... C# why don't you have ^40 like Rust for centered alignment
        //var header = $"{groupId,-38} {groupName,-36} {planCount,-4}";
        //Console.WriteLine(header);

        //probably oversized, as not all groups will have a plan, oh well.
        var plansList = new List<GroupPlans>(grplans.Count);
        foreach (var g in grplans)
        {
            //Console.Write($"{g.Key.Item1, -38} {g.Key.Item2, -36} ");
            var val = await g.Value;
            if (val?.Value is null)
                //Console.WriteLine("NULL");
                continue;

            //Console.WriteLine($"{val.Value.Count}");
            if (val.Value.Count > 0) plansList.Add(new GroupPlans(g.Key.Item1, g.Key.Item2, val.Value));
        }

        //PrintLine();
        //Console.WriteLine($"Found {plansList.Count} groups with plans:");

        //foreach (var p in plansList)
        //{
        //    Console.WriteLine($"{p.groupId} - {p.groupName} - {p.plans.Count}");
        //    foreach (var plan in p.plans)
        //    {
        //        Console.WriteLine($"----{plan.Id}: {plan.Title} - {plan.Details}");
        //        //requires an additional API call :(
        //        //Console.WriteLine($"----Task Count: {plan.Tasks?.Count}");
        //        //also requires an additional API call :( :(
        //        //Console.WriteLine($"----Bucket Count: {plan.Buckets?.Count}");
        //        //user name returned is null from GraphAPI, display Id instead
        //        Console.WriteLine($"----Created By: {plan.CreatedBy?.User?.Id}"); 
        //    }
        //}

        return plansList;
    }

    private static async Task<List<PlanBucket>?> GetPlanBuckets(string planId)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Can't get plan buckets, Graph client not init.");
            return null;
        }

        await GraphApiLimiter;
        var buckets = await _userClient.Planner.Plans[planId].Buckets.GetAsync();

        if (buckets?.Value is null)
        {
            Console.WriteLine("Graph returned a null set of buckets");
            return null;
        }

        var ret = new List<PlanBucket>(buckets.Value.Count);
        foreach (var b in buckets.Value)
            if (b.Name is not null && b.Id is not null)
                ret.Add(new PlanBucket(b.Name, b.Id));

        return ret;
    }

    //TODO: Implement attachment downloading
    private static async Task DownloadAttachment(TCard card)
    {
        throw new NotImplementedException();
    }

    private static async Task UploadAttachment(string name, Stream attachment, string taskId)
    {
        //TODO:
        //1. Upload the attachment to SharePoint site associated with the plan
        //2. Create the request body to attach the upload to the task
        //3. Confirm that the upload actually suceeded
        //https://learn.microsoft.com/en-us/graph/api/resources/plannerexternalreferences?view=graph-rest-1.0
        //URLs are encoded, idk if the API needs this or if it will take unencoded URLs, probably not

        throw new NotImplementedException();
    }

    //the Graph API (v1) does not have the ability to create new planner plans
    //it can be done with the beta API, but I don't want to use that for this (beta API is subj to change)
    //I'd rather the "plans" be created by the user in the web UI
    //and then selected through tsync
    public static async Task GetAllGraphPlans()
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph has not been initialized for user auth");
            return;
        }

        //zero out existing list, new download has been requested
        Plans = new List<GroupPlan>();

        var groupPlans = await GetAllPlans();

        var users = await GetAllUsers();


        if (groupPlans is null || users is null)
        {
            Console.WriteLine("Missing required information from Graph API");
            return;
        }

        //maybe in the future I'll care about this, but not right now
        //right now I just need to get Trello -> Planner working
        //TODO: Maybe determine if I can build this to be a 2 way sync
        //var planBuckets = new Dictionary<String, List<PlanBucket>>();

        foreach (var gp in groupPlans)
            //Console.WriteLine($"{gp.groupId} - {gp.groupName}");
        foreach (var p in gp.plans)
        {
            Console.Write(".");

            //Console.WriteLine($"----{p.Id} - {p.Title}");
            await GraphApiLimiter;
            var drives = await _userClient.Groups[gp.groupId].Drives.GetAsync();
            if (drives?.Value?[0].Id is null)
            {
                Console.WriteLine(
                    $"Error: Group {gp.groupId} - {gp.groupName} has no drives! Please ensure that the Documents library exists for this group.");
                continue;
            }

            if (p.Id is null || p.Title is null)
            {
                Console.WriteLine("Error: Group plan ID or name is null. Please fix.");
                continue;
            }

            //                                                                                             !this is not null, fake warning
            Plans.Add(
                new GroupPlan(gp.groupId, gp.groupName, p.Id, p.Title, drives.Value[0].Id!, drives.Value[0].Name!));
        }

        Console.WriteLine();

        PrintPlans();

        //TODO: Create buckets = lists (trello)
        //TODO: Create tasks = cards (trello)
        //TODO: Create conversations = comments (trello)
        //TODO: Create checklists = checklists
        //TODO: Check for any data that trello has that doesnt fit very well into Planner (hopefully none)
        //TODO: Print logs for entire process, and pause on errors
    }

    public static void PrintPlans()
    {
        foreach (var p in Plans) Console.WriteLine(p.ToString());
    }

    public static async Task<string?> UploadFileToPlanGroup(string planId, string fileName, Stream data)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph client is not initialized");
            return null;
        }

        if (Plans.Count == 0)
        {
            Console.WriteLine("Error: Need to load Group Plans!");
            return null;
        }

        var drive = from plan in Plans
            where plan.planId.Equals(planId, StringComparison.InvariantCulture)
            select plan;

        // ReSharper disable once PossibleMultipleEnumeration
        if (!drive.Any()) Console.WriteLine("Error: Invalid planID");

        //I don't care, this will almost always only have 1 result
        //this looks nicer than any alternative I can think of
        // ReSharper disable once PossibleMultipleEnumeration
        var gp = drive.First();

        //apparently the Graph SDK does not support uploading files... some web resources show that it does, but are all >2 years old
        //so I am assuming that this is no longer the case, and I can't find any other code than just saying f*** it, do it live
        await GraphApiLimiter;
        var token = await GetUserTokenAsync();

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            //ensure we are rewound to the start of the stream
            data.Position = 0;
            using (var content = new StreamContent(data))
            {
                //yes, I know this Guid does not match the one we created in the FileMeta, I also don't care. Should I reuse it?
                //maybe? generating a new Guid isn't expensive though, and ensures no file name conflicts.
                var uploadUrl =
                    $"https://graph.microsoft.com/v1.0/groups/{gp.groupId}/drive/root:/tsync/{Guid.NewGuid().ToString()}/{fileName}:/content";

                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                await GraphApiLimiter;
                var resp = await client.PutAsync(uploadUrl, content);

                if (!resp.IsSuccessStatusCode)
                    Console.WriteLine(
                        $"Error: Can not upload file. The server said: {await resp.Content.ReadAsStringAsync()}");

                using (var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
                {
                    if (json.RootElement.TryGetProperty("webUrl", out var webUrl))
                    {
                        var url = webUrl.GetString();
                        Console.WriteLine($"File {fileName} uploaded. - {url}");
                        return url;
                    }
                }
            }
        }

        Console.WriteLine($"Error: Failed to Upload file {fileName}. Unknown reason.");
        return null;
    }

    public static void PrintGroupDrives()
    {
        if (Plans.Count < 1)
        {
            Console.WriteLine("Error: Group Plans not loaded!");
            return;
        }

        foreach (var p in Plans)
        {
            Console.WriteLine(p.ToString());
            Console.WriteLine($"----{p.driveId} - {p.driveName}");
        }
    }

    public static async Task<String?> CreatePlanBucket(String planId, String bucketName)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph client not initialized");
            return null;
        }
        var body = new PlannerBucket
        {
            Name = bucketName,
            PlanId = planId
        };

        await GraphApiLimiter;
        var resp = await _userClient.Planner.Buckets.PostAsync(body);

        if (resp is null)
        {
            Console.WriteLine($"Error creating {bucketName} for {planId}");
            return null;
        }

        return resp.Id;
        
    }

    public static async Task<(String?, String?)?> CreateTask(PlannerTask task)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph client not initialized!");
            return null;
        }

        Int32 retryCount = 0;
        Retry:
        retryCount++;
        PlannerTask? resp = null;
        try
        {
            await GraphApiLimiter;
            resp = await _userClient.Planner.Tasks.PostAsync(task);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError e)
        {
            Console.WriteLine($"OData Error: {e.Message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"PostAsync Exception: {e.Message}");
            if (retryCount < 10)
            {
                goto Retry;
            }

            Console.WriteLine("Too many retry failures!");
        }
        if (resp is null)
        {
            Console.WriteLine($"Error posting task: {task.Title}");
            return null;
        }

        return (resp.Id, resp.ConversationThreadId);
    }

    public static async Task PostReplyToGroupThread(String groupId, String threadId, String reply)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph client not initialized!");
            return;
        }
        var body = new ReplyPostRequestBody()
        {
            Post = new Post
            {
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = reply
                }
            }
        };

        await GraphApiLimiter;
        await _userClient.Groups[groupId].Threads[threadId].Reply.PostAsync(body);
    }

    public static async Task<List<(String, String)>?> GetPlanTaskIds(String planId)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Error: Graph client is not initialized");
            return null;
        }

        await GraphApiLimiter;
        var resp = await _userClient.Planner.Plans[planId].Tasks.GetAsync();

        if (resp?.Value is null)
        {
            Console.WriteLine($"Error: Plan {planId} could not be retrieved from Graph");
            return null;
        }

        var ret = new List<(String, String)>();

        foreach (var r in resp.Value)
        {
            if (r.Id is null)
            {
                continue;
            }
            var etag = r.AdditionalData["@odata.etag"] as String;
            if (etag is null)
            {
                continue;
            }
            ret.Add((r.Id, etag));
        }

        return ret;
    }

    public static async Task DeleteTask(String taskId, String etag)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Error: Graph client is not initialized");
            return;
        }

        await GraphApiLimiter;
        await _userClient.Planner.Tasks[taskId].DeleteAsync((configuration) =>
        {
            configuration.Headers.Add("If-Match", etag);
        });
    }

    public static async Task<List<(String, String)>?> GetBucketIds(String planId)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Error: Graph client is not initialized");
            return null;
        }

        await GraphApiLimiter;
        var resp = await _userClient.Planner.Plans[planId].Buckets.GetAsync();
        
        if (resp?.Value is null)
        {
            Console.WriteLine($"Error: Plan {planId} could not be retrieved from Graph");
            return null;
        }
        
        var ret = new List<(String, String)>();
        
        foreach (var r in resp.Value)
        {
            if (r.Id is null)
            {
                continue;
            }
            var etag = r.AdditionalData["@odata.etag"] as String;
            if (etag is null)
            {
                continue;
            }
            ret.Add((r.Id, etag));
        }
        
        return ret;
    }

    public static async Task DeleteBucket(String bucketId, String etag)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Error: Graph client is not initialized");
            return;
        }

        await GraphApiLimiter;
        await _userClient.Planner.Buckets[bucketId].DeleteAsync((configuration) =>
        {
            configuration.Headers.Add("If-Match", etag);
        });
    }
    
    public struct GroupPlan(
        string GroupId,
        string GroupName,
        string PlanId,
        string PlanName,
        string DriveId,
        string DriveName)
    {
        public string groupId { get; init; } = GroupId;
        public string groupName { get; init; } = GroupName;
        public string planId { get; init; } = PlanId;
        public string planName { get; init; } = PlanName;

        public string driveId { get; init; } = DriveId;

        public string driveName { get; init; } = DriveName;

        public override string ToString()
        {
            return $"Group: {groupId} - Plan: {planId} = {groupName} : {planName}";
        }
    }

    private struct GroupPlans
    {
        public string groupId { get; }
        public string groupName { get; }
        public List<PlannerPlan> plans { get; }

        public GroupPlans(string id, string name)
        {
            groupId = id;
            groupName = name;
            plans = new List<PlannerPlan>();
        }

        public GroupPlans(string id, string name, List<PlannerPlan> plans)
        {
            groupId = id;
            groupName = name;
            this.plans = plans;
        }
    }

    private struct GraphUser
    {
        public string name { get; init; }
        public string id { get; init; }
        public string mail { get; init; }

        public GraphUser(string name, string id, string mail)
        {
            this.name = name;
            this.id = id;
            this.mail = mail;
        }
    }

    private struct PlanBucket
    {
        public string name { get; init; }
        public string id { get; init; }

        public PlanBucket(string name, string id)
        {
            this.name = name;
            this.id = id;
        }
    }
}