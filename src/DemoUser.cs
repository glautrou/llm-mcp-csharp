namespace TodoApi;

/// <summary>
/// Utilisateur factice du palier 0 : sans authentification, toutes les tâches
/// appartiennent à cet identifiant. Sera remplacé par l'identité Entra ID
/// (ClaimsPrincipal) au palier 2.
/// </summary>
public static class DemoUser
{
    public const string Id = "demo-user";
}
