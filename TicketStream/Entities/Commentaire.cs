namespace TicketStream.Entities
{
    public class Commentaire
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Contenu { get; set; } = null!;

        public Guid DemandeId { get; set; }
        public Demande Demande { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeletedAt { get; set; }
        public Guid? DeletedById { get; set; }
        public User? DeletedBy { get; set; }
    }
}
