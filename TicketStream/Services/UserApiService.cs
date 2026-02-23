using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TicketStream.Auth;
using TicketStream.Models;

namespace TicketStream.Services
{
    /// <summary>
    /// Service pour les appels API de gestion des utilisateurs.
    /// La liste complète est réservée à ROLE_AGENT.
    /// Le profil de l'utilisateur courant est accessible à tous les rôles.
    /// </summary>
    public class UserApiService
    {
        private readonly IHttpClientFactory _factory;
        private readonly TokenService _tokenService;
        private readonly AuthenticationStateProvider _authProvider;

        public UserApiService(
            IHttpClientFactory factory,
            TokenService tokenService,
            AuthenticationStateProvider authProvider)
        {
            _factory      = factory;
            _tokenService = tokenService;
            _authProvider = authProvider;
        }

        // Crée un HttpClient avec le token JWT dans le header Authorization
        private HttpClient Client
        {
            get
            {
                var client = _factory.CreateClient("TicketStreamApi");
                if (!string.IsNullOrEmpty(_tokenService.Token))
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenService.Token);
                return client;
            }
        }

        /// <summary>
        /// Retourne la liste des utilisateurs avec filtres optionnels (ROLE_AGENT uniquement).
        /// </summary>
        public async Task<List<UserDto>> GetUtilisateursAsync(
            bool?   actif = null,
            string? email = null,
            string? role  = null)
        {
            var url   = "api/users";
            var query = new List<string>();

            if (actif.HasValue)                   query.Add($"actif={actif.Value.ToString().ToLower()}");
            if (!string.IsNullOrEmpty(email))     query.Add($"email={Uri.EscapeDataString(email)}");
            if (!string.IsNullOrEmpty(role))      query.Add($"role={Uri.EscapeDataString(role)}");

            if (query.Count > 0) url += "?" + string.Join("&", query);

            return await Client.GetFromJsonAsync<List<UserDto>>(url) ?? new List<UserDto>();
        }

        /// <summary>
        /// Retourne uniquement les agents (ROLE_AGENT).
        /// Utilisé pour alimenter le filtre "agent assigné" et les selects d'assignation.
        /// </summary>
        public async Task<List<UserDto>> GetAgentsAsync()
            => await GetUtilisateursAsync(role: "ROLE_AGENT");

        /// <summary>
        /// Retourne le profil de l'utilisateur actuellement connecté (accessible à tous les rôles).
        /// Lit l'UUID depuis les claims du token JWT.
        /// </summary>
        public async Task<UserDto?> GetProfilAsync()
        {
            try
            {
                var state  = await _authProvider.GetAuthenticationStateAsync();
                var idStr  = state.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (idStr == null) return null;

                return await Client.GetFromJsonAsync<UserDto>($"api/users/{idStr}");
            }
            catch { return null; }
        }
    }
}
