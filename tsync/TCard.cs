namespace tsync;

public struct TCard
{
    public bool Closed => IsArchived;

    public bool IsArchived { get; init; }

    public string Id { get; init; }

    public string IdList { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }

    public DateTime? Start { get; init; }

    public DateTime? Due { get; init; }

    public List<TCardLabel> Labels { get; init; }

    public List<TAttachment> Attachments { get; init; }

    public List<TComment> Comments { get; init; }

    public List<TCheckList> CheckLists { get; init; }


    public TCard(string id, string idList, string name, string description, bool isArchived, DateTime start,
        DateTime due,
        List<TCardLabel> labels, List<TCheckList> checkLists, List<TAttachment> attachments)
    {
        Id = id;
        IdList = idList;
        Name = name;
        Description = description;
        IsArchived = isArchived;
        Start = start;
        Due = due;
        Labels = labels;
        Comments = new List<TComment>();
        Attachments = attachments;
        CheckLists = checkLists;
    }

    internal TCard(TCard card, List<TComment> comments)
    {
        Id = card.Id;
        IdList = card.IdList;
        Name = card.Name;
        Description = card.Description;
        IsArchived = card.IsArchived;
        Start = card.Start;
        Due = card.Due;
        Labels = card.Labels;
        Attachments = card.Attachments;
        CheckLists = card.CheckLists;
        Comments = comments;
    }
}