namespace TeamSyncWorkspace.Models
{
    public class Chat
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsGroup { get; set; }
        public int CreatedBy { get; set; }
        public int TeamId { get; set; }
        public ICollection<ChatMember> ChatMembers { get; set; }
        public ICollection<Message> Messages { get; set; }
    }
}
