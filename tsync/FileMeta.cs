namespace tsync;

public struct FileMeta
{
    public Guid FileID { get; init; }

    public TAttachment AttachmentData { get; init; }

    public bool Complete { get; set; }

    public string? GraphUrl { get; set; }

    public string? Hash;

    public string OriginBoard { get; init; }

    public FileMeta(TAttachment attachmentData, string originBoard)
    {
        FileID = Guid.NewGuid();
        Complete = false;
        AttachmentData = attachmentData;
        OriginBoard = originBoard;
    }

    public FileMeta(Guid fileId, TAttachment attachmentData, string originBoard, bool complete = false,
        string? hash = null)
    {
        FileID = fileId;
        AttachmentData = attachmentData;
        Complete = complete;
        Hash = hash;
        OriginBoard = originBoard;
    }
}