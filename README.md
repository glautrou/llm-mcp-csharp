# llm-mcp-csharp

Démo accompagnant l'article « Exposer une Minimal API .NET 10 à un LLM avec MCP » : une API REST de gestion de tâches doublée d'un serveur MCP, construite par paliers.

| Tag | Contenu |
| --- | --- |
| `v0` | API REST + serveur MCP, sans authentification |
| `v1` | Confirmation de suppression via Multi Round-Trip Requests |
| `v2` | Authentification Microsoft Entra ID, scopes et identité |

## Prérequis

- SDK .NET 10
- Un tenant Microsoft Entra ID (à partir du palier `v2`)

## Lancer

```bash
dotnet run --project src
```

L'API REST répond sur `http://localhost:5000/api/todos`, le serveur MCP sur `http://localhost:5000/mcp`.

## Configurer Microsoft Entra ID (palier v2)

Deux inscriptions d'application sont nécessaires : une pour l'API, une pour le client MCP. Aucun identifiant réel ne doit être écrit dans le dépôt.

### 1. Inscrire l'API

Dans le [centre d'administration Entra](https://entra.microsoft.com), **Applications** > **Inscriptions d'applications** > **Nouvelle inscription**.

- Nom : `todo-mcp-api`
- Types de comptes : comptes de cet annuaire d'organisation uniquement
- Pas d'URI de redirection

Relever l'**ID d'application (client)** et l'**ID de l'annuaire (locataire)**.

### 2. Exposer le scope et déclarer l'URL du serveur MCP

Dans l'inscription de l'API, **Exposer une API** :

- Accepter l'URI d'ID d'application proposé : `api://<id-client-api>`
- **Ajouter une étendue** :
  - Nom : `Todos.ReadWrite`
  - Consentement : administrateurs et utilisateurs
  - Nom affiché : « Gérer les tâches »

Ajouter ensuite une **seconde URI d'ID d'application** : l'URL exacte du serveur MCP, chemin compris, sans barre oblique finale.

```
http://localhost:5000/mcp
```

Ce n'est pas facultatif pour Claude. Claude envoie l'URL du serveur MCP comme paramètre `resource` (RFC 8707) sur les requêtes d'autorisation et de jeton. Si cette valeur ne figure pas dans les URI d'ID d'application, Entra ID refuse d'émettre le jeton avec l'erreur `AADSTS9010010`. Ajouter aussi l'URL publique du serveur quand elle change (tunnel, hébergement).

L'application accepte donc deux audiences : `api://<id-client-api>` pour les clients qui demandent le scope, et l'URL du serveur MCP pour ceux qui envoient un `resource`.

### 3. Inscrire le client MCP

Le protocole MCP prévoit l'enregistrement dynamique des clients (DCR), mais Entra ID ne le prend pas en charge : les clients doivent être inscrits à l'avance.

**Nouvelle inscription** :

- Nom : `todo-mcp-client`
- Type de client public / natif
- URI de redirection — une seule inscription suffit pour tous les clients :
  - MCP Inspector : `http://localhost:6274/oauth/callback`
  - Claude web, application de bureau et mobile : `https://claude.ai/api/mcp/auth_callback`
  - Claude Code : redirection en boucle locale sur un port variable, d'où `http://localhost/callback` et `http://127.0.0.1/callback` (Entra ID ignore le port pour `127.0.0.1`)

Puis **Autorisations d'API** > **Ajouter une autorisation** > **Mes API** > `todo-mcp-api` > `Todos.ReadWrite`, et accorder le consentement administrateur si votre tenant l'exige.

Dans Claude, le connecteur se déclare avec **Paramètres avancés** > *OAuth Client ID*, où l'on saisit l'ID de `todo-mcp-client`. Le secret client reste vide : l'inscription est un client public.

### 4. Renseigner les secrets utilisateur

Les valeurs réelles vivent hors du dépôt, dans le magasin de secrets de développement :

```bash
dotnet user-secrets --project src set "AzureAd:TenantId" "<id-locataire>"
```

```bash
dotnet user-secrets --project src set "AzureAd:ClientId" "<id-client-api>"
```

`src/appsettings.json` ne contient que des emplacements vides : l'application refuse de démarrer si la configuration est absente.

### 5. Vérifier

Sans jeton, l'API et le serveur MCP répondent `401` avec l'en-tête qui pointe vers les métadonnées de la ressource :

```bash
curl -i http://localhost:5000/api/todos
```

Les métadonnées elles-mêmes sont publiques :

```bash
curl http://localhost:5000/.well-known/oauth-protected-resource
```

## Tester le serveur MCP

Avec [MCP Inspector](https://github.com/modelcontextprotocol/inspector) :

```bash
npx @modelcontextprotocol/inspector
```

Transport « Streamable HTTP », URL `http://localhost:5000/mcp`. À partir du palier `v2`, Inspector déclenche la connexion Entra ID à partir des métadonnées publiées par le serveur.
