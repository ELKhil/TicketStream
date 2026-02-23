using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TicketStream.Auth;
using TicketStream.Data;
using TicketStream.Entities;
using TicketStream.Services;
using TicketStream.Swagger;

var builder = WebApplication.CreateBuilder(args);

// =====================
// Contrôleurs API
// =====================

// Contrôleurs avec gestion des cycles JSON et sérialisation des enums en chaînes.
// DemandeStatusJsonConverter est enregistré en premier pour gérer "Terminé" avec accent ;
// JsonStringEnumConverter prend en charge les autres enums (ex: UserRole).
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.Converters.Add(new DemandeStatusJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// =====================
// Swagger / OpenAPI
// =====================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "TicketStream API",
        Version     = "v1",
        Description = "API for internal ticket management"
    });

    // Définition du schéma de sécurité JWT pour Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "Bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Entrez votre token JWT ici. Exemple : Bearer {votre_token}"
    });

    // Application du schéma JWT à toutes les routes protégées
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // Affiche "Terminé" (avec accent) dans le schéma Swagger de DemandeStatus
    c.SchemaFilter<DemandeStatusSchemaFilter>();
});

// =====================
// Base de données EF Core
// =====================

builder.Services.AddDbContext<TicketStreamContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =====================
// Authentification JWT (pour les contrôleurs API)
// =====================

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// =====================
// Blazor Server
// =====================

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Services scoped Blazor (durée de vie = circuit SignalR = session utilisateur)
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<UserState>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<ToastService>();

// Enregistre JwtAuthStateProvider comme implémentation de AuthenticationStateProvider
// Les deux résolutions partagent la même instance scoped (comportement attendu)
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<JwtAuthStateProvider>());

// Services API consommés par Blazor
builder.Services.AddScoped<AuthApiService>();
builder.Services.AddScoped<DemandeApiService>();
builder.Services.AddScoped<UserApiService>();
builder.Services.AddScoped<CommentaireApiService>();

// =====================
// HttpClient pour Blazor → API REST
// =====================

// Récupère l'URL de base depuis la configuration (appsettings.Development.json)
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7074/";

builder.Services.AddHttpClient("TicketStreamApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    // En développement, accepte le certificat auto-signé (dev cert).
    // À retirer en production (utiliser un certificat valide).
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

// =====================
// Construction de l'application
// =====================

var app = builder.Build();

// Test de connectivité à la base de données au démarrage
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketStreamContext>();
    try
    {
        if (db.Database.CanConnect())
            Console.WriteLine("✅ Connected to SQL Server!");
        else
            Console.WriteLine("❌ Cannot connect to SQL Server!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Cannot connect to SQL Server: {ex.Message}");
    }
}

// =====================
// Pipeline HTTP
// =====================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TicketStream API v1");
        // Déplacé de la racine "/" vers "/swagger" pour laisser Blazor occuper "/"
        c.RoutePrefix = "swagger";
    });
}

// UseHttpsRedirection uniquement en production (l'app tourne en HTTP en développement)
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles();   // Sert wwwroot/ (css/site.css, etc.)

// IMPORTANT : UseAuthentication doit être avant UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

// Routes API (prioritaires sur le fallback Blazor)
app.MapControllers();

// Routes Blazor
app.MapRazorPages();                    // _Host.cshtml accessible à "/"
app.MapBlazorHub();                     // Hub SignalR pour la connexion Blazor Server
app.MapFallbackToPage("/_Host");        // Toutes les autres routes → Blazor router

app.Run();
