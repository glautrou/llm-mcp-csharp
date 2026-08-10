using TodoApi.Endpoints;
using TodoApi.Stores;
using TodoApi.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<InMemoryTodoStore>();
builder.Services.AddOpenApi();

// Serveur MCP sur transport HTTP streamable.
// Dans le SDK 2.x, ce transport est stateless par défaut : chaque requête
// est autonome, aucune session à maintenir côté serveur.
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<TodoTools>();

var app = builder.Build();

app.MapOpenApi();

// L'API REST existante et le serveur MCP cohabitent dans la même application
// et partagent le même magasin de données.
app.MapTodoEndpoints();
app.MapMcp("/mcp");

app.Run();
