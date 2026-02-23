namespace TicketStream.Auth
{
    /// <summary>
    /// Service scoped (par circuit Blazor, donc par session utilisateur) qui conserve
    /// le token JWT en mémoire. Réinitialisé à chaque rechargement de page —
    /// la persistance entre rechargements est gérée par JwtAuthStateProvider via ProtectedLocalStorage.
    /// </summary>
    public class TokenService
    {
        private string? _token;

        // Token JWT de l'utilisateur connecté, null si non connecté
        public string? Token => _token;

        // Enregistre le token après connexion réussie
        public void SetToken(string token) => _token = token;

        // Efface le token lors de la déconnexion
        public void ClearToken() => _token = null;
    }
}
