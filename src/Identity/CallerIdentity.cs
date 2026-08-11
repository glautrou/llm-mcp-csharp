using System.Security.Claims;

namespace TodoApi.Identity;

/// <summary>
/// Extraction de l'identité de l'appelant depuis le jeton validé.
/// L'identifiant du propriétaire n'est jamais un paramètre de tool ni un
/// champ de requête : il vient toujours des claims.
/// </summary>
public static class CallerIdentity
{
    /// <summary>Claim `sub` : identifiant stable de l'utilisateur dans le realm.</summary>
    public const string SubjectClaim = "sub";

    /// <summary>Claim `scope` : scopes accordés, séparés par des espaces.</summary>
    public const string ScopeClaim = "scope";

    /// <summary>Nom de la politique d'autorisation exigeant le scope de l'API.</summary>
    public const string TodosPolicy = "TodosAccess";

    /// <summary>Identifiant du propriétaire des tâches, lu dans le jeton.</summary>
    public static string GetOwnerId(this ClaimsPrincipal user) =>
        user.FindFirstValue(SubjectClaim)
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Le jeton ne contient pas d'identifiant d'utilisateur.");

    /// <summary>Nom lisible de l'appelant, pour les journaux.</summary>
    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue("preferred_username") ?? user.FindFirstValue("name") ?? "inconnu";
}
