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
    public async static Task MakeGraphCallAsync()
    {
        var groups = await _userClient.Groups.GetAsync();

        if (groups?.Value is null)
        {
            Console.WriteLine("Unable to get Groups from Graph");
            return;
        }
        
        var grplans = new List<KeyValuePair<(String, String), Task<PlannerPlanCollectionResponse?>>>(100);
        
        foreach (var g in groups.Value)
        {
            if (g.Id is null || g.DisplayName is null)
            {
                continue;
            }
            grplans.Add(TaskKVP((g.Id, g.DisplayName),_userClient.Groups[g.Id].Planner.Plans.GetAsync()));
        }

        var groupId = "Group ID";
        var groupName = "Group Name";
        var planCount = "Plan Count";
        //grr... C# why don't you have ^40 like Rust for centered alignment
        var header = $"{groupId,-38} {groupName,-36} {planCount,-4}";
        Console.WriteLine(header);
        foreach (var g in grplans)
        {
            Console.Write($"{g.Key.Item1, -38} {g.Key.Item2, -36} ");
            var val = await g.Value;
            if (val?.Value is null)
            {
                Console.WriteLine("NULL");
                continue;
            }
            Console.WriteLine($"{val.Value.Count}");
        }
    }
}
