namespace TicketStream.Models
{
    // DTO représentant un commentaire retourné par l'API
    public class CommentaireDto
    {
        public Guid Id { get; set; }
        public string Contenu { get; set; } = "";
        public Guid DemandeId { get; set; }
        public Guid UserId { get; set; }
        public UserDto? User { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
