using TicketStream.Entities;

namespace TicketStream.Models
{
    /// <summary>
    /// DTO reçu par PUT /api/demandes/{id}.
    /// Contient uniquement les champs modifiables d'une demande.
    /// Le champ Status est désérialisé via DemandeStatusJsonConverter.
    /// </summary>
    public record ModifierDemandeDto(
        string        Title,
        string        Description,
        DemandeStatus Status,
        Guid?         AssignedAgentId,
        DateTime?     AssignedAt);
}
