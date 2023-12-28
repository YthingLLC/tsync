namespace tsync;

public struct TMember
{
   public String Id { get; set; } 
   public String FullName { get; set; }
   public String UserName { get; set; }

   public TMember(String id, String fullName, String userName)
   {
      Id = id;
      FullName = fullName;
      UserName = userName;
   }
}