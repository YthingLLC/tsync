namespace tsync;

public struct FileMeta
{
   public Guid FileID { get; init; } 
   
   public TAttachment AttachmentData { get; init; }
   
   public Boolean Complete { get; set; }

   public String? GraphUrl { get; set; }
   
   public String? Hash;
   
   public String OriginBoard { get; init; }

   public FileMeta(TAttachment attachmentData, String originBoard)
   {
      FileID = Guid.NewGuid();
      Complete = false;
      AttachmentData = attachmentData;
      OriginBoard = originBoard;
   }

   public FileMeta(Guid fileId, TAttachment attachmentData, String originBoard, Boolean complete = false, String? hash = null)
   {
      FileID = fileId;
      AttachmentData = attachmentData;
      Complete = complete;
      Hash = hash;
      OriginBoard = originBoard;
   }
}