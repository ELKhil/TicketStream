using System.Net.Http.Json;
using TicketStream.Auth;
using TicketStream.Models;

namespace TicketStream.Services
{
    /// <summary>
    /// Données de mise à jour d'une demande envoyées au PUT /api/demandes/{id}.
    /// Status utilise les valeurs JSON : "EnAttente", "EnCours", "Terminé".
    /// </summary>
    public record UpdateDemandeRequest(
        string Title,
        string Description,
        string Status,
        Guid? AssignedAgentId,
        DateTime? AssignedAt);

    /// <summary>
    /// Service pour les appels API de gestion des demandes (tickets).
    /// Ajoute automatiquement le header Authorization Bearer depuis TokenService.
    /// </summary>
    public class DemandeApiService
    {
        private readonly IHttpClientFactory _factory;
        private readonly TokenService _tokenService;

        public DemandeApiService(IHttpClientFactory factory, TokenService tokenService)
        {
            _factory      = factory;
            _tokenService = tokenService;
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
        /// Retourne la liste des demandes selon les filtres fournis.
        /// Le paramètre status utilise le nom de l'enum sans accent ("EnAttente", "EnCours", "Termine")
        /// car il est transmis via query string (liaison enum par nom, pas via le convertisseur JSON).
        /// </summary>
        public async Task<List<DemandeDto>> GetDemandesAsync(
            string? status          = null,
            Guid?   assignedAgentId = null,
            bool?   isAssigned      = null,
            string  tri             = "recentes")
        {
            var url   = "api/demandes";
            var query = new List<string>();

            if (!string.IsNullOrEmpty(status))    query.Add($"status={Uri.EscapeDataString(status)}");
            if (assignedAgentId.HasValue)         query.Add($"assignedAgentId={assignedAgentId}");
            if (isAssigned.HasValue)              query.Add($"isAssigned={isAssigned.Value.ToString().ToLower()}");
            query.Add($"tri={tri}");

            if (query.Count > 0) url += "?" + string.Join("&", query);

            return await Client.GetFromJsonAsync<List<DemandeDto>>(url) ?? new List<DemandeDto>();
        }

        /// <summary>
        /// Retourne une demande par son UUID. Retourne null si introuvable ou erreur.
        /// </summary>
        public async Task<DemandeDto?> GetDemandeAsync(Guid id)
        {
            try { return await Client.GetFromJsonAsync<DemandeDto>($"api/demandes/{id}"); }
            catch { return null; }
        }

        /// <summary>
        /// Crée une nouvelle demande. Retourne null si succès, sinon le message d'erreur.
        /// </summary>
        public async Task<string?> CreerDemandeAsync(string title, string description)
        {
            var response = await Client.PostAsJsonAsync("api/demandes",
                new { Title = title, Description = description });
            return response.IsSuccessStatusCode ? null : "Erreur lors de la création de la demande.";
        }

        /// <summary>
        /// Met à jour une demande existante. Retourne null si succès, sinon le message d'erreur.
        /// Le champ Status du request doit valoir "EnAttente", "EnCours" ou "Terminé" (avec accent)
        /// car il est transmis via le corps JSON et traité par DemandeStatusJsonConverter.
        /// </summary>
        public async Task<string?> ModifierDemandeAsync(Guid id, UpdateDemandeRequest request)
        {
            var response = await Client.PutAsJsonAsync($"api/demandes/{id}", request);
            return response.IsSuccessStatusCode ? null : "Erreur lors de la modification.";
        }

        /// <summary>
        /// Supprime logiquement une demande (soft delete). Retourne null si succès.
        /// </summary>
        public async Task<string?> SupprimerDemandeAsync(Guid id)
        {
            var response = await Client.DeleteAsync($"api/demandes/{id}");
            return response.IsSuccessStatusCode ? null : "Erreur lors de la suppression.";
        }
    }
}
