using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using ModelContextProtocol.AspNetCore.Authentication;
using TodoApi.Endpoints;
using TodoApi.Identity;
using TodoApi.Stores;
using TodoApi.Tools;

var builder = WebApplication.CreateBuilder(args);

var authority = Required("Oidc:Authority");
var audience = Required("Oidc:Audience");
var requiredScope = Required("Oidc:RequiredScope");
var serverUrl = builder.Configuration["ServerUrl"] ?? "http://localhost:5000";

string Required(string key) => builder.Configuration[key] is { Length: > 0 } value
    ? value
    : throw new InvalidOperationException($"Configuration « {key} » manquante. Voir le README.");

// URL canonique du serveur MCP, telle que l'utilisateur la saisit dans son
// client. Les métadonnées doivent la reprendre exactement, chemin compris.
var mcpResourceUrl = $"{serverUrl.TrimEnd('/')}/mcp";

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

        // Le Keycloak de démonstration est en HTTP sur la machine locale.
        // En production, l'autorité est en HTTPS et cette ligne disparaît.
        options.RequireHttpsMetadata = builder.Environment.IsProduction();

        // Sans cette ligne, ASP.NET Core renomme les claims entrants
        // (`sub` devient une longue URI de schéma). On garde les noms émis
        // par le serveur d'autorisation.
        options.MapInboundClaims = false;
    })
    .AddMcp(options =>
    {
        // Publié sur /.well-known/oauth-protected-resource : c'est ainsi que
        // le client MCP découvre où obtenir un jeton et pour quel scope.
        options.ResourceMetadata = new()
        {
            Resource = mcpResourceUrl,
            AuthorizationServers = { authority },
            ScopesSupported = [requiredScope],
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(CallerIdentity.TodosPolicy, policy =>
        policy.RequireAuthenticatedUser()
            // Le claim `scope` contient tous les scopes accordés dans une
            // seule chaîne séparée par des espaces : il faut l'éclater
            // plutôt que comparer la valeur entière.
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

// Derrière un proxy qui termine TLS, la requête arrive en HTTP clair.
// Sans ces en-têtes, l'application se croit en HTTP et publie des URL
// « http:// » dans l'en-tête WWW-Authenticate — que les clients refusent.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedFor,
};
// Le proxy a une adresse dynamique dans le réseau du conteneur : on ne peut
// pas la lister à l'avance. Acceptable ici parce que l'application n'est
// jamais exposée directement, uniquement à travers ce proxy.
forwardedHeaders.KnownNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

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
