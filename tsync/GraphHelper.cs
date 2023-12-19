using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Me.SendMail;
using KeyValuePair = Microsoft.Graph.Models.KeyValuePair;

class GraphHelper
{
    // Settings object
    private static Settings? _settings;
    // User auth token credential
    private static DeviceCodeCredential? _deviceCodeCredential;
    // Client configured with user authentication
    private static GraphServiceClient? _userClient;

    public static void InitializeGraphForUserAuth(Settings settings,
        Func<DeviceCodeInfo, CancellationToken, Task> deviceCodePrompt)
    {
        _settings = settings;

        var options = new DeviceCodeCredentialOptions
        {
            ClientId = settings.ClientId,
            TenantId = settings.TenantId,
            DeviceCodeCallback = deviceCodePrompt,
        };

        _deviceCodeCredential = new DeviceCodeCredential(options);

        _userClient = new GraphServiceClient(_deviceCodeCredential, settings.GraphUserScopes);
    }

    public static async Task<string> GetUserTokenAsync()
    {
        // Ensure credential isn't null
        _ = _deviceCodeCredential ??
            throw new System.NullReferenceException("Graph has not been initialized for user auth");

        // Ensure scopes isn't null
        _ = _settings?.GraphUserScopes ?? throw new System.ArgumentNullException("Argument 'scopes' cannot be null");

        // Request token with given scopes
        var context = new TokenRequestContext(_settings.GraphUserScopes);
        var response = await _deviceCodeCredential.GetTokenAsync(context);
        return response.Token;
    }

    public static Task<User?> GetUserAsync()
    {
        // Ensure client isn't null
        _ = _userClient ??
            throw new System.NullReferenceException("Graph has not been initialized for user auth");

        return _userClient.Me.GetAsync((config) =>
        {
            // Only request specific properties
            config.QueryParameters.Select = new[] {"displayName", "mail", "userPrincipalName" };
        });
    }

    public static Task<MessageCollectionResponse?> GetInboxAsync()
    {
        // Ensure client isn't null
        _ = _userClient ??
            throw new System.NullReferenceException("Graph has not been initialized for user auth");

        return _userClient.Me
            // Only messages from Inbox folder
            .MailFolders["Inbox"]
            .Messages
            .GetAsync((config) =>
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
            throw new System.NullReferenceException("Graph has not been initialized for user auth");

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
                new Recipient
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

    static KeyValuePair<K, Task<V>> TaskKVP<K, V>(K id, Task<V> task)
    {
        return new KeyValuePair<K, Task<V>>(id, task);
    }

    struct GroupPlans
    {
        public String groupId { get; init; }
        public String groupName { get; init; }
        public List<PlannerPlan> plans { get; set; }

        public GroupPlans(String id, String name)
        {
            groupId = id;
            groupName = name;
            plans = new List<PlannerPlan>();
        }

        public GroupPlans(String id, String name, List<PlannerPlan> plans)
        {
            groupId = id;
            groupName = name;
            this.plans = plans;
        }
    }

    static void PrintLine()
    {
        Console.WriteLine("--------------------------------------------------------------------------------");
    }

    struct GraphUser
    {
        public String name { get; init; }
        public String id { get; init; }
        public String mail { get; init; }

        public GraphUser(String name, String id, String mail)
        {
            this.name = name;
            this.id = id;
            this.mail = mail;
        }
    }

    async static Task<List<GraphUser>?> GetAllUsers()
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph client has not been initialized for user auth");
            return null;
        }

        var users = await _userClient.Users.GetAsync();

        if (users?.Value is null)
        {
            return null;
        }

        var ret = new List<GraphUser>();
        
        foreach (var u in users.Value)
        {
            if (u.DisplayName is not null && u.Id is not null && u.Mail is not null)
            {
                ret.Add(new GraphUser(u.DisplayName, u.Id, u.Mail)); 
            }
        }

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
    async static Task<List<GroupPlans>?> GetAllPlans()
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph client has not been initialized for user auth");
            return null;
        }
        
