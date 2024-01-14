namespace tsync;

public struct FileMeta
{
   public Guid FileID { get; init; } 
   
   public TAttachment AttachmentData { get; init; }
   
   public Boolean Complete { get; set; }

   public String? GraphUrl { get; set; }
   
   public String? Hash;

   public FileMeta(TAttachment attachmentData)
   {
      FileID = Guid.NewGuid();
      Complete = false;
      AttachmentData = attachmentData;
   }

   public FileMeta(Guid fileId, TAttachment attachmentData, Boolean complete = false, String? hash = null)
   {
      FileID = fileId;
      AttachmentData = attachmentData;
      Complete = complete;
      Hash = hash;
   }
}