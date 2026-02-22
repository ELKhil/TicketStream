namespace TicketStream.Entities
{
    public class Demande
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Status { get; set; } = "En attente"; // En attente, En cours, Terminé

        public Guid UserId { get; set; }       // FK vers User
        public User User { get; set; } = null!;

        public Guid? AssignedAgentId { get; set; }
        public User? AssignedAgent { get; set; }
        public DateTime? AssignedAt { get; set; }

        // 🔹 Propriétés audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedById { get; set; }    // FK vers User
        public User? UpdatedBy { get; set; }

        public DateTime? DeletedAt { get; set; }  // null = non supprimé
        public Guid? DeletedById { get; set; }    // FK vers User
        public User? DeletedBy { get; set; }      // null = non supprimé
    }
}
