using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TicketStream.Entities;

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

            // Convertisseur enum DemandeStatus ↔ chaîne française stockée en base.
            // Cela permet de conserver les valeurs existantes ("En attente", "En cours", "Terminé")
            // sans modifier le schéma ni les données en base.
            var statusConverter = new ValueConverter<DemandeStatus, string>(
                v => v == DemandeStatus.EnAttente ? "En attente"
                   : v == DemandeStatus.EnCours   ? "En cours"
                                                  : "Terminé",
                v => v == "En attente" ? DemandeStatus.EnAttente
                   : v == "En cours"   ? DemandeStatus.EnCours
                                       : DemandeStatus.Termine);

            modelBuilder.Entity<Entities.Demande>()
                .Property(d => d.Status)
                .HasConversion(statusConverter);

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
