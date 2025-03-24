using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models;

public class File
{
    [Key]
    public int FileId { get; set; }
    public int FolderId { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public DateTime UploadedDate { get; set; }

    [ForeignKey("FolderId")]
    public Folder Folder { get; set; }
}