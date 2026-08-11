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

**Nouvelle inscription** nommée `todo-mcp-client`. Une seule inscription suffit pour tous les clients : leurs URI de redirection s'y cumulent.

Les URI s'ajoutent dans **Authentification** > **Ajouter une plateforme** > **Applications mobiles et de bureau**. C'est bien cette plateforme qu'il faut choisir, même pour une URL `https://` : elle déclare un client *public*, qui obtient ses jetons par PKCE sans secret. Les plateformes **Web** (secret obligatoire) et **Application monopage** (jeton délivré uniquement à un navigateur, via CORS) ne conviennent pas.

Cocher ou saisir en URI personnalisées :

```
http://localhost:6274/oauth/callback
https://claude.ai/api/mcp/auth_callback
http://localhost:8080/callback
```

- la première pour MCP Inspector ;
- la deuxième pour Claude web, bureau et mobile ;
- la troisième pour Claude Code, qui écoute en boucle locale. Entra ID ignore le port des URI `localhost`, mais le déclarer explicitement évite toute ambiguïté. Utiliser `http://127.0.0.1:8080/callback` à la place obligerait à modifier le manifeste : le portail refuse le schéma `http` sur l'adresse IP littérale.

Puis **Autorisations d'API** > **Ajouter une autorisation** > **Mes API** > `todo-mcp-api` > `Todos.ReadWrite`, et accorder le consentement administrateur si votre tenant l'exige.

Relever l'**ID d'application (client)** de cette inscription : c'est lui que les clients MCP devront présenter, Entra ID ne sachant pas les enregistrer à la volée.

### 3 bis. Déclarer le client côté MCP

**Claude Code** accepte l'identifiant directement dans sa configuration, sans passer par une interface :

```bash
claude mcp add --transport http --client-id <id-client-mcp> --callback-port 8080 todo-api http://localhost:5000/mcp
```

Ce qui revient à écrire dans `.mcp.json` :

```json
{
  "mcpServers": {
    "todo-api": {
      "type": "http",
      "url": "http://localhost:5000/mcp",
      "oauth": { "clientId": "<id-client-mcp>", "callbackPort": 8080 }
    }
  }
}
```

**Claude web et bureau** n'ont pas de fichier de configuration équivalent : le connecteur s'ajoute par l'interface, dans **Paramètres** > **Connecteurs** > **Ajouter un connecteur personnalisé**, puis **Paramètres avancés** > *OAuth Client ID*. Le champ *OAuth Client Secret* reste vide, l'inscription étant un client public.

### 4. Renseigner les secrets utilisateur

Les valeurs réelles vivent hors du dépôt, dans le magasin de secrets de développement :

```bash
dotnet user-secrets --project src set "AzureAd:TenantId" "<id-annuaire-locataire>"
```

```bash
dotnet user-secrets --project src set "AzureAd:ClientId" "<id-application-de-todo-mcp-api>"
```

`AzureAd:ClientId` est l'ID d'application de l'inscription **API**, celle qui expose le scope — pas celui du client MCP.

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
