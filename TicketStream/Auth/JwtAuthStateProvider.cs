using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace TicketStream.Auth
{
    /// <summary>
    /// Fournisseur d'état d'authentification personnalisé pour Blazor Server.
    /// Lit le token JWT depuis TokenService (mémoire) et ProtectedLocalStorage (persistance navigateur).
    ///
    /// Flux :
    ///   1. GetAuthenticationStateAsync → utilise uniquement la mémoire (pas de JS interop)
    ///   2. InitialiserDepuisStockageAsync → appelé après le premier rendu (JS disponible),
    ///      restaure le token depuis le localStorage chiffré et notifie les composants
    ///   3. ConnecterAsync → enregistre le token en mémoire + stockage local
    ///   4. DeconnecterAsync → efface le token des deux emplacements
    /// </summary>
    public class JwtAuthStateProvider : AuthenticationStateProvider
    {
        private readonly TokenService _tokenService;
        private readonly ProtectedLocalStorage _storage;
        private bool _initialized;

        public JwtAuthStateProvider(TokenService tokenService, ProtectedLocalStorage storage)
        {
            _tokenService = tokenService;
            _storage = storage;
        }

        /// <summary>
        /// Retourne l'état d'authentification courant basé sur le token en mémoire.
        /// N'accède PAS au stockage local (JS interop non disponible lors du pré-rendu SSR).
        /// </summary>
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = _tokenService.Token;
            if (string.IsNullOrEmpty(token))
                return Task.FromResult(EtatAnonyme());

            try
            {
                var claims  = ExtrairesClaims(token);
                var identity = new ClaimsIdentity(claims, "jwt");
                return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
            }
            catch
            {
                return Task.FromResult(EtatAnonyme());
            }
        }

        /// <summary>
        /// Restaure le token depuis le localStorage chiffré du navigateur.
        /// Doit être appelé depuis OnAfterRenderAsync (JS disponible).
        /// Retourne true si un token valide a été trouvé et chargé.
        /// </summary>
        public async Task<bool> InitialiserDepuisStockageAsync()
        {
            if (_initialized) return !string.IsNullOrEmpty(_tokenService.Token);
            _initialized = true;

            try
            {
                var result = await _storage.GetAsync<string>("jwt_token");
                if (result.Success && !string.IsNullOrEmpty(result.Value))
                {
                    _tokenService.SetToken(result.Value);
                    NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                    return true;
                }
            }
            catch { /* JS interop non disponible ou stockage vide */ }

            return false;
        }

        /// <summary>
        /// Appelé après une connexion réussie : enregistre le token en mémoire
        /// et dans le localStorage chiffré, puis notifie les composants Blazor.
        /// </summary>
        public async Task ConnecterAsync(string token)
        {
            _tokenService.SetToken(token);
            try { await _storage.SetAsync("jwt_token", token); } catch { }
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        /// <summary>
        /// Appelé lors de la déconnexion : efface le token de la mémoire
        /// et du localStorage, puis notifie les composants Blazor.
        /// </summary>
        public async Task DeconnecterAsync()
        {
            _tokenService.ClearToken();
            try { await _storage.DeleteAsync("jwt_token"); } catch { }
            NotifyAuthenticationStateChanged(Task.FromResult(EtatAnonyme()));
        }

        // Retourne un état d'authentification pour un utilisateur non connecté
        private static AuthenticationState EtatAnonyme()
            => new(new ClaimsPrincipal(new ClaimsIdentity()));

        /// <summary>
        /// Extrait les claims du token JWT en appliquant le mappage standard
        /// des noms courts JWT ("sub", "name", "role") vers les types ClaimTypes ASP.NET Core.
        /// Le JWT est signé avec ClaimTypes.*, mappés en noms courts à l'émission.
        /// </summary>
        private static IEnumerable<Claim> ExtrairesClaims(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(token);
            var claims  = new List<Claim>();

            foreach (var claim in jwt.Claims)
            {
                // Applique le mappage entrant standard : "sub" → NameIdentifier, "role" → Role, etc.
                var type = JwtSecurityTokenHandler.DefaultInboundClaimTypeMap
                    .TryGetValue(claim.Type, out var mapped) ? mapped : claim.Type;
                claims.Add(new Claim(type, claim.Value));
            }

            return claims;
        }
    }
}
