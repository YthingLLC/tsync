namespace tsync;

public struct TCommentData
{
    public String Text { get; init; }

    public TCommentData(String text)
    {
        Text = text;
    }
}

public struct TComment
{
    public String Id { get; init; }
    public TMember MemberCreator { get; init; }
    //only like this to allow for built in .NET JSON deserialization, otherwise this would be flattened
    public TCommentData Data { get; init; }

    public TComment(String id, TMember memberCreator, TCommentData data)
    {
        Id = id;
        MemberCreator = memberCreator;
        Data = data;
    }
}