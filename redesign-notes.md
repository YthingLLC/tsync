### Why this sucks?

The MS Graph API and SDK are kind of... unreliable at times. The documentation does
not always tell you about all of the caveats that you will run into.

Such as... needing to make like 400 requests just to create a single task with comments,
attachments, and a description.

Why? You need to reference an etag for updating task details, because you're not able
to provide task details when creating a task. If you try to provide the details when you
create the task, the task creation will fail. So... after creating the task, you need
to request the details of the task, and use the etag of the details to update the details.

You also want to have comments? You need to make a conversation thread for the Group
that the planner plan is part of. Not a big deal right? 

Wrong. Very wrong.

You get the conversationThreadId back from Graph instantly, but you can't always use
that conversationThreadId to post more replies to right away. It can take up to 5 minutes
(from my testing) before the conversationThreadId can be used to post replies to!

Which, by the way, Microsoft's own people say [shouldn't be possible](https://learn.microsoft.com/en-us/answers/questions/1499714/why-does-a-request-to-a-conversation-thread-reply).

I mean, I guess it makes sense... Microsoft somehow managed to make Exchange a vital part of the infrastructure of MS Planner... /s

Or... my favorite... undocumented maximum character lengths that are completely nonsensical at times.

Or... even more fun... a maximum of 20 checklist items, and 10 attachments, on a single task. [Yes, really!](https://learn.microsoft.com/en-us/office365/planner/planner-limits)


### So now what?

From what I've learned building this, there are several things I would do differently.

1. Create all of the tasks
2. Store all of the tasks, including their initial conversation thread ID.
3. Store a list of tasks that contain comments (or just linq it out)
4. Store a list of tasks that contain attachments (or just linq it out)
5. After all tasks are created, then re-iterate through all of the tasks with comments.
6. Post all of the comments to their conversationThreadIds (they'll probably all be working by this point)
7. Re-iterate through all of the tasks that contain attachments.
8. Request the etag of the task details
9. Post the attachments to the task

This would, probably, simplify the code, and have the overall task creation speed increase.
Right now, this just waits when it's attempting a retry on a post to a conversation thread Id.

It would also simplify the logic a lot if the CreateTask() in GraphHelper.cs did a lot less

This does *work* though, and that's the important part. 

It being *kind of* slow is a bonus too. It let's you watch what's going on, as it's creating everything.

Makes it easier to spot when something goes wrong...




Also, I'd rewrite the whole thing in Rust. Ever since learning Rust... C# irritates me.
It "hides" too much of what it's really doing, and it really isn't very consistent at all.

Only reason I picked C# was to use the Graph and Trello SDKs... both of which I don't *really* use.

I use the Graph SDK to get a User token... which I could probably figure out how to do without the SDK.

The Trello SDK was complete garbage though. I don't even know why I wasted my time trying to make that thing work.

SDK developers really do overcomplicate the crap out of things in the name of being "clever"


### What I really want to do

Make a replacement to both Trello and MS Planner that doesn't have these silly limitations. With an API that doesn't suck.
