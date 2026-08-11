using System.Security.Claims;
using TodoApi.Identity;
using TodoApi.Models;
using TodoApi.Stores;

namespace TodoApi.Endpoints;

/// <summary>Endpoints REST de l'API Todo : la partie « API existante » de la démo.</summary>
public static class TodoEndpoints
{
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/todos")
            .WithTags("Todos")
            // Même exigence que pour les tools MCP : jeton valide et scope attendu.
            .RequireAuthorization(CallerIdentity.TodosPolicy);

        group.MapGet("/", (ClaimsPrincipal user, InMemoryTodoStore store) =>
            store.GetAll(user.GetOwnerId()));

        group.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, InMemoryTodoStore store) =>
            store.Get(id, user.GetOwnerId()) is { } todo
                ? Results.Ok(todo)
                : Results.NotFound());

        group.MapPost("/", (CreateTodoRequest request, ClaimsPrincipal user, InMemoryTodoStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Title)] = ["Le titre est obligatoire."],
                });
            }

            var todo = store.Add(new Todo
            {
                Title = request.Title.Trim(),
                IsImportant = request.IsImportant,
                OwnerId = user.GetOwnerId(),
            });

            return Results.Created($"/api/todos/{todo.Id}", todo);
        });

        group.MapPost("/{id:guid}/complete", (Guid id, ClaimsPrincipal user, InMemoryTodoStore store) =>
            store.Complete(id, user.GetOwnerId()) is { } todo
                ? Results.Ok(todo)
                : Results.NotFound());

        group.MapDelete("/{id:guid}", (Guid id, ClaimsPrincipal user, InMemoryTodoStore store) =>
            store.Delete(id, user.GetOwnerId())
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }
}

/// <summary>Corps de la requête de création d'une tâche.</summary>
public sealed record CreateTodoRequest(string Title, bool IsImportant = false);
