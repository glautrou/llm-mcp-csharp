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
    private readonly ConcurrentDictionary<string, bool> _seededOwners = new();

    public IReadOnlyCollection<Todo> GetAll(string ownerId)
    {
        EnsureSeeded(ownerId);
        return _todos.Values.Where(t => t.OwnerId == ownerId).OrderBy(t => t.CreatedAt).ToArray();
    }

    public Todo? Get(Guid id, string ownerId)
    {
        EnsureSeeded(ownerId);
        return _todos.TryGetValue(id, out var todo) && todo.OwnerId == ownerId ? todo : null;
    }

    public Todo Add(Todo todo)
    {
        EnsureSeeded(todo.OwnerId);
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

    /// <summary>
    /// Crée les tâches de démonstration la première fois qu'un utilisateur
    /// accède à sa liste. Chaque utilisateur ne voit ainsi que ses propres
    /// tâches, sans configuration préalable.
    /// </summary>
    private void EnsureSeeded(string ownerId)
    {
        if (!_seededOwners.TryAdd(ownerId, true))
        {
            return;
        }

        foreach (var todo in new[]
        {
            new Todo { Title = "Préparer la démo MCP", OwnerId = ownerId },
            new Todo { Title = "Relire l'article avant publication", IsImportant = true, OwnerId = ownerId },
            new Todo { Title = "Réserver la salle pour la revue de sprint", IsCompleted = true, OwnerId = ownerId },
        })
        {
            _todos[todo.Id] = todo;
        }
    }
}
