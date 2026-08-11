using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore.Authentication;
using TodoApi.Endpoints;
using TodoApi.Identity;
using TodoApi.Stores;
using TodoApi.Tools;

var builder = WebApplication.CreateBuilder(args);

// Valeurs à renseigner en secrets utilisateur (dotnet user-secrets), jamais en dur.
var tenantId = Required("AzureAd:TenantId");
var apiClientId = Required("AzureAd:ClientId");
var serverUrl = builder.Configuration["ServerUrl"] ?? "http://localhost:5000";

string Required(string key) => builder.Configuration[key] is { Length: > 0 } value
    ? value
    : throw new InvalidOperationException($"Configuration « {key} » manquante. Voir le README.");

var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
var audience = $"api://{apiClientId}";
var requiredScope = "Todos.ReadWrite";

builder.Services.AddAuthentication(options =>
    {
        // Le schéma MCP répond aux appels non authentifiés par un en-tête
        // WWW-Authenticate qui pointe vers les métadonnées de la ressource.
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;

        // Sans cette ligne, ASP.NET Core renomme les claims entrants
        // (`oid` devient une longue URI de schéma). On garde les noms émis
        // par Entra ID pour que le code parle le même langage que le jeton.
        options.MapInboundClaims = false;
    })
    .AddMcp(options =>
    {
        // Publié sur /.well-known/oauth-protected-resource : c'est ainsi que
        // le client MCP découvre où obtenir un jeton et pour quel scope.
        options.ResourceMetadata = new()
        {
            Resource = serverUrl,
            AuthorizationServers = { authority },
            ScopesSupported = [$"{audience}/{requiredScope}"],
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(CallerIdentity.TodosPolicy, policy =>
        policy.RequireAuthenticatedUser()
            // Entra ID publie les scopes délégués dans le claim `scp`, sous la
            // forme d'une seule chaîne séparée par des espaces : il faut donc
            // l'éclater plutôt que comparer la valeur entière.
            .RequireAssertion(context => context.User
                .FindAll(CallerIdentity.ScopeClaim)
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains(requiredScope)));
});

builder.Services.AddSingleton<InMemoryTodoStore>();
builder.Services.AddOpenApi();

// Serveur MCP sur transport HTTP streamable.
// Dans le SDK 2.x, ce transport est stateless par défaut : chaque requête
// est autonome, aucune session à maintenir côté serveur.
builder.Services.AddMcpServer()
    .WithHttpTransport()
    // Nécessaire pour que les attributs [Authorize] des tools soient appliqués.
    .AddAuthorizationFilters()
    .WithTools<TodoTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();

// L'API REST existante et le serveur MCP cohabitent dans la même application
// et partagent le même magasin de données.
app.MapTodoEndpoints();

// Exiger le jeton sur le endpoint MCP lui-même, et pas seulement sur chaque
// tool : c'est la réponse 401 accompagnée de l'en-tête WWW-Authenticate qui
// déclenche la découverte OAuth côté client.
app.MapMcp("/mcp").RequireAuthorization(CallerIdentity.TodosPolicy);

app.Run();
