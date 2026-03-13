# Développement de règles personnalisées

## Vue d'ensemble

Codengine est conçu pour être extensible. Vous pouvez créer vos propres règles d'analyse pour répondre à vos besoins métier spécifiques.

## Architecture des règles

```
Codengine.Rules/
├── Abstractions/
│   ├── IRule.cs           # Interface de base
│   ├── RuleBase.cs        # Classe abstraite avec helpers
│   ├── IRuleProvider.cs   # Fournisseur de règles
│   └── DefaultRuleProvider.cs  # Découverte automatique
├── CSharp/
│   ├── NullCheckAfterOrDefaultRule.cs  # COD001
│   └── ... autres règles
└── Fixes/
    └── NullCheckFixer.cs  # Auto-fixer pour COD001
```

## Créer une règle

### 1. Interface IRule

```csharp
public interface IRule
{
    string Id { get; }              // Ex: "COD010"
    string Name { get; }            // Ex: "MyCustomRule"
    string Description { get; }     // Description longue
    RuleSeverity Severity { get; }  // Info, Warning, Error, Critical
    string Category { get; }        // Ex: "Security", "Performance"
    bool IsEnabled { get; set; }    // Activation/désactivation

    IEnumerable<Violation> Analyze(RuleContext context);
}
```

### 2. Classe RuleBase

```csharp
public abstract class RuleBase : IRule
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract RuleSeverity Severity { get; }
    public virtual string Category => "General";
    public bool IsEnabled { get; set; } = true;

    public abstract IEnumerable<Violation> Analyze(RuleContext context);

    // Helper pour créer une violation
    protected Violation CreateViolation(
        RuleContext context,
        SyntaxNode node,
        string message,
        string? suggestedFix = null);
}
```

### 3. Exemple complet

```csharp
using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Détecte les méthodes publiques sans documentation XML.
/// </summary>
public class PublicMethodDocumentationRule : RuleBase
{
    public override string Id => "COD010";
    public override string Name => "PublicMethodDocumentation";
    public override string Description =>
        "Les méthodes publiques doivent avoir une documentation XML.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override string Category => "Documentation";

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();

        // Trouver toutes les méthodes publiques
        var publicMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));

        foreach (var method in publicMethods)
        {
            // Vérifier si la méthode a une documentation XML
            var hasXmlDoc = method.GetLeadingTrivia()
                .Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                          t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            if (!hasXmlDoc)
            {
                yield return CreateViolation(
                    context,
                    method.Identifier,
                    $"La méthode publique '{method.Identifier.Text}' n'a pas de documentation XML.",
                    $"/// <summary>\n/// Description de {method.Identifier.Text}\n/// </summary>");
            }
        }
    }
}
```

## Utiliser Roslyn

### RuleContext

```csharp
public class RuleContext
{
    public SyntaxTree SyntaxTree { get; }      // Arbre syntaxique
    public SemanticModel? SemanticModel { get; } // Modèle sémantique (types, symboles)
    public string FilePath { get; }             // Chemin du fichier
    public CSharpCompilation? Compilation { get; } // Compilation
}
```

### Parcourir l'AST

```csharp
// Obtenir la racine
var root = context.SyntaxTree.GetRoot();

// Trouver tous les noeuds d'un type
var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

// Filtrer
var asyncMethods = methods.Where(m =>
    m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword)));

// Accéder aux informations
foreach (var method in methods)
{
    var name = method.Identifier.Text;
    var returnType = method.ReturnType.ToString();
    var parameters = method.ParameterList.Parameters;
    var body = method.Body;
    var modifiers = method.Modifiers;
}
```

### Utiliser le modèle sémantique

```csharp
if (context.SemanticModel != null)
{
    // Obtenir le symbole d'une expression
    var symbol = context.SemanticModel.GetSymbolInfo(expression).Symbol;

    // Obtenir le type d'une expression
    var typeInfo = context.SemanticModel.GetTypeInfo(expression);
    var type = typeInfo.Type;

    // Vérifier si un type implémente une interface
    var implementsIDisposable = type?.AllInterfaces
        .Any(i => i.Name == "IDisposable") ?? false;
}
```

### Patterns courants

