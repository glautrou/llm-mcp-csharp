# llm-mcp-csharp

Démo accompagnant l'article « Exposer une Minimal API .NET 10 à un LLM avec MCP » : une API REST de gestion de tâches doublée d'un serveur MCP, construite par paliers.

| Tag | Contenu |
| --- | --- |
| `v0` | API REST + serveur MCP, sans authentification |
| `v1` | Confirmation de suppression via Multi Round-Trip Requests |
| `v2` | Authentification OAuth 2.0 avec Keycloak, scopes et identité |

## Prérequis

- SDK .NET 10
- Docker (à partir du palier `v2`, pour le serveur d'autorisation)

## Démarrer

Le serveur d'autorisation arrive préconfiguré : le realm, le scope et l'utilisateur de démonstration sont embarqués dans l'image et importés au premier démarrage. Aucune manipulation dans la console Keycloak n'est nécessaire.

```bash
docker compose up -d --build
```

```bash
dotnet run --project src
```

L'API REST répond sur `http://localhost:5000/api/todos`, le serveur MCP sur `http://localhost:5000/mcp`, et Keycloak sur `http://localhost:8081`.

### Comptes

| Usage | Identifiant | Mot de passe |
| --- | --- | --- |
| Utilisateur de démonstration | `demo` | `demo` |
| Second utilisateur, pour vérifier l'isolement | `demo2` | `demo2` |
| Console d'administration Keycloak | `admin` | `admin` |

Ce sont des identifiants de démonstration locale, sans valeur secrète.

Le second compte n'est pas là pour décorer : chaque utilisateur reçoit sa propre copie des tâches d'exemple à sa première visite, et ne voit jamais celles des autres. Se connecter avec `demo2` après avoir créé une tâche avec `demo` le montre en une manipulation.

## Ce que contient le realm

Le fichier [`keycloak/realm-todo-mcp.json`](keycloak/realm-todo-mcp.json) est un export Keycloak. Trois éléments comptent :

- **Le scope `Todos.ReadWrite`**, marqué comme scope par défaut du realm : tout client qui s'enregistre l'obtient automatiquement.
- **Un mappeur d'audience** attaché à ce scope, qui ajoute `todo-api` au claim `aud` des jetons d'accès. Sans lui, l'API rejetterait les jetons.
- **L'absence de la politique « Trusted Hosts »**, retirée du jeu de politiques d'enregistrement anonyme. C'est elle qui, par défaut, interdit l'enregistrement dynamique de clients. Ce choix convient à une démonstration ; en production, on la conserve et on restreint les hôtes autorisés, ou on exige un jeton d'accès initial.

## Vérifier

Sans jeton, l'API et le serveur MCP répondent `401` avec l'en-tête qui pointe vers les métadonnées de la ressource :

```bash
curl -i http://localhost:5000/api/todos
```

Les métadonnées, elles, sont publiques :

```bash
curl http://localhost:5000/.well-known/oauth-protected-resource/mcp
```

Le serveur d'autorisation annonce l'enregistrement dynamique de clients, ce qui dispense d'inscrire chaque client à l'avance :

```bash
curl -s http://localhost:8081/realms/todo-mcp/.well-known/openid-configuration
```

## Tester le serveur MCP

Avec [MCP Inspector](https://github.com/modelcontextprotocol/inspector) :

```bash
npx @modelcontextprotocol/inspector
```

Transport « Streamable HTTP », URL `http://localhost:5000/mcp`. Inspector lit les métadonnées publiées par le serveur, s'enregistre tout seul auprès de Keycloak et ouvre la page de connexion.

Avec Claude Code, le serveur est déjà déclaré dans [`.mcp.json`](.mcp.json) ; la connexion se fait par `/mcp`. Aucun identifiant de client à saisir, là encore grâce à l'enregistrement dynamique.

## Configuration de l'API

Tout est dans [`src/appsettings.json`](src/appsettings.json), sans secret :

```json
"Oidc": {
  "Authority": "http://localhost:8081/realms/todo-mcp",
  "Audience": "todo-api",
  "RequiredScope": "Todos.ReadWrite"
}
```
