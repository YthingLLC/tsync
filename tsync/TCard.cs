
namespace tsync;

public struct TCard
{
    public Boolean Closed => IsArchived;

    public Boolean IsArchived { get; init; }
    
    public String Id { get; init; }
    public String Name { get; init; }
    public String Description { get; init; }
    
    public DateTime Start { get; init; }
    
    public DateTime Due { get; init; }

    public List<TCardLabel> Labels { get; init; }
    
    public List<TAttachment> Attachments { get; init; }
    
    public List<TComment> Comments { get; init; }
    
    public List<TCheckList> CheckLists { get; init; }


    public TCard(String id, String name, String description, Boolean isArchived, DateTime start, DateTime due,
        List<TCardLabel> labels, List<TComment> comments, List<TCheckList> checkLists, List<TAttachment> attachments)
    {
        Id = id;
        Name = name;
        Description = description;
        IsArchived = isArchived;
        Start = start;
        Due = due;
        Labels = labels;
        Comments = comments;
        Attachments = attachments;
        CheckLists = checkLists;
    }

}