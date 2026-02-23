using TicketStream.Models;

namespace TicketStream.Auth
{
    /// <summary>
    /// Service scoped qui conserve les informations de l'utilisateur connecté
    /// (chargées depuis l'API après la connexion). Permet d'afficher le nom,
    /// l'email et le rôle dans l'interface sans décoder le JWT manuellement.
    /// </summary>
    public class UserState
    {
        public Guid? Id { get; private set; }
        public string Name { get; private set; } = "";
        public string Email { get; private set; } = "";
        public string Role { get; private set; } = "";
        public bool IsLoaded { get; private set; }

        // Indique si l'utilisateur connecté est un agent
        public bool IsAgent => Role == "ROLE_AGENT";

        // Remplit l'état avec les données du profil chargé depuis l'API
        public void SetUser(UserDto user)
        {
            Id    = user.Id;
            Name  = user.Name;
            Email = user.Email;
            Role  = user.Role;
            IsLoaded = true;
        }

        // Réinitialise l'état lors de la déconnexion
        public void Clear()
        {
            Id    = null;
            Name  = "";
            Email = "";
            Role  = "";
            IsLoaded = false;
        }
    }
}
