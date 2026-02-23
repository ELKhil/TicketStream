using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketStream.Data;
using TicketStream.Entities;
using TicketStream.Models;

/// <summary>
/// Contrôleur responsable de la gestion des demandes (tickets).
/// Expose les opérations CRUD via l'API REST sous la route api/demandes.
/// Toutes les routes sont protégées par JWT ([Authorize]).
/// Le soft delete est utilisé : les demandes supprimées ne sont pas effacées
/// physiquement mais marquées avec une date de suppression (DeletedAt).
///
/// Permissions :
///   ROLE_AGENT → accès complet à toutes les demandes
///   ROLE_USER  → accès uniquement à ses propres demandes
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DemandesController : ControllerBase
{
    // Contexte EF Core injecté pour accéder à la base de données
    private readonly TicketStreamContext _context;

    /// <summary>
    /// Constructeur : reçoit le contexte EF Core par injection de dépendances.
    /// </summary>
    /// <param name="context">Contexte de la base de données TicketStream</param>
    public DemandesController(TicketStreamContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retourne la liste des demandes actives (non supprimées) avec filtres optionnels.
    ///
    /// ROLE_AGENT : voit toutes les demandes et peut filtrer par :
    ///   - status         : statut de la demande (EnAttente, EnCours, Termine)
    ///   - assignedAgentId: UUID de l'agent assigné (filtre sur un agent précis)
    ///   - isAssigned     : true = uniquement assignées, false = uniquement non assignées
    ///                      (ignoré si assignedAgentId est fourni)
    ///   - createdAt      : date de création (filtre sur le jour calendaire UTC exact)
    ///
    /// ROLE_USER : voit uniquement ses propres demandes et peut filtrer par :
    ///   - status         : statut de la demande
    ///
    /// Les filtres sont cumulables (ET logique).
    /// </summary>
    /// <param name="status">Filtre optionnel sur le statut (ROLE_AGENT et ROLE_USER)</param>
    /// <param name="assignedAgentId">Filtre optionnel sur l'agent assigné (ROLE_AGENT uniquement)</param>
    /// <param name="isAssigned">Filtre optionnel assignée/non assignée (ROLE_AGENT uniquement, ignoré si assignedAgentId est fourni)</param>
    /// <param name="createdAt">Filtre optionnel sur la date de création — jour entier UTC (ROLE_AGENT uniquement)</param>
    /// <returns>Liste filtrée des demandes selon le rôle et les paramètres fournis</returns>
    [HttpGet]
    public async Task<IEnumerable<Demande>> Get(
        [FromQuery] DemandeStatus? status = null,
        [FromQuery] Guid? assignedAgentId = null,
        [FromQuery] bool? isAssigned = null,
        [FromQuery] string tri = "recentes")
    {
        // Extraction des informations de l'utilisateur connecté depuis le token JWT
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        var query = _context.Demandes
            .Include(d => d.User)           // Utilisateur qui a créé la demande
            .Include(d => d.AssignedAgent)  // Agent assigné à la demande (peut être null)
            .Include(d => d.UpdatedBy)      // Dernier utilisateur ayant modifié la demande (peut être null)
            .Include(d => d.DeletedBy)      // Utilisateur ayant supprimé la demande (peut être null)
            .Where(d => d.DeletedAt == null                // Exclut les demandes supprimées
                     && (isAgent || d.UserId == userId));  // ROLE_USER ne voit que les siennes

        // Filtre par statut : accessible par tous les rôles
        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        // Filtres supplémentaires réservés à ROLE_AGENT
        if (isAgent)
        {
            // Filtre par agent assigné spécifique
            if (assignedAgentId.HasValue)
                query = query.Where(d => d.AssignedAgentId == assignedAgentId.Value);
            // Filtre assignée / non assignée (ignoré si un agent précis est fourni)
            else if (isAssigned.HasValue)
                query = isAssigned.Value
                    ? query.Where(d => d.AssignedAgentId != null)   // Uniquement assignées
                    : query.Where(d => d.AssignedAgentId == null);  // Uniquement non assignées
        }

        // Tri par date de création : "recentes" = plus récentes en premier (défaut), "anciennes" = plus anciennes en premier
        query = tri == "anciennes"
            ? query.OrderBy(d => d.CreatedAt)
            : query.OrderByDescending(d => d.CreatedAt);

        return await query.ToListAsync();
    }

    /// <summary>
    /// Retourne une demande spécifique par son UUID.
    /// ROLE_AGENT : peut accéder à n'importe quelle demande.
    /// ROLE_USER  : retourne 403 Forbidden si la demande ne lui appartient pas.
    /// Retourne 404 si la demande est introuvable ou supprimée.
    /// </summary>
    /// <param name="id">UUID unique de la demande</param>
    /// <returns>La demande correspondante, 403 Forbidden ou 404 NotFound</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Demande>> Get(Guid id)
    {
        // Extraction des informations de l'utilisateur connecté depuis le token JWT
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        var demande = await _context.Demandes
            .Include(d => d.User)           // Utilisateur qui a créé la demande
            .Include(d => d.AssignedAgent)  // Agent assigné à la demande (peut être null)
            .Include(d => d.UpdatedBy)      // Dernier utilisateur ayant modifié la demande (peut être null)
            .Include(d => d.DeletedBy)      // Utilisateur ayant supprimé la demande (peut être null)
            .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null);

        // Retourne 404 si la demande est introuvable ou supprimée
        if (demande == null) return NotFound();

        // ROLE_USER ne peut accéder qu'à ses propres demandes
        if (!isAgent && demande.UserId != userId) return Forbid();

        return demande;
    }

    /// <summary>
    /// Crée une nouvelle demande.
    /// Accessible par tous les rôles (ROLE_USER et ROLE_AGENT).
    /// L'UserId est automatiquement défini depuis le token JWT (l'utilisateur connecté).
    /// Un UUID est automatiquement généré comme identifiant de la demande.
    /// La date de création (CreatedAt) est définie côté serveur.
    /// Le statut par défaut est "En attente" (défini dans l'entité Demande).
    /// Retourne 201 Created avec la demande créée et l'URL de la ressource.
    /// </summary>
    /// <param name="demande">Objet demande reçu dans le corps de la requête</param>
    /// <returns>201 Created avec la demande créée et son URL</returns>
    [HttpPost]
    public async Task<ActionResult<DemandeDto>> Post(CreerDemandeDto dto)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var demande = new Demande
        {
            Title = dto.Title,
            Description = dto.Description,
            UserId = userId,          // ← obligatoire, c’est le user connecté
            User = null,              // ← éviter le tracking double
            AssignedAgentId = null,
            AssignedAgent = null,
            UpdatedById = null,
            UpdatedBy = null,
            DeletedById = null,
            DeletedBy = null,
            CreatedAt = DateTime.UtcNow,
            Status = DemandeStatus.EnAttente
        };

        _context.Demandes.Add(demande);
        await _context.SaveChangesAsync();

        var result = new DemandeDto
        {
            Id = demande.Id,
            Title = demande.Title,
            Description = demande.Description,
            Status = demande.Status.ToString(),
            UserId = userId,
            CreatedAt = demande.CreatedAt
        };

        return CreatedAtAction(nameof(Get), new { id = demande.Id }, result);
    }
    /// <summary>
    /// Met à jour une demande existante identifiée par son UUID.
    /// ROLE_AGENT : peut modifier n'importe quelle demande.
    /// ROLE_USER  : peut uniquement modifier ses propres demandes (403 sinon).
    /// Seuls les champs modifiables sont mis à jour : titre, description, statut,
    /// agent assigné et date d'assignation.
    /// La date de modification (UpdatedAt) et l'auteur (UpdatedById) sont définis
    /// automatiquement depuis le token JWT.
    /// Retourne 404 si introuvable ou supprimée, 403 si non autorisé, 204 en cas de succès.
    /// </summary>
    /// <param name="id">UUID de la demande à modifier</param>
    /// <param name="updatedDemande">Objet contenant les nouvelles valeurs</param>
    /// <returns>204 NoContent, 403 Forbidden ou 404 NotFound</returns>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, ModifierDemandeDto dto)
    {
        // Extraction des informations de l'utilisateur connecté depuis le token JWT
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        // Vérifie que la demande existe et n'est pas supprimée
        var demande = await _context.Demandes.FindAsync(id);
        if (demande == null || demande.DeletedAt != null) return NotFound();

        // ROLE_USER ne peut modifier que ses propres demandes
        if (!isAgent && demande.UserId != userId) return Forbid();

        // Mise à jour des champs métier
        // Titre et description : modifiables uniquement par le propriétaire de la demande.
        // Un agent qui n'est pas l'auteur peut seulement changer le statut et l'assignation.
        if (!isAgent || demande.UserId == userId)
        {
            demande.Title       = dto.Title;
            demande.Description = dto.Description;
        }

        // Statut et assignation : modifiables uniquement par un agent
        if (isAgent)
        {
            demande.Status          = dto.Status;
            demande.AssignedAgentId = dto.AssignedAgentId;
        }

        // AssignedAt : recalculé uniquement si un agent a modifié l'assignation
        if (isAgent)
        {
            demande.AssignedAt = dto.AssignedAgentId.HasValue
                ? dto.AssignedAt ?? DateTime.UtcNow
                : null;
        }

        // Traçabilité : enregistre qui a modifié la demande et quand (depuis le token JWT)
        demande.UpdatedAt   = DateTime.UtcNow;
        demande.UpdatedById = userId;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Supprime logiquement (soft delete) une demande par son UUID.
    /// ROLE_AGENT : peut supprimer n'importe quelle demande.
    /// ROLE_USER  : peut uniquement supprimer ses propres demandes (403 sinon).
    /// La demande n'est pas effacée physiquement : DeletedAt est renseigné avec la date
    /// actuelle et DeletedById avec l'UUID de l'utilisateur connecté (depuis JWT).
    /// Retourne 404 si introuvable ou déjà supprimée, 403 si non autorisé, 204 en cas de succès.
    /// </summary>
    /// <param name="id">UUID de la demande à supprimer</param>
    /// <returns>204 NoContent, 403 Forbidden ou 404 NotFound</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        // Extraction des informations de l'utilisateur connecté depuis le token JWT
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        // Vérifie que la demande existe et n'est pas déjà supprimée
        var demande = await _context.Demandes.FindAsync(id);
        if (demande == null || demande.DeletedAt != null) return NotFound();

        // ROLE_USER ne peut supprimer que ses propres demandes
        if (!isAgent && demande.UserId != userId) return Forbid();

        // Soft delete : marque la demande comme supprimée sans l'effacer de la base
        demande.DeletedAt = DateTime.UtcNow;
        demande.DeletedById = userId;

        await _context.SaveChangesAsync();

        return NoContent();
    }
}
