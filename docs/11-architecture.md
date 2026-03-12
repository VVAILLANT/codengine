# Architecture technique

## Vue d'ensemble

```
┌─────────────────────────────────────────────────────────────┐
│                      Codengine.Cli                          │
│                    (System.CommandLine)                     │
└─────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ Codengine.Core  │  │ Codengine.Rules │  │   Codengine.    │
│                 │◄─│                 │  │   Connectors    │
│ - Engine        │  │ - IRule         │  │                 │
│ - Models        │  │ - RuleBase      │  │ - Oracle        │
│ - Config        │  │ - CSharp/*      │  │ - (FileSystem)  │
│ - Fixes         │  │ - Fixes/*       │  │                 │
└─────────────────┘  └─────────────────┘  └─────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│                   Codengine.Reporters                       │
│         Console  │  JSON  │  HTML                           │
└─────────────────────────────────────────────────────────────┘
```

## Projets

### Codengine.Core

Le noyau de l'application contenant :

| Composant | Description |
|-----------|-------------|
| `Models/` | Classes de données (Violation, AnalysisResult, RuleContext) |
| `Engine/` | Moteur d'analyse Roslyn |
| `Configuration/` | Chargement et gestion de la configuration |
| `Fixes/` | Système d'auto-correction |

**Dépendances** :
- `Microsoft.CodeAnalysis.CSharp` (Roslyn)
- `Microsoft.CodeAnalysis.CSharp.Workspaces`

### Codengine.Rules

Contient toutes les règles d'analyse :

| Composant | Description |
|-----------|-------------|
| `Abstractions/` | Interfaces et classes de base (IRule, RuleBase) |
| `CSharp/` | Règles pour le code C# |
| `Fixes/` | Auto-fixers par règle |
| `Oracle/` | (Futur) Règles pour PL/SQL |

**Dépendances** :
- `Codengine.Core`
- `Microsoft.CodeAnalysis.CSharp`

### Codengine.Connectors

Connecteurs vers des sources externes :

| Composant | Description |
|-----------|-------------|
| `Abstractions/` | Interface ISourceConnector |
| `Oracle/` | Extraction des packages PL/SQL |
| `FileSystem/` | (Futur) Lecture de fichiers |

**Dépendances** :
- `Codengine.Core`
- `Oracle.ManagedDataAccess.Core`

### Codengine.Reporters

Générateurs de rapports :

| Composant | Description |
|-----------|-------------|
| `IReporter.cs` | Interface commune |
| `ConsoleReporter.cs` | Sortie colorée terminal |
| `JsonReporter.cs` | Export JSON |
| `HtmlReporter.cs` | Rapport HTML |

**Dépendances** :
- `Codengine.Core`

### Codengine.Cli

Application console :

| Composant | Description |
|-----------|-------------|
| `Program.cs` | Point d'entrée, définition des commandes |

**Dépendances** :
- Tous les autres projets
- `System.CommandLine`

## Flux d'exécution

### Analyse

```
1. CLI parse les arguments
         │
         ▼
2. Chargement de la configuration (ConfigLoader)
         │
         ▼
3. Découverte des règles (DefaultRuleProvider)
         │
         ▼
4. Création du moteur (RoslynAnalysisEngine)
         │
         ▼
5. Pour chaque fichier .cs :
   ├── Parse avec Roslyn (SyntaxTree)
   ├── Compilation (SemanticModel)
   └── Exécution des règles
         │
         ▼
6. Agrégation des violations (AnalysisResult)
         │
         ▼
7. Génération du rapport (IReporter)
```

### Auto-fix

```
1. Analyse (comme ci-dessus)
         │
         ▼
2. Filtrage des violations fixables
         │
         ▼
3. Pour chaque fichier :
   ├── Tri des violations par ligne (desc)
   └── Pour chaque violation :
       ├── Récupération du fixer (CodeFixerEngine)
       ├── Application du fix
       └── Re-parse du code modifié
         │
         ▼
4. Sauvegarde des fichiers modifiés
```

## Classes principales

### RoslynAnalysisEngine

```csharp
public class RoslynAnalysisEngine : IAnalysisEngine
{
    // Analyse un répertoire
    Task<AnalysisResult> AnalyzeAsync(EngineConfig config, CancellationToken ct);

    // Analyse un fichier
    Task<AnalysisResult> AnalyzeFileAsync(string filePath, CancellationToken ct);

    // Analyse du code en mémoire
    Task<AnalysisResult> AnalyzeCodeAsync(string code, string virtualPath, CancellationToken ct);
}
```

### IRule

```csharp
public interface IRule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    RuleSeverity Severity { get; }
    string Category { get; }
    bool IsEnabled { get; set; }

    IEnumerable<Violation> Analyze(RuleContext context);
}
```

### Violation

```csharp
public class Violation
{
    public string RuleId { get; }
    public string RuleName { get; }
    public string Message { get; }
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public RuleSeverity Severity { get; }
    public string? CodeSnippet { get; }
    public string? SuggestedFix { get; }
}
```

### ICodeFixer

```csharp
public interface ICodeFixer
{
    string RuleId { get; }
    string Description { get; }
    bool CanFix(Violation violation);
    Task<FixResult> FixAsync(Violation violation, SyntaxTree tree, CancellationToken ct);
}
```

## Extensibilité

### Ajouter une règle

1. Créer une classe dans `Codengine.Rules/CSharp/`
2. Hériter de `RuleBase`
3. Implémenter `Analyze()`
4. La règle est auto-découverte

### Ajouter un reporter

1. Créer une classe implémentant `IReporter`
2. L'enregistrer dans `Program.cs` :

```csharp
IReporter reporter = format switch
{
    "json" => new JsonReporter(),
    "html" => new HtmlReporter(),
    "custom" => new CustomReporter(),  // Nouveau
    _ => new ConsoleReporter()
};
```

### Ajouter un connecteur

1. Créer une classe implémentant `ISourceConnector`
2. Ajouter une commande dans `Program.cs`

## Considérations de performance

- **Parallélisme** : Les fichiers sont analysés en parallèle (`Parallel.ForEachAsync`)
- **Lazy loading** : Les règles sont découvertes une seule fois au démarrage
- **Compilation** : Une seule compilation Roslyn par fichier
- **Mémoire** : Les violations sont agrégées dans un `ConcurrentBag`

## Tests

```
tests/
├── Codengine.Core.Tests/
│   └── (tests du moteur)
└── Codengine.Rules.Tests/
    ├── NullCheckAfterSingleOrDefaultRuleTests.cs
    └── EmptyListBeforeContainsRuleTests.cs
```

Framework : **xUnit**

```bash
dotnet test
```
