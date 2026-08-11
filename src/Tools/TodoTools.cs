using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TodoApi.Identity;
using TodoApi.Models;
using TodoApi.Stores;

namespace TodoApi.Tools;

/// <summary>
/// Tools MCP exposés au LLM. Noms et descriptions en français : c'est
/// l'interface que le modèle lit pour choisir quoi appeler.
/// </summary>
[McpServerToolType]
[Authorize(Policy = CallerIdentity.TodosPolicy)]
public sealed class TodoTools(InMemoryTodoStore store, ILogger<TodoTools> logger)
{
    [McpServerTool(Name = "lister_todos")]
    [Description("Liste toutes les tâches de l'utilisateur, terminées ou non.")]
    public IReadOnlyCollection<Todo> ListTodos(ClaimsPrincipal user) =>
        store.GetAll(user.GetOwnerId());

    [McpServerTool(Name = "creer_todo")]
    [Description("Crée une nouvelle tâche dans la liste de l'utilisateur.")]
    public Todo CreateTodo(
        ClaimsPrincipal user,
        [Description("Titre de la tâche")] string title,
        [Description("Indique si la tâche est importante")] bool isImportant = false) =>
        store.Add(new Todo
        {
            Title = title.Trim(),
            IsImportant = isImportant,
            OwnerId = user.GetOwnerId(),
        });

    [McpServerTool(Name = "terminer_todo")]
    [Description("Marque une tâche comme terminée.")]
    public string CompleteTodo(
        ClaimsPrincipal user,
        [Description("Identifiant de la tâche")] Guid id) =>
        store.Complete(id, user.GetOwnerId()) is { } todo
            ? $"Tâche « {todo.Title} » marquée comme terminée."
            // Erreur métier : une phrase que le modèle peut comprendre et relayer.
            : "Aucune tâche ne porte cet identifiant.";

    [McpServerTool(Name = "supprimer_todo")]
    [Description("Supprime une tâche de la liste. Appelez ce tool directement, sans demander de confirmation au préalable : si la tâche est importante, le serveur se chargera lui-même de demander une raison à l'utilisateur.")]
    public string DeleteTodo(
        McpServer server,
        RequestContext<CallToolRequestParams> context,
        ClaimsPrincipal user,
        [Description("Identifiant de la tâche")] Guid id,
        [Description("Raison de la suppression. À ne transmettre que si l'utilisateur l'a spontanément indiquée ; sinon, laisser vide et le serveur la demandera.")] string? reason = null)
    {
        var ownerId = user.GetOwnerId();

        // L'importance est TOUJOURS lue côté serveur, jamais reçue en paramètre :
        // un paramètre « estImportante » pourrait être menti par le modèle
        // pour contourner la confirmation.
        if (store.Get(id, ownerId) is not { } todo)
        {
            return "Aucune tâche ne porte cet identifiant.";
        }

        // Tâche ordinaire : raison facultative, suppression immédiate.
        if (!todo.IsImportant)
        {
            // Delete échoue si la tâche vient d'être supprimée par un autre appel.
            if (!store.Delete(id, ownerId))
            {
                return "Aucune tâche ne porte cet identifiant.";
            }

            logger.LogInformation("Tâche {TodoId} supprimée. Raison : {Reason}", id, reason ?? "(aucune)");
            return $"Tâche « {todo.Title} » supprimée.";
        }

        // Tâche importante : la raison doit venir de l'utilisateur.
        // Quatre scénarios selon les capacités du client :
        //  1. la raison est fournie d'emblée dans l'appel ;
        //  2. retour de round-trip : le client renvoie la réponse de l'utilisateur ;
        //  3. client MRTR : on suspend l'appel pour demander la raison ;
        //  4. client sans MRTR ni session : on guide le modèle.

        // (1) Raison fournie d'emblée.
        var confirmedReason = reason;

        // (2) Retour de round-trip : l'utilisateur a répondu à l'elicitation.
        if (string.IsNullOrWhiteSpace(confirmedReason) &&
            context.Params?.InputResponses?.TryGetValue("reason", out var response) is true)
        {
            ElicitResult? result;
            try
            {
                result = response.Deserialize(InputResponse.ElicitResultJsonTypeInfo);
            }
            catch (Exception ex)
            {
                // Erreur technique : le détail part dans les logs, le modèle
                // ne reçoit qu'un message neutre.
                logger.LogError(ex, "Réponse d'elicitation illisible pour la tâche {TodoId}", id);
                throw new McpException("La réponse de confirmation n'a pas pu être lue.");
            }

            // decline ou cancel : la tâche reste en place.
            if (result?.IsAccepted is not true)
            {
                return "Suppression annulée : l'utilisateur n'a pas confirmé.";
            }

            confirmedReason = result.Content?.TryGetValue("reason", out var value) is true
                ? value.GetString()
                : null;
        }

        // (1) ou (2) : une raison est disponible, on peut supprimer.
        if (!string.IsNullOrWhiteSpace(confirmedReason))
        {
            // Delete échoue si la tâche vient d'être supprimée par un autre appel.
            if (!store.Delete(id, ownerId))
            {
                return "Aucune tâche ne porte cet identifiant.";
            }

            logger.LogInformation(
                "Tâche importante {TodoId} supprimée par {User}. Raison : {Reason}",
                id, user.GetDisplayName(), confirmedReason);
            return $"Tâche importante « {todo.Title} » supprimée. Raison : {confirmedReason}";
        }

        // (3) Le client sait relayer une demande d'input : on suspend l'appel
        //     et la question part à l'utilisateur — pas au modèle.
        if (server.IsMrtrSupported)
        {
            throw new InputRequiredException(
                inputRequests: new Dictionary<string, InputRequest>
                {
                    ["reason"] = InputRequest.ForElicitation(new ElicitRequestParams
                    {
                        Message = $"« {todo.Title} » est une tâche importante. Confirmez sa suppression en indiquant une raison.",
                        RequestedSchema = new()
                        {
                            Properties =
                            {
                                ["reason"] = new ElicitRequestParams.StringSchema
                                {
                                    Title = "Raison de la suppression",
                                    Description = "Pourquoi supprimer cette tâche importante ?",
                                },
                            },
                        },
                    }),
                },
                requestState: id.ToString());
        }

        // (4) Aucun canal vers l'utilisateur : on explique au modèle quoi faire.
        return $"« {todo.Title} » est une tâche importante : sa suppression exige une raison. " +
               "Demandez la raison à l'utilisateur puis rappelez supprimer_todo avec le paramètre reason.";
    }
}
