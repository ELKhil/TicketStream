using System.Net.Http.Json;
using TicketStream.Auth;
using TicketStream.Models;

namespace TicketStream.Services
{
    /// <summary>
    /// Service pour les appels API de gestion des commentaires.
    /// Ajoute automatiquement le header Authorization Bearer depuis TokenService.
    /// </summary>
    public class CommentaireApiService
    {
        private readonly IHttpClientFactory _factory;
        private readonly TokenService _tokenService;

        public CommentaireApiService(IHttpClientFactory factory, TokenService tokenService)
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
        /// Retourne les commentaires actifs (non supprimés) d'une demande spécifique.
        /// </summary>
        public async Task<List<CommentaireDto>> GetCommentairesAsync(Guid demandeId)
            => await Client.GetFromJsonAsync<List<CommentaireDto>>(
                   $"api/commentaires?demandeId={demandeId}") ?? new List<CommentaireDto>();

        /// <summary>
        /// Ajoute un commentaire sur une demande. Retourne null si succès, sinon le message d'erreur.
        /// </summary>
        public async Task<string?> AjouterCommentaireAsync(Guid demandeId, string contenu)
        {
            var response = await Client.PostAsJsonAsync("api/commentaires",
                new { Contenu = contenu, DemandeId = demandeId });
            return response.IsSuccessStatusCode ? null : "Erreur lors de l'ajout du commentaire.";
        }

        /// <summary>
        /// Supprime logiquement un commentaire. Retourne null si succès, sinon le message d'erreur.
        /// </summary>
        public async Task<string?> SupprimerCommentaireAsync(Guid id)
        {
            var response = await Client.DeleteAsync($"api/commentaires/{id}");
            return response.IsSuccessStatusCode ? null : "Erreur lors de la suppression.";
        }
    }
}
