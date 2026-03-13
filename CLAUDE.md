# Codengine - Guide pour Claude

## Description du projet

Codengine est un analyseur de code statique maison (style SonarQube) pour :
- **C# (.NET Framework 4.8 / .NET 9)** : Analyse via Roslyn AST
- **PL/SQL Oracle** : Extraction des packages depuis un schéma Oracle

**Objectif principal** : Clean code automatique sans IA, basé sur des règles métier.

## Architecture

```
codengine/
├── src/
│   ├── Codengine.Core/           # Noyau : modèles, engine, configuration
│   │   ├── Configuration/        # EngineConfig, ConfigLoader
│   │   ├── Engine/               # RoslynAnalysisEngine
│   │   ├── Fixes/                # Système d'auto-fix
│   │   └── Models/               # Violation, AnalysisResult, RuleContext
│   │
│   ├── Codengine.Rules/          # Règles d'analyse (modulaire)
│   │   ├── Abstractions/         # IRule, RuleBase, IRuleProvider
│   │   ├── CSharp/               # Règles C# (COD001-COD009)
│   │   └── Fixes/                # Auto-fixers par règle
│   │
│   ├── Codengine.Connectors/     # Connecteurs externes
│   │   └── Oracle/               # OraclePackageExtractor
│   │
│   ├── Codengine.Reporters/      # Sortie des résultats
│   │   ├── ConsoleReporter.cs
│   │   ├── JsonReporter.cs
│   │   └── HtmlReporter.cs
│   │
│   └── Codengine.Cli/            # Application console (System.CommandLine)
│
└── tests/
    └── Codengine.Rules.Tests/    # Tests xUnit
```

## Commandes de build

```bash
# Dotnet est installé ici (pas dans le PATH bash)
DOTNET="/c/Program Files/dotnet/dotnet.exe"

# Build
"$DOTNET" build

# Tests
"$DOTNET" test

# Exécuter le CLI (sans installation)
"$DOTNET" run --project src/Codengine.Cli -- <commande>

# Installer comme outil global (une seule fois)
"$DOTNET" pack src/Codengine.Cli -o src/Codengine.Cli/nupkg
"$DOTNET" tool install --global --add-source src/Codengine.Cli/nupkg Codengine.Cli
# Ensuite : codengine <commande> depuis n'importe où

# Publier l'outil global après modification (bump version + réinstall)
bash scripts/publish-tool.sh
```

## Règle : publication automatique après modification validée

**IMPORTANT** : Après chaque ensemble de modifications validées par l'utilisateur (fonctionnalité, bug fix, etc.), exécuter automatiquement :

```bash
bash scripts/publish-tool.sh
```

Ce script :
1. Lit la version actuelle dans `src/Codengine.Cli/Codengine.Cli.csproj`
2. Incrémente le numéro de patch (ex. 1.0.3 → 1.0.4)
3. Met à jour le `.csproj`
4. Repack le `.nupkg`
5. Désinstalle et réinstalle l'outil global `codengine`

Ne pas exécuter ce script si l'utilisateur ne demande que des recherches ou explications sans modification de code.

## Commandes CLI

```bash
codengine analyze ./src                    # Analyser
codengine analyze ./src -f html -o r.html  # Rapport HTML
codengine analyze ./src -d COD006,COD007   # Désactiver règles
codengine analyze ./src -v                 # Verbose (suggestions)
codengine fix ./src                        # Auto-fix
codengine fix ./src --dry-run              # Preview fixes
codengine extract-oracle -c "..."          # Extraire packages Oracle
codengine list-rules                       # Lister règles
codengine init                             # Créer config
```

## Règles disponibles

