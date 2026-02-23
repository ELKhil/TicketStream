namespace TicketStream.Services
{
    /// <summary>
    /// Service scoped gérant les notifications toast affichées en haut à droite.
    /// Chaque toast disparaît automatiquement après la durée spécifiée.
    /// </summary>
    public class ToastService
    {
        // Événement déclenché à chaque ajout ou suppression d'un toast
        public event Action? OnChange;

        private readonly List<ToastMessage> _toasts = new();

        public IReadOnlyList<ToastMessage> Toasts => _toasts.AsReadOnly();

        /// <summary>
        /// Affiche un toast et le supprime automatiquement après dureeMsec millisecondes.
        /// </summary>
        /// <param name="message">Texte de la notification</param>
        /// <param name="type">"success", "danger" ou "warning"</param>
        /// <param name="dureeMsec">Durée d'affichage en millisecondes (défaut : 4000)</param>
        public async void Show(string message, string type = "success", int dureeMsec = 4000)
        {
            var toast = new ToastMessage(Guid.NewGuid(), message, type);
            _toasts.Add(toast);
            OnChange?.Invoke();

            await Task.Delay(dureeMsec);

            _toasts.Remove(toast);
            OnChange?.Invoke();
        }

        /// <summary>
        /// Ferme manuellement un toast par son identifiant.
        /// </summary>
        public void Fermer(Guid id)
        {
            _toasts.RemoveAll(t => t.Id == id);
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// Représente un message toast avec son identifiant, son texte et son type visuel.
    /// </summary>
    public record ToastMessage(Guid Id, string Message, string Type);
}
