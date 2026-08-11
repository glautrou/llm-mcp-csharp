using System.Security.Claims;

namespace TodoApi.Identity;

/// <summary>
/// Extraction de l'identité de l'appelant depuis le jeton validé.
/// L'identifiant du propriétaire n'est jamais un paramètre de tool ni un
/// champ de requête : il vient toujours des claims.
/// </summary>
public static class CallerIdentity
{
    /// <summary>Claim `oid` : identifiant stable de l'utilisateur dans le tenant.</summary>
    public const string ObjectIdClaim = "oid";

    /// <summary>Claim `scp` : scopes délégués, séparés par des espaces chez Entra ID.</summary>
    public const string ScopeClaim = "scp";

    /// <summary>Nom de la politique d'autorisation exigeant le scope de l'API.</summary>
    public const string TodosPolicy = "TodosAccess";

    /// <summary>
    /// Identifiant du propriétaire des tâches. `oid` est préféré à `sub` :
    /// il est stable d'une application à l'autre pour un même utilisateur.
    /// </summary>
    public static string GetOwnerId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ObjectIdClaim)
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Le jeton ne contient pas d'identifiant d'utilisateur.");

    /// <summary>Nom lisible de l'appelant, pour les journaux.</summary>
    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue("name") ?? user.FindFirstValue("preferred_username") ?? "inconnu";
}
