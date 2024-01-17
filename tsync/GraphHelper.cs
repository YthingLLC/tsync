using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    //Needed because creating a task in Graph does not create an associated conversation thread
    //like it does in the planner web ui
    //so... create the thread, with a small "tsync" message, and then attach that thread
    //to newly created tasks!
    private static async Task<String?> CreateConversationThread(String groupId, String taskTitle)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph client is not initialized");
            return null;
        }

        var title = taskTitle;

        if (taskTitle.Length > 50)
        {
            title = title.Substring(0, 50);
        }

        var body = new ConversationThread
        {
            Topic = title,
            Posts = new List<Post>
            {
                new Post
                {
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Text,
                        Content = $"[tsync] created at : {DateTimeOffset.Now}"
                    }
                }
            }
        };

        await GraphApiLimiter;
        //somehow, someway, using the Graph SDK to create the damn thread
        //does not mean it knows about the thread when you try to post replies to it
        //using the exact same _userClient object
        //and it instead throws a damn fit (exception)
        //it really would've been easier to just never use the damn SDK in the first place!
        //what a piece...
        var resp = await _userClient.Groups[groupId].Threads.PostAsync(body);

        if (resp is not null)
        {
            return resp.Id;
        }
        
        return null;
    }

    //Used for caching responses from GetGroupIdForPlan
    //I could probably rework this a bit to not *need* make these calls again
    //...but it is not likely to be many total calls in the grand scheme of things
    //so too bad, this is easier. Also "hides" this impl detail from the caller
    //even though my only caller (at the moment) knows what the groupIds are already
    private static Dictionary<String, String> PlanGroups = new();
    
    private static async Task<String?> GetGroupIdForPlan(String planId)
    {
        //see? At least I use a cache, it's not that bad...
        if (PlanGroups.ContainsKey(planId))
        {
            return PlanGroups[planId];
        }
        if (_userClient is null)
        {
            Console.WriteLine("Graph client is not initialized");
            return null;
        }

        await GraphApiLimiter;
        var resp = await _userClient.Planner.Plans[planId].GetAsync();

        if (resp is null || resp.Container is null || resp.Container.ContainerId is null)
        {
            Console.WriteLine($"Error: Could not determine Group for Plan {planId}");
            return null;
        }
        
        //...but the cache only works if you add to it
        PlanGroups.Add(planId, resp.Container.ContainerId);
        return resp.Container.ContainerId;

    }
    
    //only here because it's easier to convert the PlannerTask to a TPlannerTask than to
    //rewrite the logic that creates the PlannerTask in the first place...
    public static async Task<(String?, String?)?> CreateTask(PlannerTask task)
    {
        var tplanId = task.PlanId;
        var tbucketId = task.BucketId; 
        var ttitle = task.Title;

        if (tplanId is null || tbucketId is null || ttitle is null)
        {
            Console.WriteLine("Error: PlanID, BucketID, and Title are required to create new tasks!");
            return null;
        }
        
        var groupId = await GetGroupIdForPlan(tplanId);

        if (groupId is null)
        {
            Console.WriteLine($"Error: Could not create conversation thread for Task {ttitle}. (Is PlanID correct?)");
            return null;
        }

        var convId = await CreateConversationThread(groupId, ttitle);

        if (convId is null)
        {
            Console.WriteLine($"Error: Could not create conversation thread for Task {ttitle}. Check permissions to Group {groupId}");
            return null;
        }

        if (task.Details is null)
        {
            return await CreateTask(new TPlannerTask
            {
                planId = tplanId,
                bucketId = tbucketId,
                title = ttitle,
                conversationThreadId = convId
            });
        }

        Dictionary<String, TPlannerTaskExternalReference>? refs = null;
        Dictionary<String, TPlannerTaskCheckItem>? clists = null;

        if (task.Details.Checklist is not null)
        {
            clists = new();
            foreach (var citem in task.Details.Checklist.AdditionalData)
            {
                //...I hate that the official SDK doesn't really support this properly
                //it's this weird boxed type... same for external references...
                //also, first time I've ever seen this syntax. Rider suggested it, but I kind of like it
                //reminds me of 'if let' in Rust
                if (citem.Value is not PlannerChecklistItem val || val.Title is null)
                {
                    Console.WriteLine("Error: Title is required for checklist items!");
                    continue;
                }

                var titem = new TPlannerTaskCheckItem
                {
                    isChecked = val.IsChecked ?? false,
                    title = val.Title,
                    lastModifiedDateTime = val.LastModifiedDateTime,
                    orderHint = val.OrderHint
                };
                
                clists.Add(citem.Key, titem);

            }
        }

        if (task.Details.References is not null)
        {
            refs = new();

            foreach (var ritem in task.Details.References.AdditionalData)
            {
                if (ritem.Value is not PlannerExternalReference val || val.Alias is null)
                {
                    Console.WriteLine("Error: Alias is required for external references!");
                    continue;
                }

                var titem = new TPlannerTaskExternalReference
                {
                    alias = val.Alias,
                    lastModifiedDateTime = val.LastModifiedDateTime
                };

                var refLink = ritem.Key;
                if (refLink.StartsWith("@"))
                {
                    //official Graph SDK requires a silly @ at the front of this field
                    //strip it off when creating the TPlannerTaskExternalReference
                    refLink = refLink.Substring(1);
                }
                
                refs.Add(refLink, titem);
            }
        }

        var tdetails = new TPlannerTaskDetails
        {
            description = task.Details.Description,
            references = refs,
            checklist = clists
        };

        return await CreateTask(new TPlannerTask
        {
            planId = tplanId,
            bucketId = tbucketId,
            title = ttitle,
            conversationThreadId = convId,
            details = tdetails
        });



    }
    
    public static async Task<(String?, String?)?> CreateTask(TPlannerTask task)
    {
        //Item1 = TaskId
        //Item2 = ThreadId
        if (_userClient is null)
        {
            Console.WriteLine("Graph client not initialized!");
            return null;
        }

        var opts = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        var serial = System.Text.Json.JsonSerializer.Serialize(task, opts);
        
        //Console.WriteLine(serial);


        await GraphApiLimiter;
        var token = await GetUserTokenAsync();
        
        //TODO: Consider reusing HttpClients
        //idk, maybe share it with the upload client?
        using (var client = new HttpClient())
        {

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            
            using (var content = new StringContent((serial)))
            {
                var postUrl = $"https://graph.microsoft.com/v1.0/planner/tasks";

                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                
                
                await GraphApiLimiter;
                var resp = await client.PostAsync(postUrl, content);

                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error: Can not create task. The server said: {await resp.Content.ReadAsStringAsync()}");
                    return null;
                }


                var respContent = await resp.Content.ReadAsStringAsync();
                
                var jresp = JsonSerializer.Deserialize<TPlannerTask?>(respContent, opts);
                if (jresp is null || jresp.Value.id is null || jresp.Value.conversationThreadId is null || jresp.Value.etag is null)
                {
                    Console.WriteLine("Error parsing Task response from server.");
                    Console.WriteLine($"Server sent: {jresp}");
                    return null;
                }

                if (!jresp.Value.conversationThreadId.Equals(task.conversationThreadId,
                        StringComparison.InvariantCulture))
                {
                    Console.WriteLine($"Info: Graph returned conversation thread ID does not match provided thread ID: {jresp.Value.conversationThreadId} != {task.conversationThreadId}");
                }

                if (task.details is null)
                {
                    return (jresp.Value.id, jresp.Value.conversationThreadId);
                }

                var taskDetails = JsonSerializer.Serialize(task.details);
                using (var patchContent = new StringContent(taskDetails))
                {
                    patchContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    client.DefaultRequestHeaders.Add("Prefer", "return=representation");
                    client.DefaultRequestHeaders.Add("If-Match", jresp.Value.etag);

                    var patchUrl = $"https://graph.microsoft.com/v1.0/planner/tasks/{jresp.Value.id}/details";
                    
                    await GraphApiLimiter;
                    var detailsResp = await client.PatchAsync(patchUrl, patchContent);
                    
                    if(!resp.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Error: Unable to patch task details. The server said {await detailsResp.Content.ReadAsStringAsync()}");
                        Console.WriteLine($"Deleting task: {jresp.Value.id}, due to details failure.");
                        await DeleteTask(jresp.Value.id, jresp.Value.etag);
                        return null;
                    }

                    return (jresp.Value.id, jresp.Value.conversationThreadId);
                }
            }
        }
        //this is allegedly unreachable. I don't know if I believe that.
        return null;
    }

    //...this also can't use the Graph SDK
    //because it caches groups and threads
    //and if it didn't make them, or request them, it doesn't know about them
    //and instead of just sending the damn request
    //it instead throws an exception!
    //what a piece of...
    public static async Task PostReplyToGroupThread(String groupId, String threadId, String reply)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph client not initialized!");
            return;
        }

        var body = JsonSerializer.Serialize(new TGroupThreadPostContainer(reply));

        await GraphApiLimiter;
        var token =  await GetUserTokenAsync();

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            using (var content = new StringContent(body))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                //threads have == at the end usually, need to escape, this seems to be the simplest way
                var postUrl = new Uri($"https://graph.microsoft.com/v1.0/groups/{groupId}/threads/{threadId}/reply").ToString();

                await GraphApiLimiter;

                var resp = await client.PostAsync(postUrl, content);

                //Sometimes, this will return 404, even though using Graph explorer it works fine.
                //I think this has something to do with the request happening too soon after creating the thread?
                //Using the exact same postUrl and body with Graph Explorer works fine
                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine("Info: Retrying post-reply, Graph returned 404...");
                    await Task.Delay(TimeSpan.FromMilliseconds(1500));
                    resp = await client.PostAsync(postUrl, content);
                }
                
                if (!resp.IsSuccessStatusCode)
                {
                    //TODO: Figure out wtf is going on with curl, no idea why this returns 404 when Graph Explorer works fine
                    //same Url and body
                    Console.WriteLine($"Error: Unable to post reply to group {groupId} thread {threadId}. The server said {await resp.Content.ReadAsStringAsync()}");
                }
            }
        }
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