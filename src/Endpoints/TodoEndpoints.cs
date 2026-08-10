using TodoApi.Models;
using TodoApi.Stores;

namespace TodoApi.Endpoints;

/// <summary>Endpoints REST de l'API Todo : la partie « API existante » de la démo.</summary>
public static class TodoEndpoints
{
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/todos").WithTags("Todos");

        group.MapGet("/", (InMemoryTodoStore store) =>
            store.GetAll(DemoUser.Id));

        group.MapGet("/{id:guid}", (Guid id, InMemoryTodoStore store) =>
            store.Get(id, DemoUser.Id) is { } todo
                ? Results.Ok(todo)
                : Results.NotFound());

        group.MapPost("/", (CreateTodoRequest request, InMemoryTodoStore store) =>
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
                OwnerId = DemoUser.Id,
            });

            return Results.Created($"/api/todos/{todo.Id}", todo);
        });

        group.MapPost("/{id:guid}/complete", (Guid id, InMemoryTodoStore store) =>
            store.Complete(id, DemoUser.Id) is { } todo
                ? Results.Ok(todo)
                : Results.NotFound());

        group.MapDelete("/{id:guid}", (Guid id, InMemoryTodoStore store) =>
            store.Delete(id, DemoUser.Id)
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }
}

/// <summary>Corps de la requête de création d'une tâche.</summary>
public sealed record CreateTodoRequest(string Title, bool IsImportant = false);
