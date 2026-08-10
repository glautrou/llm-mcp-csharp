using System.ComponentModel;
using ModelContextProtocol.Server;
using TodoApi.Models;
using TodoApi.Stores;

namespace TodoApi.Tools;

/// <summary>
/// Tools MCP exposés au LLM. Noms et descriptions en français : c'est
/// l'interface que le modèle lit pour choisir quoi appeler.
/// </summary>
[McpServerToolType]
public sealed class TodoTools(InMemoryTodoStore store)
{
    [McpServerTool(Name = "lister_todos")]
    [Description("Liste toutes les tâches de l'utilisateur, terminées ou non.")]
    public IReadOnlyCollection<Todo> ListTodos() =>
        store.GetAll(DemoUser.Id);

    [McpServerTool(Name = "creer_todo")]
    [Description("Crée une nouvelle tâche dans la liste de l'utilisateur.")]
    public Todo CreateTodo(
        [Description("Titre de la tâche")] string title,
        [Description("Indique si la tâche est importante")] bool isImportant = false) =>
        store.Add(new Todo
        {
            Title = title.Trim(),
            IsImportant = isImportant,
            OwnerId = DemoUser.Id,
        });

    [McpServerTool(Name = "terminer_todo")]
    [Description("Marque une tâche comme terminée.")]
    public string CompleteTodo(
        [Description("Identifiant de la tâche")] Guid id) =>
        store.Complete(id, DemoUser.Id) is { } todo
            ? $"Tâche « {todo.Title} » marquée comme terminée."
            // Erreur métier : une phrase que le modèle peut comprendre et relayer.
            : "Aucune tâche ne porte cet identifiant.";

    [McpServerTool(Name = "supprimer_todo")]
    [Description("Supprime définitivement une tâche de la liste.")]
    public string DeleteTodo(
        [Description("Identifiant de la tâche")] Guid id) =>
        store.Delete(id, DemoUser.Id)
            ? "Tâche supprimée."
            : "Aucune tâche ne porte cet identifiant.";
}
