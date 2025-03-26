using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models.Documents
{
    public class DocumentOperation
    {
        [Key]
        public int OperationId { get; set; }

        public int DocumentId { get; set; }

        public int UserId { get; set; }

        public string OperationType { get; set; }

        public string OperationData { get; set; }

        public DateTime Timestamp { get; set; }

        [ForeignKey("DocumentId")]
        public CollabDoc Document { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
    }
}