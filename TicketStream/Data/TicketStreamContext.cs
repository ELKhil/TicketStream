using Microsoft.EntityFrameworkCore;

namespace TicketStream.Data
{
    public class TicketStreamContext : DbContext
    {
        public TicketStreamContext(DbContextOptions<TicketStreamContext> options)
         : base(options) { }

        public DbSet<Entities.User> Users { get; set; }
        public DbSet<Entities.Demande> Demandes { get; set; }
        public DbSet<Entities.Commentaire> Commentaires { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Stocker l'enum UserRole comme string en base
            modelBuilder.Entity<Entities.User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            // AssignedAgent : pas de cascade (nullable, évite les cycles)
            modelBuilder.Entity<Entities.Demande>()
                .HasOne(d => d.AssignedAgent)
                .WithMany()
                .HasForeignKey(d => d.AssignedAgentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Commentaire.User : pas de cascade (évite les cycles User → Demande → Commentaire)
            modelBuilder.Entity<Entities.Commentaire>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Commentaire.DeletedBy : pas de cascade
            modelBuilder.Entity<Entities.Commentaire>()
                .HasOne(c => c.DeletedBy)
                .WithMany()
                .HasForeignKey(c => c.DeletedById)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