```csharp
// Vérifier si c'est un appel de méthode spécifique
private bool IsTargetMethod(InvocationExpressionSyntax invocation, string methodName)
{
    return invocation.Expression switch
    {
        MemberAccessExpressionSyntax memberAccess =>
            memberAccess.Name.Identifier.Text == methodName,
        IdentifierNameSyntax identifier =>
            identifier.Identifier.Text == methodName,
        _ => false
    };
}

// Obtenir la méthode contenant un noeud
private MethodDeclarationSyntax? GetContainingMethod(SyntaxNode node)
{
    return node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
}

// Obtenir le bloc contenant
private BlockSyntax? GetContainingBlock(SyntaxNode node)
{
    return node.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
}
```

## Créer un auto-fixer

### Interface ICodeFixer

```csharp
public interface ICodeFixer
{
    string RuleId { get; }           // ID de la règle associée
    string Description { get; }       // Description du fix
    bool CanFix(Violation violation); // Peut-on corriger ?
    Task<FixResult> FixAsync(Violation violation, SyntaxTree tree, CancellationToken ct);
}

public class FixResult
{
    public bool Success { get; init; }
    public string? NewCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int LinesChanged { get; init; }
}
```

### Exemple de fixer

```csharp
public class PublicMethodDocumentationFixer : ICodeFixer
{
    public string RuleId => "COD010";
    public string Description => "Ajoute une documentation XML vide";

    public bool CanFix(Violation violation) => violation.RuleId == RuleId;

    public Task<FixResult> FixAsync(
        Violation violation,
        SyntaxTree tree,
        CancellationToken ct)
    {
        var root = tree.GetRoot(ct);

        // Trouver la méthode
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m =>
                m.GetLocation().GetLineSpan().StartLinePosition.Line + 1 == violation.Line);

        if (method == null)
            return Task.FromResult(FixResult.Failed("Méthode non trouvée"));

        // Créer le commentaire XML
        var xmlComment = SyntaxFactory.ParseLeadingTrivia(
            $"/// <summary>\n/// TODO: Documenter {method.Identifier.Text}\n/// </summary>\n");

        // Ajouter le commentaire
        var newMethod = method.WithLeadingTrivia(
            method.GetLeadingTrivia().AddRange(xmlComment));

        var newRoot = root.ReplaceNode(method, newMethod);

        return Task.FromResult(FixResult.Succeeded(newRoot.ToFullString(), 3));
    }
}
```

## Enregistrer la règle

Les règles sont **automatiquement découvertes** via réflexion dans `DefaultRuleProvider`.

Pour qu'une règle soit découverte :
1. Elle doit être dans l'assembly `Codengine.Rules`
2. Elle doit implémenter `IRule`
3. Elle ne doit pas être abstraite

```csharp
// Dans DefaultRuleProvider.cs
private static IEnumerable<IRule> DiscoverRules()
{
    var ruleType = typeof(IRule);
    var assembly = Assembly.GetExecutingAssembly();

    return assembly.GetTypes()
        .Where(t => ruleType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
        .Select(t => (IRule)Activator.CreateInstance(t)!)
        .OrderBy(r => r.Id);
}
```

## Tests unitaires

```csharp
using Codengine.Core.Models;
using Codengine.Rules.CSharp;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class PublicMethodDocumentationRuleTests
{
    private readonly PublicMethodDocumentationRule _rule = new();

    [Fact]
    public void Should_Detect_Missing_Documentation()
    {
        var code = @"
public class MyClass
{
    public void MyMethod() { }
}";

        var violations = AnalyzeCode(code);

        Assert.Single(violations);
        Assert.Equal("COD010", violations[0].RuleId);
    }

    [Fact]
    public void Should_Not_Detect_When_Documentation_Exists()
    {
        var code = @"
public class MyClass
{
    /// <summary>
    /// Ma méthode documentée
    /// </summary>
    public void MyMethod() { }
}";

        var violations = AnalyzeCode(code);

        Assert.Empty(violations);
    }

    private List<Violation> AnalyzeCode(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var context = new RuleContext
        {
            SyntaxTree = tree,
            SemanticModel = null,
            FilePath = "test.cs",
            Compilation = null
        };
        return _rule.Analyze(context).ToList();
    }
}
```

## Bonnes pratiques

1. **ID unique** : Utilisez un préfixe pour vos règles custom (ex: `MYAPP001`)
2. **Messages clairs** : Expliquez le problème et la solution
3. **Tests** : Testez les cas positifs ET négatifs
4. **Performance** : Évitez les parcours multiples de l'AST
5. **Null safety** : Le `SemanticModel` peut être `null`
6. **Documentation** : Documentez vos règles dans `docs/05-rules.md`
