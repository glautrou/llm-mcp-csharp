using System.Collections.Concurrent;
using TodoApi.Models;

namespace TodoApi.Stores;

/// <summary>
/// Magasin de tâches en mémoire. Choix assumé pour la démo : les données
/// disparaissent au redémarrage de l'application.
/// </summary>
public sealed class InMemoryTodoStore
{
    private readonly ConcurrentDictionary<Guid, Todo> _todos = new();

    public InMemoryTodoStore()
    {
        // Quelques tâches de démonstration, dont une importante.
        Add(new Todo { Title = "Préparer la démo MCP", OwnerId = DemoUser.Id });
        Add(new Todo { Title = "Relire l'article avant publication", IsImportant = true, OwnerId = DemoUser.Id });
        Add(new Todo { Title = "Réserver la salle pour la revue de sprint", IsCompleted = true, OwnerId = DemoUser.Id });
    }

    public IReadOnlyCollection<Todo> GetAll(string ownerId) =>
        _todos.Values.Where(t => t.OwnerId == ownerId).OrderBy(t => t.CreatedAt).ToArray();

    public Todo? Get(Guid id, string ownerId) =>
        _todos.TryGetValue(id, out var todo) && todo.OwnerId == ownerId ? todo : null;

    public Todo Add(Todo todo)
    {
        _todos[todo.Id] = todo;
        return todo;
    }

    public Todo? Complete(Guid id, string ownerId)
    {
        // Boucle de mise à jour optimiste : TryUpdate échoue si la tâche
        // a été modifiée entre-temps par une autre requête.
        while (true)
        {
            if (Get(id, ownerId) is not { } existing)
            {
                return null;
            }

            var updated = existing with { IsCompleted = true };
            if (_todos.TryUpdate(id, updated, existing))
            {
                return updated;
            }
        }
    }

    public bool Delete(Guid id, string ownerId) =>
        Get(id, ownerId) is { } existing &&
        _todos.TryRemove(new KeyValuePair<Guid, Todo>(id, existing));
}
