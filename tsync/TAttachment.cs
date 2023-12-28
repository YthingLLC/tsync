namespace tsync;

public struct TAttachment
{
    public String Id { get; init; }
    
    //of the file
    public String Name { get; init; }
    
    //you tell me, Trello returns it separately
    public String FileName { get; init; }

    //for statistics, maybe there is a case they don't match? idfk
    public Boolean FileNamesMatch => Name.Equals(FileName, StringComparison.InvariantCulture);
    
    /// <summary>
    /// From the Trello API, maybe this will be different from actual? *shrugs*
    /// If this is null, then it is probably just a URL to something else
    /// </summary>
    public Int32 Bytes { get; init; }
    
    /// <summary>
    /// Appears to only be set to true for files stored in Trello
    /// </summary>
    public Boolean IsUpload { get; init; }
    
    //Maybe handle things different with this in the future? idk
    public String MimeType { get; init; }
    
    public DateTime Date { get; init; }
    
    //url to download the file, requires Authorization: OAuth header
    //https://community.developer.atlassian.com/t/download-attachments-with-api/72386/2?u=dsenk
    //curl -H "Authorization: OAuth oauth_consumer_key=\"{{key}}\", oauth_token=\"{{token}}\"" https://api.trello.com/1/cards/5e839f3696a55979a932b3ad/attachments/5edfd184387b678655b58348/download/my_image.png
    public String Url { get; init; }

    public TAttachment(String id, String name, String fileName, Int32 length, Boolean isUpload, String mimeType,
        DateTime date, String url)
    {
        Id = id;
        Name = name;
        FileName = fileName;
        Bytes = length;
        IsUpload = isUpload;
        MimeType = mimeType;
        Date = date;
        Url = url;
    }
}