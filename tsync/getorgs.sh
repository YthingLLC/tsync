curl --request GET --url "https://api.trello.com/1/members/me/organizations?key=$(jq -r '.settings .trelloApiKey'  < appsettings.json)&token=$(jq -r ".settings .trelloUserToken" < appsettings.json)" --header 'Accept: application/json' | jq ".[] | {id, displayName, idBoards, domainName, membersCount}"