        var groups = await _userClient.Groups.GetAsync();
        
        if (groups?.Value is null)
        {
            Console.WriteLine("Unable to get Groups from Graph");
            return null;
        }
        
        var grplans = new List<KeyValuePair<(String, String), Task<PlannerPlanCollectionResponse?>>>(100);
        
        foreach (var g in groups.Value)
        {
            if (g.Id is null || g.DisplayName is null)
            {
                continue;
            }
            //put the tasks in the list, we are going to await them later
            //allows for multiple concurrent requests in flight
            grplans.Add(TaskKVP((g.Id, g.DisplayName),_userClient.Groups[g.Id].Planner.Plans.GetAsync()));
        }
        
        //var groupId = "Group ID";
        //var groupName = "Group Name";
        //var planCount = "Plan Count";
        //grr... C# why don't you have ^40 like Rust for centered alignment
        //var header = $"{groupId,-38} {groupName,-36} {planCount,-4}";
        //Console.WriteLine(header);
        
        //probably oversized, as not all groups will have a plan, oh well.
        List<GroupPlans> plansList = new List<GroupPlans>(grplans.Count);
        foreach (var g in grplans)
        {
            //Console.Write($"{g.Key.Item1, -38} {g.Key.Item2, -36} ");
            var val = await g.Value;
            if (val?.Value is null)
            {
                //Console.WriteLine("NULL");
                continue;
            }
        
            //Console.WriteLine($"{val.Value.Count}");
            if (val.Value.Count > 0)
            {
                plansList.Add(new GroupPlans(g.Key.Item1, g.Key.Item2, val.Value));
            }
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

    struct PlanBucket
    {
        public String name { get; init; }
        public String id { get; init; }

        public PlanBucket(String name, String id)
        {
            this.name = name;
            this.id = id;
        }
    }

    async static Task<List<PlanBucket>?> GetPlanBuckets(String planId)
    {
        if (_userClient is null)
        {
            Console.WriteLine("Can't get plan buckets, Graph client not init.");
            return null;
        }
        
        var buckets = await _userClient.Planner.Plans[planId].Buckets.GetAsync();

        if (buckets?.Value is null)
        {
            Console.WriteLine("Graph returned a null set of buckets");
            return null;
        }

        var ret = new List<PlanBucket>(buckets.Value.Count);
        foreach (var b in buckets.Value)
        {
            if (b.Name is not null && b.Id is not null)
            {
                ret.Add(new PlanBucket(b.Name, b.Id));
            }
        }

        return ret;
    }
    
    //the Graph API (v1) does not have the ability to create new planner plans
    //it can be done with the beta API, but I don't want to use that for this (beta API is subj to change)
    //I'd rather the "plans" be created by the user in the web UI
    //and then selected through tsync
    public async static Task MakeGraphCallAsync()
    {
        if (_userClient is null)
        {
            Console.WriteLine("Graph has not been initialized for user auth");
            return;
        }

        var plans = await GetAllPlans();

        var users = await GetAllUsers();


        if (plans is null || users is null)
        {
            Console.WriteLine("Missing required information from Graph API");
            return;
        }
        
        var planBuckets = new Dictionary<String, List<PlanBucket>>();
        
        foreach (var p in plans)
        {
            var buckets = new List<PlanBucket>();
            var bucketReqs = new List<Task<PlannerBucketCollectionResponse?>>();
            foreach (var p2 in p.plans)
            {
                if (p2.Id is null)
                {
                    continue;
                }
                bucketReqs.Add(_userClient.Planner.Plans[p2.Id].Buckets.GetAsync());
            }
            foreach (var b in bucketReqs)
            {
                var bucket = await b;
            
                if (bucket?.Value is not null)
                {
                    
                }
            }
        }
        
        

    }
}
