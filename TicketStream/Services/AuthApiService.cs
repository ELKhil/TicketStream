using System.Net.Http.Json;
using TicketStream.Auth;

namespace TicketStream.Services
{
    /// <summary>
    /// Service pour les appels API d'authentification (connexion, inscription, déconnexion).
    /// Orchestre la mise à jour de JwtAuthStateProvider et UserState après connexion.
    /// </summary>
    public class AuthApiService
    {
        private readonly IHttpClientFactory _factory;
        private readonly JwtAuthStateProvider _authProvider;
        private readonly UserState _userState;
        private readonly UserApiService _userApi;

        public AuthApiService(
            IHttpClientFactory factory,
            JwtAuthStateProvider authProvider,
            UserState userState,
            UserApiService userApi)
        {
            _factory      = factory;
            _authProvider = authProvider;
            _userState    = userState;
            _userApi      = userApi;
        }

        // Client HTTP sans token (utilisé pour les endpoints publics login/register)
        private HttpClient Client => _factory.CreateClient("TicketStreamApi");

        /// <summary>
        /// Connecte l'utilisateur : appelle POST /api/auth/login, enregistre le token JWT,
        /// puis charge le profil utilisateur via l'API.
        /// Retourne null si succès, sinon le message d'erreur à afficher.
        /// </summary>
        public async Task<string?> ConnecterAsync(string email, string password)
        {
            try
            {
                var response = await Client.PostAsJsonAsync("api/auth/login",
                    new { Email = email, Password = password });

                if (!response.IsSuccessStatusCode)
                {
                    var erreur = await response.Content.ReadFromJsonAsync<ErreurApi>();
                    return erreur?.Message ?? "Identifiants incorrects.";
                }

                var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
                if (result?.Token == null) return "Réponse invalide du serveur.";

                // Enregistre le token et met à jour l'état d'authentification Blazor
                await _authProvider.ConnecterAsync(result.Token);

                // Charge le profil de l'utilisateur connecté pour affichage dans l'interface
                var profil = await _userApi.GetProfilAsync();
                if (profil != null) _userState.SetUser(profil);

                return null; // null = succès
            }
            catch (Exception ex)
            {
                return $"Erreur de connexion : {ex.Message}";
            }
        }

        /// <summary>
        /// Inscrit un nouvel utilisateur via POST /api/auth/register.
        /// Retourne null si succès, sinon le message d'erreur.
        /// </summary>
        public async Task<string?> InscrireAsync(string name, string email, string password, string role = "ROLE_USER")
        {
            try
            {
                var response = await Client.PostAsJsonAsync("api/auth/register",
                    new { Name = name, Email = email, Password = password, Role = role });

                if (!response.IsSuccessStatusCode)
                {
                    var erreur = await response.Content.ReadFromJsonAsync<ErreurApi>();
                    return erreur?.Message ?? "Erreur lors de l'inscription.";
                }

                return null;
            }
            catch (Exception ex)
            {
                return $"Erreur : {ex.Message}";
            }
        }

        /// <summary>
        /// Déconnecte l'utilisateur : efface le token JWT et réinitialise l'état.
        /// </summary>
        public async Task DeconnecterAsync()
        {
            await _authProvider.DeconnecterAsync();
            _userState.Clear();
        }

        private record TokenResponse(string Token);
        private record ErreurApi(string Message);
    }
}
