# Codengine

Analyseur de code statique pour C# (.NET Framework 4.8 / .NET 9) et PL/SQL Oracle.

> **Documentation complète** : Voir le dossier [`docs/`](./docs/INDEX.md)

## Installation

### Prérequis

- **.NET SDK 9.0** — installer via winget :

```bash
winget install Microsoft.DotNet.SDK.9
```

### Installer Codengine

```bash
git clone https://github.com/VVAILLANT/codengine.git
cd codengine
dotnet pack src/Codengine.Cli -o src/Codengine.Cli/nupkg
dotnet tool install --global --add-source src/Codengine.Cli/nupkg Codengine.Cli
```

### Vérifier l'installation

```bash
codengine list-rules
```

### Mise à jour

```bash
cd codengine
git pull
bash scripts/publish-tool.sh
```

### Désinstallation

```bash
dotnet tool uninstall --global Codengine.Cli
```

## Utilisation

### Analyser du code C#

```bash
# Analyser le répertoire courant
codengine analyze

# Analyser un répertoire spécifique
codengine analyze ./src

# Avec rapport JSON
codengine analyze ./src -f json -o rapport.json

# Avec rapport HTML
codengine analyze ./src -f html -o rapport.html

# Désactiver certaines règles
codengine analyze ./src -d COD001,COD006

# Mode verbose (affiche les suggestions)
codengine analyze ./src -v
```

### Corriger automatiquement

```bash
# Appliquer les corrections
codengine fix ./src

# Mode dry-run (voir les corrections sans les appliquer)
codengine fix ./src --dry-run

# Corriger seulement certaines règles
codengine fix ./src -r COD001,COD005
```

### Extraire les packages Oracle

```bash
# Extraire tous les packages d'un schéma
codengine extract-oracle -c "Data Source=//host:1521/SID;User Id=user;Password=pass;"

# Avec schéma spécifique
codengine extract-oracle -c "..." -s MY_SCHEMA -o ./packages

# Filtrer les packages
codengine extract-oracle -c "..." -i "PKG_*" -e "PKG_TEST_*"

# En récupérant les paramètres du fichier de configuration (codengine.config.json)
codengine extract-oracle --config

# En récupérant les paramètres du fichier de configuration + surcharge de paramètres
codengine extract-oracle --config -c "Data Source=//host:1521/SID;User Id=user;Password=pass;"
```

### Formater le code PL/SQL

```bash
# Formater les fichiers .sql du répertoire configuré (oracle.outputDirectory)
codengine format-oracle --config

# Formater un répertoire spécifique
codengine format-oracle C:\Projects\GIT\MROAD\Database\PACKAGE

# Prévisualiser les changements sans modifier les fichiers
codengine format-oracle --config --dry-run

# Créer un backup (.bak) avant modification
codengine format-oracle --config --backup

# Personnaliser l'indentation
codengine format-oracle --config --indent-size 2

# Désactiver la mise en majuscules des mots-clés
codengine format-oracle --config --uppercase-keywords false

# Choisir le moteur de formatage (basic, sqlformatternet, combined)
codengine format-oracle --config --engine combined
```

### Autres commandes

```bash
# Lister les règles disponibles
codengine list-rules

# Créer un fichier de configuration
codengine init
```

## Règles disponibles

| ID | Nom | Catégorie | Sévérité | Description |
|----|-----|-----------|----------|-------------|
| COD001 | NullCheckAfterSingleOrDefault | NullSafety | Error | Vérifier null après SingleOrDefault()/FirstOrDefault() |
| COD002 | EmptyListBeforeContains | NullSafety | Error | Vérifier liste non null/vide avant Contains() dans Where() |
| COD003 | AsyncMethodNaming | Naming | Warning | Les méthodes async doivent finir par "Async" |
| COD004 | DisposePattern | Resources | Warning | Utiliser using pour les IDisposable |
| COD005 | EmptyCatchBlock | ErrorHandling | Error | Les blocs catch ne doivent pas être vides |
| COD006 | MagicNumber | Maintainability | Warning | Éviter les nombres magiques |
| COD007 | LongMethod | Maintainability | Warning | Méthodes trop longues (>50 lignes) |
| COD008 | StringConcatenationInLoop | Performance | Warning | Utiliser StringBuilder dans les boucles |
| COD009 | ToListInQuery | Performance | Warning | Éviter ToList() avant Count()/Any()/First() |

## Configuration

Créez un fichier `codengine.config.json` à la racine de votre projet :

```json
{
  "sourcePath": ".",
  "includePatterns": ["**/*.cs"],
  "excludePatterns": [
    "**/bin/**",
    "**/obj/**",
    "**/Migrations/**"
  ],
  "rules": {
    "COD001": { "enabled": true },
    "COD006": { "enabled": false }
  },
  "reporting": {
    "format": "console",
    "verbose": false,
    "includeCodeSnippets": true
  },
  "oracle": {
    "outputDirectory": "./oracle_packages",
    "format": {
      "indentSize": 4,
      "uppercaseKeywords": true,
      "maxConsecutiveBlankLines": 1,
      "trimTrailingWhitespace": true,
      "linesBetweenQueries": 1,
      "maxLineLength": 0,
      "engine": "combined"
    }
  },
  "failOnError": true,
  "failOnWarning": false
}
```

## Ajouter une nouvelle règle

1. Créer une classe dans `Codengine.Rules/CSharp/` héritant de `RuleBase` :

```csharp
public class MyCustomRule : RuleBase
{
    public override string Id => "COD010";
    public override string Name => "MyCustomRule";
    public override string Description => "Description de la règle";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override string Category => "Custom";

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        // Analyser l'AST avec Roslyn...
        yield break;
    }
}
```

2. La règle sera automatiquement découverte au démarrage.

## Architecture

```
codengine/
├── src/
│   ├── Codengine.Core/          # Modèles, engine, configuration
│   ├── Codengine.Rules/         # Règles d'analyse et fixers
│   ├── Codengine.Connectors/    # Connecteurs (Oracle, ...)
│   ├── Codengine.Reporters/     # Reporters (Console, JSON, HTML)
│   └── Codengine.Cli/           # Application console
└── tests/
    └── Codengine.Rules.Tests/   # Tests unitaires
```

## Intégration CI/CD

### GitHub Actions

```yaml
- name: Run Codengine
  run: |
    dotnet tool install -g codengine
    codengine analyze ./src -f json -o codengine-report.json

- name: Upload Report
  uses: actions/upload-artifact@v3
  with:
    name: codengine-report
    path: codengine-report.json
```

### Azure DevOps

```yaml
- script: |
    dotnet tool install -g codengine
    codengine analyze ./src -f json -o $(Build.ArtifactStagingDirectory)/codengine-report.json
  displayName: 'Run Codengine Analysis'
```

## Licence

MIT
