namespace TicketStream.Models
{
    // DTO représentant un utilisateur retourné par l'API (sans le hash du mot de passe)
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public bool Actif { get; set; }
    }
}
