# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run (Swagger UI available at https://localhost:7074/)
dotnet run --project TicketStream/TicketStream.csproj

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --project TicketStream

# Apply pending migrations to the database
dotnet ef database update --project TicketStream
```

## Conventions de code

- **Langue des commentaires** : tous les commentaires doivent être rédigés **en français**.
- **Commentaires obligatoires** : chaque méthode et chaque action de contrôleur doit avoir un commentaire détaillé expliquant son rôle, ses paramètres et son comportement (ce qu'elle fait, ce qu'elle retourne, les cas particuliers).

## Architecture

**TicketStream** is an ASP.NET Core 7 Web API for internal ticket management, using Entity Framework Core with SQL Server.

### Project structure

```
TicketStream/              # Single project inside the solution
  Controllers/             # API controllers (direct DbContext injection, no service layer)
  Data/                    # TicketStreamContext (DbContext)
  Entities/                # EF Core entity classes
  Migrations/              # EF Core migration history
  Program.cs               # App bootstrap: DI, Swagger, EF, middleware
  appsettings.json         # Connection string (SQL Server localhost:1433)
```

### Domain model

Entities currently in code:

- **`User`** – `Id`, `Name`, `Email`, `Role` (`UserRole` enum: `ROLE_USER`/`ROLE_AGENT`), `Actif`
- **`Demande`** – Ticket with `Title`, `Description`, `Status` (`DemandeStatus` enum: `EnAttente`/`EnCours`/`Termine`, stored in DB as French strings via ValueConverter), FK `UserId` → `User`, FK `AssignedAgentId` → `User` (nullable), `AssignedAt`. Full audit trail: `CreatedAt`, `UpdatedAt/By`, `DeletedAt/By`.
- **`Commentaire`** – Commentaire lié à une `Demande` et un `User`. Champs : `Contenu`, `CreatedAt`, soft-delete (`DeletedAt`, `DeletedById`).
- **`DemandeStatus`** – Enum (`EnAttente`, `EnCours`, `Termine`). Stored in DB as `"En attente"`, `"En cours"`, `"Terminé"` via a `ValueConverter` in `TicketStreamContext`.

### Key patterns

- **Soft delete**: `Demande.DeletedAt` is `null` when active. All queries filter with `.Where(d => d.DeletedAt == null)`. DELETE sets `DeletedAt`/`DeletedById` instead of removing the row.
- **Audit trail**: `Demande` tracks who last updated and deleted it via nullable FK fields pointing back to `User`. `UpdatedById`/`DeletedById` are set from the JWT token of the authenticated user.
- **Enum storage**: Both `UserRole` and `DemandeStatus` are stored as strings in the database via `HasConversion` in `TicketStreamContext`.
- **Swagger at root**: In development, Swagger UI is served at `/` (`RoutePrefix = string.Empty`).

### Notes

- **`Historique`** entity has been abandoned. The `Historiques` table still exists in the database (from initial migrations) but is no longer managed by EF Core and is no longer referenced in any controller.
