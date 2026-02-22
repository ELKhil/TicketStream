using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TicketStream.Data;

var builder = WebApplication.CreateBuilder(args);

// =====================
// Add services to the container
// =====================

// Contrôleurs avec gestion des cycles JSON (évite les boucles infinies de sérialisation)
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// Swagger / OpenAPI avec support du token JWT (bouton Authorize dans l'UI)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TicketStream API",
        Version = "v1",
        Description = "API for internal ticket management"
    });

    // Définition du schéma de sécurité JWT pour Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
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
});

// Base de données SQL Server via Entity Framework Core
builder.Services.AddDbContext<TicketStreamContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configuration de l'authentification JWT Bearer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,                          // Vérifie l'émetteur du token
            ValidateAudience = true,                        // Vérifie le destinataire du token
            ValidateLifetime = true,                        // Vérifie que le token n'est pas expiré
            ValidateIssuerSigningKey = true,                // Vérifie la signature du token
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Activation des politiques d'autorisation basées sur les rôles
builder.Services.AddAuthorization();

var app = builder.Build();

// =====================
// Test de connectivité à la base de données au démarrage
// =====================
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
// Configure the HTTP request pipeline
// =====================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TicketStream API v1");
        c.RoutePrefix = string.Empty; // Swagger à la racine: https://localhost:7074/
    });
}

app.UseHttpsRedirection();

// IMPORTANT : UseAuthentication doit être avant UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