| ID | Nom | Catégorie | Sévérité | Auto-fix |
|----|-----|-----------|----------|----------|
| COD001 | NullCheckAfterSingleOrDefault | NullSafety | Error | Oui | Méthodes *OrDefault() (First, Single, Last, ElementAt) — types référence uniquement |
| COD002 | EmptyListBeforeContains | NullSafety | Error | Non | Sur tout .Where() — liste vide dans Contains() annule le filtrage |
| COD003 | AsyncMethodNaming | Naming | Warning | Oui |
| COD004 | DisposePattern | Resources | Warning | Non |
| COD005 | EmptyCatchBlock | ErrorHandling | Error | Oui |
| COD006 | MagicNumber | Maintainability | Warning | Non |
| COD007 | LongMethod | Maintainability | Warning | Non |
| COD008 | StringConcatenationInLoop | Performance | Warning | Non |
| COD009 | ToListInQuery | Performance | Warning | Non |

## Ajouter une nouvelle règle

1. Créer une classe dans `src/Codengine.Rules/CSharp/` :

```csharp
public class MaRegle : RuleBase
{
    public override string Id => "COD010";
    public override string Name => "MaRegle";
    public override string Description => "Description";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override string Category => "MaCategorie";

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        // Utiliser Roslyn pour analyser l'AST
        // CreateViolation() pour créer une violation
        yield break;
    }
}
```

2. La règle est auto-découverte via réflexion (DefaultRuleProvider).

3. Pour ajouter un auto-fixer, créer dans `src/Codengine.Rules/Fixes/` une classe implémentant `ICodeFixer`.

## Conventions

- **Target** : .NET 9.0
- **Nullable** : activé
- **Tests** : xUnit
- **CLI** : System.CommandLine
- **Oracle** : Oracle.ManagedDataAccess.Core
- **Analyse C#** : Microsoft.CodeAnalysis.CSharp (Roslyn)

## Fichiers de configuration

- `codengine.config.json` : Configuration par projet
- Cherché automatiquement dans le répertoire courant ou parents

## État actuel

- 33 fichiers sources
- 9 règles implémentées
- 3 auto-fixers (COD001, COD003, COD005)
- 3 reporters (Console, JSON, HTML)
- 1 connecteur (Oracle)
- 7 tests unitaires passent

## Documentation utilisateur

La documentation complète est dans `docs/` :

| Fichier | Contenu |
|---------|---------|
| `INDEX.md` | Table des matières |
| `01-getting-started.md` | Guide de démarrage rapide |
| `02-installation.md` | Installation détaillée |
| `03-configuration.md` | Options de configuration |
| `04-cli-usage.md` | Toutes les commandes CLI |
| `05-rules.md` | **Description de chaque règle** |
| `06-auto-fix.md` | Système d'auto-correction |
| `07-oracle-extraction.md` | Extraction packages Oracle |
| `08-reports.md` | Formats de rapport |
| `09-ci-cd.md` | Intégration CI/CD |
| `10-custom-rules.md` | Créer des règles personnalisées |
| `11-architecture.md` | Architecture technique |
| `12-faq.md` | FAQ et dépannage |

### Maintenance de la documentation

**IMPORTANT** : Mettre à jour la documentation à chaque modification :

1. **Nouvelle règle** : Ajouter dans `docs/05-rules.md` + mettre à jour le tableau dans CLAUDE.md
2. **Nouveau fixer** : Ajouter dans `docs/06-auto-fix.md` + mettre à jour les tableaux
3. **Nouvelle commande** : Ajouter dans `docs/04-cli-usage.md`
4. **Nouvelle option config** : Ajouter dans `docs/03-configuration.md`
5. **Nouveau reporter** : Ajouter dans `docs/08-reports.md`
6. **Nouveau connecteur** : Créer une doc dédiée si nécessaire

## Prochaines améliorations possibles

- [ ] Plus de règles métier spécifiques
- [ ] Plus d'auto-fixers
- [ ] Règles PL/SQL pour les packages Oracle extraits
- [ ] Cache des résultats d'analyse
- [ ] Watch mode (analyse continue)
- [ ] Intégration IDE (VS Code extension)
- [ ] Baseline (ignorer violations existantes)
- [ ] Commentaires `// codengine-ignore` pour ignorer des lignes
