using System.Text.Json;
using System.Text.Json.Serialization;

namespace TicketStream.Entities
{
    /// <summary>
    /// Convertisseur JSON personnalisé pour l'enum DemandeStatus.
    /// Permet de sérialiser/désérialiser "Terminé" (avec accent) à la place de "Termine",
    /// contournant la contrainte C# qui interdit les accents dans les noms d'enum.
    /// Mapping :
    ///   EnAttente ↔ "EnAttente"
    ///   EnCours   ↔ "EnCours"
    ///   Termine   ↔ "Terminé"
    /// </summary>
    public class DemandeStatusJsonConverter : JsonConverter<DemandeStatus>
    {
        /// <summary>
        /// Désérialise la chaîne JSON vers la valeur enum correspondante.
        /// Lève une JsonException si la valeur est inconnue.
        /// </summary>
        public override DemandeStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return value switch
            {
                "EnAttente" => DemandeStatus.EnAttente,
                "EnCours"   => DemandeStatus.EnCours,
                "Terminé"   => DemandeStatus.Termine,
                _           => throw new JsonException($"Valeur de statut invalide : '{value}'. Valeurs acceptées : EnAttente, EnCours, Terminé.")
            };
        }

        /// <summary>
        /// Sérialise la valeur enum vers la chaîne JSON correspondante.
        /// "Termine" est sérialisé en "Terminé" (avec accent).
        /// </summary>
        public override void Write(Utf8JsonWriter writer, DemandeStatus value, JsonSerializerOptions options)
        {
            var str = value switch
            {
                DemandeStatus.EnAttente => "EnAttente",
                DemandeStatus.EnCours   => "EnCours",
                DemandeStatus.Termine   => "Terminé",
                _                       => throw new JsonException($"Valeur de statut non gérée : {value}")
            };
            writer.WriteStringValue(str);
        }
    }
}
