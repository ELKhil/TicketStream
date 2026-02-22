using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketStream.Data;
using TicketStream.Entities;

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
    /// Retourne la liste des demandes actives (non supprimées).
    /// ROLE_AGENT : voit toutes les demandes.
    /// ROLE_USER  : voit uniquement ses propres demandes (filtre sur UserId).
    /// Charge les relations : utilisateur demandeur, agent assigné,
    /// dernier modificateur et suppresseur éventuel.
    /// </summary>
    /// <returns>Liste des demandes selon le rôle de l'utilisateur connecté</returns>
    [HttpGet]
    public async Task<IEnumerable<Demande>> Get()
    {
        // Extraction des informations de l'utilisateur connecté depuis le token JWT
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        return await _context.Demandes
            .Include(d => d.User)           // Utilisateur qui a créé la demande
            .Include(d => d.AssignedAgent)  // Agent assigné à la demande (peut être null)
            .Include(d => d.UpdatedBy)      // Dernier utilisateur ayant modifié la demande (peut être null)
            .Include(d => d.DeletedBy)      // Utilisateur ayant supprimé la demande (peut être null)
            .Where(d => d.DeletedAt == null                      // Exclut les demandes supprimées
                     && (isAgent || d.UserId == userId))         // ROLE_USER ne voit que les siennes
            .ToListAsync();
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
    public async Task<ActionResult<Demande>> Post(Demande demande)
    {
        // Force l'UserId depuis le token JWT (ignore la valeur éventuellement fournie dans le body)
        demande.UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Définit la date de création côté serveur
        demande.CreatedAt = DateTime.UtcNow;

        _context.Demandes.Add(demande);
        await _context.SaveChangesAsync();

        // Retourne 201 avec l'URL vers la ressource créée (GET api/demandes/{id})
        return CreatedAtAction(nameof(Get), new { id = demande.Id }, demande);
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
    public async Task<IActionResult> Put(Guid id, Demande updatedDemande)
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
        demande.Title = updatedDemande.Title;
        demande.Description = updatedDemande.Description;
        demande.Status = updatedDemande.Status;
        demande.AssignedAgentId = updatedDemande.AssignedAgentId;

        // Si un agent est assigné : utilise la date fournie ou l'heure actuelle.
        // Si l'agent est retiré (null) : remet la date d'assignation à null.
        demande.AssignedAt = updatedDemande.AssignedAgentId.HasValue
            ? updatedDemande.AssignedAt ?? DateTime.UtcNow
            : null;

        // Traçabilité : enregistre qui a modifié la demande et quand (depuis le token JWT)
        demande.UpdatedAt = DateTime.UtcNow;
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
