using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketStream.Data;
using TicketStream.Entities;

/// <summary>
/// Contrôleur responsable de la gestion des commentaires liés aux demandes.
/// Toutes les routes sont protégées par JWT ([Authorize]).
/// Le soft delete est utilisé : les commentaires supprimés sont marqués
/// avec DeletedAt et DeletedById sans être effacés physiquement.
///
/// Permissions :
///   ROLE_AGENT → voit tous les commentaires, peut supprimer n'importe lequel
///   ROLE_USER  → voit uniquement les commentaires sur ses propres demandes,
///                peut uniquement supprimer ses propres commentaires
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CommentairesController : ControllerBase
{
    // Contexte EF Core injecté pour accéder à la base de données
    private readonly TicketStreamContext _context;

    /// <summary>
    /// Constructeur : reçoit le contexte EF Core par injection de dépendances.
    /// </summary>
    /// <param name="context">Contexte de la base de données TicketStream</param>
    public CommentairesController(TicketStreamContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retourne la liste des commentaires actifs (non supprimés).
    /// Accepte un filtre optionnel par demandeId via le query string (?demandeId=X).
    /// ROLE_AGENT : voit tous les commentaires (ou filtrés par demandeId).
    /// ROLE_USER  : voit uniquement les commentaires des demandes qui lui appartiennent.
    /// Charge les relations : auteur du commentaire, demande associée et suppresseur éventuel.
    /// </summary>
    /// <param name="demandeId">Filtre optionnel : UUID de la demande dont on veut les commentaires</param>
    /// <returns>Liste des commentaires selon le rôle et le filtre appliqué</returns>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? demandeId)
    {
        // Extraction des informations de l'utilisateur connecté depuis le token JWT
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        var query = _context.Commentaires
            .Include(c => c.User)       // Auteur du commentaire
            .Include(c => c.Demande)    // Demande associée au commentaire
            .Include(c => c.DeletedBy)  // Utilisateur ayant supprimé le commentaire (peut être null)
            .Where(c => c.DeletedAt == null); // Exclut les commentaires supprimés (soft delete)

        // ROLE_USER : filtre pour ne voir que les commentaires sur ses propres demandes
        if (!isAgent)
            query = query.Where(c => c.Demande.UserId == userId);

        // Filtre optionnel par UUID de demande
        if (demandeId.HasValue)
            query = query.Where(c => c.DemandeId == demandeId.Value);

        // Tri chronologique ascendant : les plus anciens commentaires en premier
        return Ok(await query.OrderBy(c => c.CreatedAt).ToListAsync());
    }

    /// <summary>
    /// Retourne un commentaire spécifique par son UUID.
    /// ROLE_AGENT : peut accéder à n'importe quel commentaire.
    /// ROLE_USER  : retourne 403 Forbidden si le commentaire appartient
    ///              à une demande qui ne lui appartient pas.
    /// Retourne 404 si le commentaire est introuvable ou supprimé.
    /// </summary>
    /// <param name="id">UUID unique du commentaire</param>
    /// <returns>Le commentaire correspondant, 403 Forbidden ou 404 NotFound</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        // Extraction des informations de l'utilisateur connecté depuis le token JWT
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        var commentaire = await _context.Commentaires
            .Include(c => c.User)       // Auteur du commentaire
            .Include(c => c.Demande)    // Demande associée au commentaire
            .Include(c => c.DeletedBy)  // Utilisateur ayant supprimé le commentaire (peut être null)
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);

        if (commentaire == null) return NotFound();

        // ROLE_USER ne peut accéder qu'aux commentaires sur ses propres demandes
        if (!isAgent && commentaire.Demande.UserId != userId)
            return Forbid();

        return Ok(commentaire);
    }

    /// <summary>
    /// Crée un nouveau commentaire sur une demande existante.
    /// Accessible par tous les rôles (ROLE_USER et ROLE_AGENT).
    /// ROLE_USER : ne peut commenter que ses propres demandes (403 sinon).
    /// L'UserId est automatiquement défini depuis le token JWT.
    /// La date de création (CreatedAt) est définie côté serveur.
    /// Retourne 404 si la demande cible est introuvable ou supprimée.
    /// Retourne 201 Created avec le commentaire créé.
    /// </summary>
    /// <param name="request">Données du commentaire : contenu et UUID de la demande</param>
    /// <returns>201 Created avec le commentaire, 403 Forbidden ou 404 NotFound</returns>
    [HttpPost]
    public async Task<IActionResult> Post(CreateCommentaireRequest request)
    {
        // Extraction des informations de l'utilisateur connecté depuis le token JWT
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        // Vérifie que la demande cible existe et n'est pas supprimée
        var demande = await _context.Demandes.FindAsync(request.DemandeId);
        if (demande == null || demande.DeletedAt != null)
            return NotFound(new { message = "La demande associée est introuvable ou supprimée." });

        // ROLE_USER ne peut commenter que ses propres demandes
        if (!isAgent && demande.UserId != userId)
            return Forbid();

        var commentaire = new Commentaire
        {
            Contenu = request.Contenu,
            DemandeId = request.DemandeId,
            UserId = userId,            // Défini depuis le token JWT, pas depuis le body
            CreatedAt = DateTime.UtcNow // Défini côté serveur
        };

        _context.Commentaires.Add(commentaire);
        await _context.SaveChangesAsync();

        // Retourne 201 avec l'URL vers la ressource créée (GET api/commentaires/{id})
        return CreatedAtAction(nameof(Get), new { id = commentaire.Id }, commentaire);
    }

    /// <summary>
    /// Supprime logiquement (soft delete) un commentaire par son UUID.
    /// ROLE_AGENT : peut supprimer n'importe quel commentaire.
    /// ROLE_USER  : peut uniquement supprimer ses propres commentaires (403 sinon).
    /// Le commentaire n'est pas effacé physiquement : DeletedAt est renseigné
    /// avec la date actuelle et DeletedById avec l'UUID de l'utilisateur connecté.
    /// Retourne 404 si introuvable ou déjà supprimé, 403 si non autorisé, 204 en cas de succès.
    /// </summary>
    /// <param name="id">UUID du commentaire à supprimer</param>
    /// <returns>204 NoContent, 403 Forbidden ou 404 NotFound</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        // Extraction des informations de l'utilisateur connecté depuis le token JWT
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        // Vérifie que le commentaire existe et n'est pas déjà supprimé
        var commentaire = await _context.Commentaires.FindAsync(id);
        if (commentaire == null || commentaire.DeletedAt != null)
            return NotFound();

        // ROLE_USER ne peut supprimer que ses propres commentaires
        if (!isAgent && commentaire.UserId != userId)
            return Forbid();

        // Soft delete : marque le commentaire comme supprimé sans l'effacer de la base
        commentaire.DeletedAt = DateTime.UtcNow;
        commentaire.DeletedById = userId;

        await _context.SaveChangesAsync();

        return NoContent();
    }
}

/// <summary>
/// Données requises pour la création d'un nouveau commentaire.
/// </summary>
/// <param name="Contenu">Texte du commentaire</param>
/// <param name="DemandeId">UUID de la demande à laquelle appartient le commentaire</param>
public record CreateCommentaireRequest(string Contenu, Guid DemandeId);
