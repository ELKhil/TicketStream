# TicketStream

TicketStream est une API REST backend pour la gestion interne de demandes (tickets) avec assignation d'agents, commentaires et audit complet.
Développée en **.NET 7**, elle utilise **Entity Framework Core**, **SQL Server** et expose ses endpoints via **Swagger/OpenAPI** avec authentification **JWT**.

---

## Technologies utilisées

- **Backend** : .NET 7, C#
- **ORM** : Entity Framework Core 7
- **Base de données** : SQL Server
- **Authentification** : JWT Bearer (JSON Web Token)
- **Documentation API** : Swagger / OpenAPI (accessible à la racine `/`)
- **Containerisation** : Docker (optionnel pour SQL Server)

---

## Fonctionnalités

- Authentification JWT avec deux rôles : `ROLE_USER` et `ROLE_AGENT`
- Inscription et connexion des utilisateurs
- Création, modification et suppression (soft delete) de demandes
- Assignation des demandes à des agents avec horodatage
- Gestion des commentaires par demande
- Audit complet : `CreatedAt`, `UpdatedAt/By`, `DeletedAt/By`
- Contrôle d'accès par rôle sur tous les endpoints
- Documentation API interactive via Swagger UI avec support du token JWT

---

## Modèle de données

```
+--------------------------------------------------+
|                     User                         |
+--------------------------------------------------+
| Id : Guid                                        |
| Name : string                                    |
| Email : string                                   |
| Role : UserRole (ROLE_USER, ROLE_AGENT)          |
| Actif : bool                                     |
| PasswordHash : string                            |
+--------------------------------------------------+

+--------------------------------------------------+
|                    Demande                       |
+--------------------------------------------------+
| Id : Guid                                        |
| Title : string                                   |
| Description : string                             |
| Status : string (En attente / En cours / Terminé)|
|                                                  |
| UserId : Guid                                    |
| AssignedAgentId : Guid?                          |
| AssignedAt : DateTime?                           |
|                                                  |
| CreatedAt : DateTime                             |
| UpdatedAt : DateTime?                            |
| UpdatedById : Guid?                              |
| DeletedAt : DateTime?                            |
| DeletedById : Guid?                              |
+--------------------------------------------------+

+--------------------------------------------------+
|                   Commentaire                    |
+--------------------------------------------------+
| Id : Guid                                        |
| Contenu : string                                 |
|                                                  |
| DemandeId : Guid                                 |
| UserId : Guid                                    |
|                                                  |
| CreatedAt : DateTime                             |
| DeletedAt : DateTime?                            |
| DeletedById : Guid?                              |
+--------------------------------------------------+
```

### Relations

```
User 1 ──────────── 0..* Demande  (créateur)
User 1 ──────────── 0..* Demande  (agent assigné)
Demande 1 ────────── 0..* Commentaire
User 1 ──────────── 0..* Commentaire

Vue globale :
    User
     ├── crée → Demande
     ├── est assigné à → Demande
     └── écrit → Commentaire

    Demande
     └── contient → Commentaire
```

---

## Permissions par rôle

| Endpoint | ROLE_AGENT | ROLE_USER |
|---|---|---|
| `POST /api/auth/register` | Public | Public |
| `POST /api/auth/login` | Public | Public |
| `GET /api/users` | Tous les users | Non autorisé |
| `GET /api/users/{id}` | N'importe lequel | Le sien uniquement |
| `PUT /api/users/{id}` | N'importe lequel (+ rôle) | Le sien (nom, email, mdp) |
| `DELETE /api/users/{id}` | Désactivation (`Actif=false`) | Non autorisé |
| `GET /api/demandes` | Toutes | Ses demandes uniquement |
| `GET /api/demandes/{id}` | N'importe laquelle | La sienne uniquement |
| `POST /api/demandes` | Oui | Oui |
| `PUT /api/demandes/{id}` | N'importe laquelle | La sienne uniquement |
| `DELETE /api/demandes/{id}` | N'importe laquelle | La sienne uniquement |
| `GET /api/commentaires` | Tous | Ceux de ses demandes |
| `GET /api/commentaires/{id}` | N'importe lequel | Si demande lui appartient |
| `POST /api/commentaires` | Oui | Sur ses demandes uniquement |
| `DELETE /api/commentaires/{id}` | N'importe lequel | Le sien uniquement |

---

## Installation et démarrage

### Prérequis

- [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
- SQL Server (local ou via Docker)
- [EF Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) : `dotnet tool install --global dotnet-ef`

### Configuration

Modifier la chaîne de connexion dans `TicketStream/appsettings.json` :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=TicketStreamDb;User Id=sa;Password=VotreMotDePasse;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "VotreCléSecrèteMinimum32Caractères!",
    "Issuer": "TicketStream",
    "Audience": "TicketStreamUsers",
    "ExpiresInMinutes": 1440
  }
}
```

### Commandes

```bash
# Restaurer les dépendances et builder
dotnet build

# Appliquer les migrations et créer la base de données
dotnet ef database update --project TicketStream

# Lancer l'API (Swagger disponible sur https://localhost:7074/)
dotnet run --project TicketStream/TicketStream.csproj
```

### Migrations EF Core

```bash
# Créer une nouvelle migration
dotnet ef migrations add <NomMigration> --project TicketStream

# Appliquer les migrations en attente
dotnet ef database update --project TicketStream

# Supprimer la base de données (dev uniquement)
dotnet ef database drop --project TicketStream --force
```

---

## Utilisation de l'API

### 1. Inscription

```http
POST /api/auth/register
Content-Type: application/json

{
  "name": "John Doe",
  "email": "john@example.com",
  "password": "motdepasse",
  "role": "ROLE_USER"
}
```

### 2. Connexion — obtenir un token JWT

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "john@example.com",
  "password": "motdepasse"
}
```

Réponse :
```json
{ "token": "eyJhbGciOiJIUzI1NiIs..." }
```

### 3. Appels authentifiés

Ajouter le token dans le header de chaque requête :

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

Dans **Swagger UI** : cliquer sur le bouton **Authorize** en haut à droite et coller le token.

---

## Structure du projet

```
TicketStream/
  Controllers/          # Contrôleurs API (AuthController, UsersController,
  │                     #   DemandesController, CommentairesController)
  Data/                 # TicketStreamContext (DbContext EF Core)
  Entities/             # Entités EF Core (User, Demande, Commentaire, UserRole)
  Migrations/           # Historique des migrations EF Core
  Program.cs            # Bootstrap : DI, JWT, Swagger, EF Core, middleware
  appsettings.json      # Configuration (connexion SQL Server, JWT)
```
