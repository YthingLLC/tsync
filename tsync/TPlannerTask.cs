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


using System.Text.Json.Serialization;

namespace tsync;


//only exists because the Graph SDK sucks for creating tasks
//it doesn't work reliably, and random 400 errors are absolutely impossible to properly diagnose.
//the exception that is thrown contains precisely 0 information from the servers other than the HTTP response code of... 400.
//at least let me read what the actual error message from the server is! Why make this so difficult?
//and the even more fun HTTPClient timeouts, when seemingly identical requests over Graph Explorer work nearly instant?
//yeah, idk either.

//no PlannerTask can not be reused, it contains fields that Graph doesn't know what to do with.
//why? idk, ask Microsoft. They made it. Using an automated tool of course... it really shows
//I hate shitty SDK developers. 

//I almost feel like I would've been better off writing everything from scratch instead of trying to use
//these crappy SDKs  for this project. First Manatee.Trello sucked
//now the Graph SDK absolutely sucks too.

//SDK developers! Don't make your damn implementations so "opaque" as to not understand what the heck they're doing!
//the features I am still using from it I may re-implement. I did it in another project! I can do it again!
//but I will definitely remember this for next time! It's easier to just write the damn REST calls myself
//then try to get your stupid SDK to do the right thing!
public struct TPlannerTask
{
    [JsonPropertyName("@odata.etag")]
    public String? etag { get; init; }
    public String planId { get; init; }
    public String bucketId { get; init; }    
    public String title { get; init; }
    public String? conversationThreadId { get; init; }
    public String? id { get; init; }
    //don't even get me started on this stupidity either
    public String? orderHint { get; init; }
    
    //...I'm pretty sure you have to post details as a separate request
    //Graph does not seem to like if you give it everything in one shot
    //idk though, maybe I'm just stupid. I don't care though. It's Microsoft's servers that need to process it, not mine.
    //Request away!
    [JsonIgnore]
    public TPlannerTaskDetails? details { get; init; }
    
}


public struct TPlannerTaskDetails
{
    [JsonPropertyName("@odata.etag")]
    public String? etag { get; init; }
    //why? why microsoft? Why is this a separate field in details and not part of the main task?
    public String? description { get; init; }
    public Dictionary<String, TPlannerTaskExternalReference>? references { get; init; }
    public Dictionary<String, TPlannerTaskCheckItem>? checklist { get; init; }
    
    
}

public struct TPlannerTaskExternalReference
{
    
    [JsonPropertyName("@odata.type")]
    public String odatatype => "#microsoft.graph.plannerExternalReference";

    public String alias { get; init; }

    public String type => "Other";
    
    //Graph requests fail if this is provided on a PATCH request
    //so... just toss it for now?
    [JsonIgnore]
    public DateTimeOffset? lastModifiedDateTime { get; init; }
    
    //no, I am not going to support lastModifiedBy. I don't care.
}

//docs say that this should have a Guid generated by client
//but Microsoft's own impl just generates random ass numbers?
//I'm serious, here's a real response from Graph explorer on a checklistitem created from the web UI for planner!
//"checklist": {
//    "24544": {
//        "@odata.type": "#microsoft.graph.plannerChecklistItem",
//        "isChecked": false,
//        "title": "test 3",
//        "orderHint": "858496Xi",
//        "lastModifiedDateTime": "2024-01-15T22:55:50.8375149Z",
//        "lastModifiedBy": {
//            "user": {
//                "displayName": null,
//                "id": "a582e0a3-c5e2-483f-822f-0bce64bce193"
//            }
//        }
//    },
//see? 24544 is not a guid!
//lies I tell you!
//https://learn.microsoft.com/en-us/graph/api/resources/plannerchecklistitems?view=graph-rest-1.0
//..the interesting part though, the random ass numbers aren't even sequential! the first item is 99461!
public struct TPlannerTaskCheckItem
{
    [JsonPropertyName("@odata.type")]
    public String odatatype => "#microsoft.graph.plannerChecklistItem";
    
    public Boolean isChecked { get; init; }
    
    public String title { get; init; }
    
    public String? orderHint { get; init; }
    
    public DateTimeOffset? lastModifiedDateTime { get; init; }
    //again, I don't care about supporting lastModifiedBy
}



//I hate this, but it's the easiest way to get this to represent how Graph expects it...
//{
//"post": {
//    "body": {
//        "contentType": "",
//        "content": "content-value"
//    }
//}
//}

public struct TGroupThreadPostContainer
{
    public TGroupThreadPost post { get; init; }

    //for convenience... it's silly to need to do this, it really is.
    public TGroupThreadPostContainer(String content)
    {
        post = new TGroupThreadPost
        {
            body = new TGroupThreadPostBody
            {
                content = content
            }
        };
    }
}

public struct TGroupThreadPost
{
    public TGroupThreadPostBody body { get; init; }
}

public struct TGroupThreadPostBody
{
    //official docs have an empty string for contentType and no real definition on what values are acceptable
    //but from perusing around, it looks like "text" is the right answer
    //"html" also seems acceptable, but I'm not doing that.
    //https://learn.microsoft.com/en-us/graph/api/conversationthread-list-posts?view=graph-rest-1.0&tabs=http
    //https://learn.microsoft.com/en-us/graph/api/post-reply?view=graph-rest-1.0&tabs=http
    //https://learn.microsoft.com/en-us/graph/api/conversationthread-reply?view=graph-rest-1.0&tabs=http
    public String contentType => "text";
    
    public String content { get; init; }
    
}