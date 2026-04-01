# Configuration

## Fichier de configuration

Codengine recherche automatiquement un fichier de configuration dans le répertoire courant ou ses parents :
- `codengine.config.json`
- `codengine.json`
- `.codengine.json`

### Créer un fichier de configuration

```bash
codengine init
```

Cela crée un fichier `codengine.config.json` avec les valeurs par défaut.

## Structure du fichier de configuration

```json
{
  "sourcePath": ".",
  "includePatterns": [
    "**/*.cs"
  ],
  "excludePatterns": [
    "**/bin/**",
    "**/obj/**",
    "**/node_modules/**",
    "**/Migrations/**",
    "**/*.Designer.cs",
    "**/*.g.cs"
  ],
  "rules": {
    "COD001": { "enabled": true },
    "COD002": { "enabled": true },
    "COD003": { "enabled": true },
    "COD004": { "enabled": true },
    "COD005": { "enabled": true },
    "COD006": { "enabled": false },
    "COD007": { "enabled": true },
    "COD008": { "enabled": true },
    "COD009": { "enabled": true }
  },
  "reporting": {
    "format": "console",
    "outputPath": null,
    "verbose": false,
    "includeCodeSnippets": true
  },
  "failOnError": true,
  "failOnWarning": false,
  "maxConcurrency": 0,
  "oracle": {
    "connectionString": null,
    "schema": null,
    "outputDirectory": "./oracle_packages",
    "includePackageBodies": true,
    "includePatterns": [],
    "excludePatterns": ["SYS_*", "DBMS_*"]
  }
}
```

## Options de configuration

### Chemins et patterns

| Option | Type | Description |
|--------|------|-------------|
| `sourcePath` | string | Répertoire racine à analyser |
| `includePatterns` | string[] | Patterns glob des fichiers à inclure |
| `excludePatterns` | string[] | Patterns glob des fichiers à exclure |

### Configuration des règles

```json
{
  "rules": {
    "COD001": {
      "enabled": true,
      "severity": "error"
    },
    "COD006": {
      "enabled": false
    }
  }
}
```

| Option | Type | Description |
|--------|------|-------------|
| `enabled` | boolean | Activer/désactiver la règle |
| `severity` | string | Surcharger la sévérité (info, warning, error, critical) |

### Configuration du reporting

| Option | Type | Description |
|--------|------|-------------|
| `format` | string | Format de sortie : `console`, `json`, `html` |
| `outputPath` | string | Chemin du fichier de rapport |
| `verbose` | boolean | Afficher les suggestions de correction |
| `includeCodeSnippets` | boolean | Inclure les extraits de code |

### Options de build

| Option | Type | Description |
|--------|------|-------------|
| `failOnError` | boolean | Retourner code erreur si violations Error/Critical |
| `failOnWarning` | boolean | Retourner code erreur si violations Warning |
| `maxConcurrency` | int | Nombre de threads (0 = auto) |

### Configuration Oracle

| Option | Type | Description |
|--------|------|-------------|
| `connectionString` | string | Chaîne de connexion Oracle |
| `schema` | string | Schéma à analyser (défaut: utilisateur courant) |
| `outputDirectory` | string | Dossier de sortie des packages |
| `includePackageBodies` | boolean | Extraire les bodies |
| `includePatterns` | string[] | Patterns d'inclusion (ex: `PKG_*`) |
| `excludePatterns` | string[] | Patterns d'exclusion |
| `encoding` | string | Encodage des fichiers extraits (défaut: `utf-8`) |

## Priorité des options

Les options de la ligne de commande ont priorité sur le fichier de configuration :

```bash
# Désactive COD006 même s'il est activé dans le fichier de config
codengine analyze ./src -d COD006
```

## Exemples de configuration

### Projet minimal

```json
{
  "sourcePath": "./src",
  "rules": {
    "COD006": { "enabled": false }
  }
}
```

### Projet strict (CI/CD)

```json
{
  "sourcePath": "./src",
  "excludePatterns": [
    "**/bin/**",
    "**/obj/**",
    "**/Tests/**"
  ],
  "rules": {
    "COD001": { "enabled": true },
    "COD002": { "enabled": true },
    "COD005": { "enabled": true }
  },
  "reporting": {
    "format": "json",
    "outputPath": "./codengine-report.json"
  },
  "failOnError": true,
  "failOnWarning": true
}
```

### Projet avec Oracle

```json
{
  "sourcePath": "./src",
  "oracle": {
    "connectionString": "Data Source=//localhost:1521/ORCL;User Id=app_user;Password=****;",
    "schema": "APP_SCHEMA",
    "outputDirectory": "./plsql",
    "includePatterns": ["PKG_*"],
    "excludePatterns": ["PKG_TEST_*"],
    "encoding": "iso-8859-15"
  }
}
```

Pour utiliser cette configuration avec `extract-oracle`, passez le flag `--config` :

```bash
# Utilise toutes les valeurs de la section oracle
codengine extract-oracle --config

# Override du schéma uniquement
codengine extract-oracle --config -s AUTRE_SCHEMA
```

> **Note** : sans `--config`, la commande `extract-oracle` ignore le fichier de configuration et nécessite au minimum `--connection`.
```
