namespace TicketStream.Entities
{
    /// <summary>
    /// Représente les états possibles d'une demande (ticket).
    /// La valeur stockée en base reste la chaîne française correspondante
    /// grâce au ValueConverter configuré dans le DbContext.
    /// </summary>
    public enum DemandeStatus
    {
        /// <summary>Demande créée, en attente de traitement.</summary>
        EnAttente,

        /// <summary>Demande prise en charge par un agent.</summary>
        EnCours,

        /// <summary>Demande traitée et clôturée.</summary>
        Termine
    }
}
