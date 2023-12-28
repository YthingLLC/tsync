curl -H "Authorization: OAuth oauth_consumer_key=\"$(jq -r '.settings .trelloApiKey'  < appsettings.json)\", oauth_token=\"$(jq -r '.settings .trelloUserToken' < appsettings.json)\"" --request GET --url "https://trello.com/1/cards/605095855c4a104dcd4610f3/attachments/6058ab4fe6056b0d42ba6102/download/Plainwell_MI.jpg" | sha256sum

echo c34c0587b47322a5de713c8cb577a8a4d38b7ca60962ef65587200bc6bb2d523  out.jpg

echo the above should match
