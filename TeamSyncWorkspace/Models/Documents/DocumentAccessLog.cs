using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models.Documents
{
    public class DocumentAccessLog
    {
        [Key]
        public int LogId { get; set; }

        public int DocumentId { get; set; }

        public int? UserId { get; set; }

        public string IpAddress { get; set; }

        public string UserAgent { get; set; }

        public DateTime AccessTime { get; set; }

        [ForeignKey("DocumentId")]
        public CollabDoc Document { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
    }
}