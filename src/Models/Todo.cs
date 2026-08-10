namespace TodoApi.Models;

/// <summary>Une tâche de la liste de todos.</summary>
public sealed record Todo
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Title { get; init; }

    public bool IsCompleted { get; init; }

    public bool IsImportant { get; init; }

    public required string OwnerId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
