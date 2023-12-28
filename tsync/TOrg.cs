namespace tsync;

public struct TOrg
{
    public String Id { get; init; }
    public String DisplayName { get; init; }
    public List<String> IdBoards { get; init; }
    public String DomainName { get; init; }
    public Int32 MembersCount { get; init; }

    public TOrg(String id, String displayName, List<String> idBoards, String domainName, Int32 membersCount)
    {
        Id = id;
        DisplayName = displayName;
        IdBoards = idBoards;
        DomainName = domainName;
        MembersCount = membersCount;
    }
    
}