namespace tsync;

public struct TOrg
{
    public string Id { get; init; }
    public string DisplayName { get; init; }
    public List<string> IdBoards { get; init; }
    public string DomainName { get; init; }
    public int MembersCount { get; init; }

    public TOrg(string id, string displayName, List<string> idBoards, string domainName, int membersCount)
    {
        Id = id;
        DisplayName = displayName;
        IdBoards = idBoards;
        DomainName = domainName;
        MembersCount = membersCount;
    }
}