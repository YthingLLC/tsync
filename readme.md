### How this works

The general idea is this:

1. Download all of the cards from Trello
2. Download all of the attachments from Trello (actual files, not links)
3. Create equivalent Plans = Boards in planner
4. Map the boards to the plans
5. Upload the attachments from Trello to Graph via the mapping
6. Create the tasks in Planner


There are many other functions that this does, and it can save / load certain parts of state.
Pretty much everything except for the actual task creation can be saved and reloaded.

This can also completely erase all tasks in a Planner plan, so be careful.

This does not create any Planner plans. That is only supported by the beta Graph API.
I had enough issues with the v1.0 Graph API, that I don't want to try fooling around with the beta API.

The code for this is kind of ugly. I don't really like it, and I would completely redesign this if I had the time.

I created this tool as I have a client that needed to migrate from Trello to Planner.
This tool accomplished that goal, and to me, that's all that matters.

It will probably reliably do that again for anyone else that needs to do so, even if it is not the "best" way to do so.

It works. It migrates everything from Trello to MS Planner, and it successfully fights both of their APIs.

To me, that's a win. 

### Get started

1. Create an app in MS Entra (Azure AD) (make sure that allow public client flows is set to yes)
2. Get a Trello API Key and User Token
2. Fill out the appsettings.json info with the clientId and tenantId for Graph
3. Fill out the appsettings.json info with the trelloApiKey and user token
4. Fill out the download path (wherever you want this to be)
5. Leave the graphUserScopes alone
6. dotnet run
7. Login
8. 1 -> 4 -> 7 -> 10 -> 20 -> 22 -> 24 -> Done

### Extra bits

This is provided on an as-is basis with no warranty under the Apache License, Version 2.0. Similar to other open source software.

Ything LLC can provide consulting services to help you migrate your Trello Boards to MS Planner,
and I may be able to help with some basic troubleshooting, but this is not intended to be a "finished" product.

You will, probably, run into problems. You will, probably, also be able to complete a Trello to MS Planner migraiton.

I don't know of any other software that is available to do this.

Please do not expect this to be "easy to use" or "100% reliable." It's not. 

If it's useful for you though, please consider supporting development:

[Patreon](https://www.patreon.com/YthingLLC)

[Stripe](https://buy.stripe.com/aEU15SgTG5L09Hi9AA)

Bitcoin: bc1qvr605jye2dqlpyxpp33ttjwghmngas9g75hlwf

Ethereum: 0x0e664F5a8b193Be343BC2DC50b0C98B789eEAAf7

Monero: 49K1rUT5GPvRmuQ4zCzitodTsZ7zPH77n3GoP4Vxx8HTXTzD3UZ69MEYRdJ54BGcecLEFoxiq8B8tK3DwdKreqBJCcx2wmZ
