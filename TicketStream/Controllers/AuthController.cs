using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TicketStream.Data;
using TicketStream.Entities;

/// <summary>
/// Contrôleur responsable de l'authentification des utilisateurs.
/// Expose les endpoints d'inscription et de connexion.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly TicketStreamContext _context;
    private readonly IConfiguration _config;

    /// <summary>
    /// Constructeur : reçoit le contexte EF Core et la configuration (JWT) par injection.
    /// </summary>
    public AuthController(TicketStreamContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    /// <summary>
    /// Inscrit un nouvel utilisateur dans le système.
    /// Un UUID est automatiquement généré comme identifiant unique.
    /// Le mot de passe est hashé avec PBKDF2 via ASP.NET Core PasswordHasher avant stockage.
    /// Retourne 400 si l'email est déjà utilisé.
    /// Retourne 201 avec le profil de l'utilisateur créé (sans le hash du mot de passe).
    /// </summary>
    /// <param name="request">Données d'inscription : nom, email, mot de passe, rôle</param>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        // Vérifie qu'aucun compte n'existe déjà avec cet email
        var exists = await _context.Users.AnyAsync(u => u.Email == request.Email);
        if (exists)
            return BadRequest(new { message = "Un compte avec cet email existe déjà." });

        // Crée l'utilisateur (l'UUID est généré automatiquement dans l'entité)
        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Role = request.Role,
            Actif = true
        };

        // PasswordHasher utilise PBKDF2 avec sel aléatoire (sécurisé)
        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Retourne l'utilisateur créé sans exposer le hash du mot de passe
        return CreatedAtAction(nameof(Register), new
        {
            id = user.Id,
            name = user.Name,
            email = user.Email,
            role = user.Role,
            actif = user.Actif
        });
    }

    /// <summary>
    /// Connecte un utilisateur et retourne un token JWT signé.
    /// Vérifie que le compte est actif, que l'email existe et que le mot de passe est correct.
    /// Le token contient les claims : userId (UUID), nom, rôle.
    /// Retourne 401 si les credentials sont invalides ou le compte inactif.
    /// </summary>
    /// <param name="request">Credentials de connexion : email et mot de passe</param>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        // Bloque l'accès si un token JWT valide est déjà présent dans la requête
        if (User.Identity?.IsAuthenticated == true)
            return BadRequest(new { message = "Vous êtes déjà connecté." });

        // Recherche l'utilisateur actif par email
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Actif);

        if (user == null)
            return Unauthorized(new { message = "Email ou mot de passe incorrect." });

        // Vérifie le mot de passe contre le hash stocké
        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Email ou mot de passe incorrect." });

        // Génère le token JWT avec les claims de l'utilisateur
        var token = GenererToken(user);

        return Ok(new { token });
    }

    /// <summary>
    /// Génère un token JWT signé contenant les informations de l'utilisateur.
    /// Le token contient : l'UUID, le nom et le rôle de l'utilisateur.
    /// La durée de validité est configurée dans appsettings.json (Jwt:ExpiresInMinutes).
    /// </summary>
    /// <param name="user">Utilisateur pour lequel générer le token</param>
    /// <returns>Token JWT sous forme de chaîne de caractères</returns>
    private string GenererToken(User user)
    {
        // Claims embarqués dans le token (accessibles via HttpContext.User dans les contrôleurs)
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),   // UUID de l'utilisateur
            new Claim(ClaimTypes.Name, user.Name),                       // Nom de l'utilisateur
            new Claim(ClaimTypes.Role, user.Role.ToString())             // Rôle : ROLE_USER ou ROLE_AGENT
        };

        // Clé de signature symétrique (HMAC-SHA256)
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Construction du token JWT
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresInMinutes"]!)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Données requises pour l'inscription d'un nouvel utilisateur.
/// </summary>
/// <param name="Name">Nom complet de l'utilisateur</param>
/// <param name="Email">Adresse email (doit être unique)</param>
/// <param name="Password">Mot de passe en clair (sera hashé avant stockage)</param>
/// <param name="Role">Rôle attribué : ROLE_USER ou ROLE_AGENT</param>
public record RegisterRequest(string Name, string Email, string Password, UserRole Role);

/// <summary>
/// Données requises pour la connexion d'un utilisateur existant.
/// </summary>
/// <param name="Email">Adresse email du compte</param>
/// <param name="Password">Mot de passe en clair</param>
public record LoginRequest(string Email, string Password);
