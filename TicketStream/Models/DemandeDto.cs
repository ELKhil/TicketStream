namespace TicketStream.Models
{
    // DTO représentant une demande retournée par l'API.
    // Le champ Status est une chaîne : "EnAttente", "EnCours" ou "Terminé"
    // (sérialisé par DemandeStatusJsonConverter côté API).
    public class DemandeDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "EnAttente";

        public Guid UserId { get; set; }
        public UserDto? User { get; set; }

        public Guid? AssignedAgentId { get; set; }
        public UserDto? AssignedAgent { get; set; }
        public DateTime? AssignedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public UserDto? UpdatedBy { get; set; }
    }
}
