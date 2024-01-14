namespace tsync;

public struct TMember
{
    public string Id { get; set; }
    public string FullName { get; set; }
    public string UserName { get; set; }

    public TMember(string id, string fullName, string userName)
    {
        Id = id;
        FullName = fullName;
        UserName = userName;
    }
}