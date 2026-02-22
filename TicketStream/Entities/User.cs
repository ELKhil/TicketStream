namespace TicketStream.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public UserRole Role { get; set; } = UserRole.ROLE_USER;
        public bool Actif { get; set; } = true;
        public string PasswordHash { get; set; } = null!;
    }
}
