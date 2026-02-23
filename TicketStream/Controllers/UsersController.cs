using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketStream.Data;
using TicketStream.Entities;

/// <summary>
/// Contrôleur responsable de la gestion des utilisateurs.
/// Toutes les routes sont protégées par JWT ([Authorize]).
///
/// Permissions :
///   ROLE_AGENT → accès complet (liste, consultation, modification, désactivation)
///   ROLE_USER  → peut uniquement consulter et modifier son propre profil
///                (nom, email, mot de passe) — ne peut pas modifier son rôle
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    // Contexte EF Core injecté pour accéder à la base de données
    private readonly TicketStreamContext _context;

    /// <summary>
    /// Constructeur : reçoit le contexte EF Core par injection de dépendances.
    /// </summary>
    /// <param name="context">Contexte de la base de données TicketStream</param>
    public UsersController(TicketStreamContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retourne la liste des utilisateurs avec filtres optionnels.
    /// Réservé à ROLE_AGENT uniquement.
    /// Le PasswordHash est exclu de la réponse pour des raisons de sécurité.
    ///
    /// Filtres disponibles (cumulables) :
    ///   - actif : true = uniquement les comptes actifs, false = uniquement les inactifs
    ///   - email : recherche partielle et insensible à la casse sur l'adresse email
    ///   - role  : filtre sur le rôle exact (ROLE_USER ou ROLE_AGENT)
    /// </summary>
    /// <param name="actif">Filtre optionnel sur le statut actif/inactif</param>
    /// <param name="email">Filtre optionnel par email (correspondance partielle)</param>
    /// <param name="role">Filtre optionnel par rôle exact</param>
    /// <returns>Liste filtrée des utilisateurs</returns>
    [HttpGet]
    [Authorize(Roles = "ROLE_AGENT")]
    public async Task<IActionResult> Get(
        [FromQuery] bool? actif = null,
        [FromQuery] string? email = null,
        [FromQuery] UserRole? role = null)
    {
        var query = _context.Users.AsQueryable();

        // Filtre par statut actif/inactif
        if (actif.HasValue)
            query = query.Where(u => u.Actif == actif.Value);

        // Filtre par email : correspondance partielle insensible à la casse
        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(u => u.Email.Contains(email));

        // Filtre par rôle exact
        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        // Sélectionne uniquement les champs nécessaires, sans exposer le hash du mot de passe
        var users = await query
            .Select(u => new { u.Id, u.Name, u.Email, u.Role, u.Actif })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Retourne un utilisateur spécifique par son UUID.
    /// ROLE_AGENT : peut accéder à n'importe quel profil.
    /// ROLE_USER  : peut uniquement accéder à son propre profil (403 sinon).
    /// Le PasswordHash est exclu de la réponse pour des raisons de sécurité.
    /// Retourne 404 si l'utilisateur est introuvable.
    /// </summary>
    /// <param name="id">UUID de l'utilisateur à consulter</param>
    /// <returns>Le profil utilisateur, 403 Forbidden ou 404 NotFound</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        // Extraction de l'UUID de l'utilisateur connecté depuis le token JWT
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        // ROLE_USER ne peut consulter que son propre profil
        if (!isAgent && currentUserId != id)
            return Forbid();

        var user = await _context.Users
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Name, u.Email, u.Role, u.Actif })
            .FirstOrDefaultAsync();

        if (user == null) return NotFound();

        return Ok(user);
    }

    /// <summary>
    /// Met à jour le profil d'un utilisateur existant.
    /// ROLE_AGENT : peut modifier n'importe quel utilisateur, y compris son rôle.
    /// ROLE_USER  : peut uniquement modifier son propre profil (nom, email, mot de passe).
    ///              Toute tentative de modification du rôle par un ROLE_USER est ignorée.
    /// Si un nouveau mot de passe est fourni, il est hashé avant stockage.
    /// Retourne 404 si l'utilisateur est introuvable, 403 si non autorisé.
    /// </summary>
    /// <param name="id">UUID de l'utilisateur à modifier</param>
    /// <param name="request">Nouvelles valeurs pour le profil</param>
    /// <returns>204 NoContent, 403 Forbidden ou 404 NotFound</returns>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, UpdateUserRequest request)
    {
        // Extraction de l'UUID de l'utilisateur connecté depuis le token JWT
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAgent = User.IsInRole("ROLE_AGENT");

        // ROLE_USER ne peut modifier que son propre profil
        if (!isAgent && currentUserId != id)
            return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Mise à jour des champs de base
        user.Name = request.Name;
        user.Email = request.Email;

        // Modification du rôle réservée à ROLE_AGENT uniquement
        if (isAgent)
            user.Role = request.Role;

        // Si un nouveau mot de passe est fourni, le hasher avant de le stocker
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, request.Password);
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Désactive un utilisateur (soft delete) en passant Actif à false.
    /// L'utilisateur reste en base de données mais ne peut plus se connecter.
    /// Réservé à ROLE_AGENT uniquement.
    /// Retourne 404 si l'utilisateur est introuvable, 400 s'il est déjà inactif.
    /// </summary>
    /// <param name="id">UUID de l'utilisateur à désactiver</param>
    /// <returns>204 NoContent, 400 BadRequest ou 404 NotFound</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ROLE_AGENT")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Vérifie que l'utilisateur n'est pas déjà désactivé
        if (!user.Actif)
            return BadRequest(new { message = "Cet utilisateur est déjà désactivé." });

        // Soft delete : désactive le compte sans supprimer l'enregistrement
        user.Actif = false;

        await _context.SaveChangesAsync();

        return NoContent();
    }
}

/// <summary>
/// Données requises pour la mise à jour d'un profil utilisateur.
/// </summary>
/// <param name="Name">Nouveau nom de l'utilisateur</param>
/// <param name="Email">Nouvelle adresse email</param>
/// <param name="Password">Nouveau mot de passe en clair (optionnel — ignoré si vide)</param>
/// <param name="Role">Nouveau rôle (ignoré si l'appelant est ROLE_USER)</param>
public record UpdateUserRequest(string Name, string Email, string? Password, UserRole Role);
