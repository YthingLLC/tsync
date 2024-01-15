namespace tsync;

public struct TCommentData
{
    public string Text { get; init; }

    public TCommentData(string text)
    {
        Text = text;
    }
}

public struct TComment
{
    public string Id { get; init; }

    public DateTime Date { get; init; }

    public TMember MemberCreator { get; init; }

    //only like this to allow for built in .NET JSON deserialization, otherwise this would be flattened
    public TCommentData Data { get; init; }

    public TComment(string id, TMember memberCreator, TCommentData data)
    {
        Id = id;
        MemberCreator = memberCreator;
        Data = data;
    }

    public override string ToString()
    {
        return $"[tsync][{Date.ToString("u")}] {MemberCreator.FullName} ({MemberCreator.UserName}): {Data.Text}";
    }
}