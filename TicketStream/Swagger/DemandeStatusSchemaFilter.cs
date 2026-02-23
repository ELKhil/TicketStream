using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using TicketStream.Entities;

namespace TicketStream.Swagger
{
    /// <summary>
    /// Filtre de schéma Swagger pour l'enum DemandeStatus.
    /// Remplace les valeurs générées automatiquement (entiers ou noms C#)
    /// par les chaînes réelles attendues par l'API, dont "Terminé" avec accent.
    /// </summary>
    public class DemandeStatusSchemaFilter : ISchemaFilter
    {
        /// <summary>
        /// Applique le filtre sur le schéma OpenAPI de DemandeStatus.
        /// Définit explicitement les valeurs affichées dans Swagger UI :
        /// "EnAttente", "EnCours", "Terminé".
        /// </summary>
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type != typeof(DemandeStatus)) return;

            // Remplace les valeurs auto-générées par les chaînes JSON réelles
            schema.Enum.Clear();
            schema.Enum.Add(new OpenApiString("EnAttente"));
            schema.Enum.Add(new OpenApiString("EnCours"));
            schema.Enum.Add(new OpenApiString("Terminé"));
        }
    }
}
