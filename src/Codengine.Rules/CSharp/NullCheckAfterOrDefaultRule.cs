using Codengine.Core.Models;
using Codengine.Rules.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codengine.Rules.CSharp;

/// <summary>
/// Vérifie qu'après chaque méthode LINQ *OrDefault() retournant un type référence,
/// le résultat est vérifié pour null avant utilisation.
/// Fonctionne comme un mini-analyseur de flux : chaque usage de la variable
/// est vérifié individuellement en remontant l'arbre syntaxique.
/// Ignore les types valeur (int, struct, etc.) qui ne peuvent pas être null.
/// </summary>
public class NullCheckAfterOrDefaultRule : RuleBase
{
    public override string Id => "COD001";
    public override string Name => "NullCheckAfterOrDefault";
    public override string Description =>
        "Le résultat d'une méthode LINQ *OrDefault() doit être vérifié pour null avant utilisation (types référence uniquement).";
    public override RuleSeverity Severity => RuleSeverity.Error;
    public override string Category => "NullSafety";

    private static readonly string[] TargetMethods =
        { "SingleOrDefault", "FirstOrDefault", "LastOrDefault", "ElementAtOrDefault" };

    public override IEnumerable<Violation> Analyze(RuleContext context)
    {
        var root = context.SyntaxTree.GetRoot();
        var violations = new List<Violation>();

        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsTargetMethod);

        foreach (var invocation in invocations)
        {
            var variableDeclaration = GetVariableDeclaration(invocation);
            if (variableDeclaration == null)
                continue;

            // Si l'appel est dans un ?? (coalesce), la variable est garantie non-null
            if (IsWrappedInCoalesce(invocation))
                continue;

            // Les types valeur (int, struct, etc.) ne peuvent pas être null → pas de faux positif
            if (IsValueType(variableDeclaration, context.SemanticModel))
                continue;

            var variableName = variableDeclaration.Identifier.Text;

            // Scope de recherche : méthode, constructeur, accesseur ou fonction locale
            var scope = invocation.Ancestors()
                .FirstOrDefault(n => n is BaseMethodDeclarationSyntax
                                  or AccessorDeclarationSyntax
                                  or LocalFunctionStatementSyntax);

            if (scope == null)
                continue;

            var firstUnsafe = FindFirstUnsafeUsage(scope, variableName, invocation);
            if (firstUnsafe != null)
            {
                violations.Add(CreateViolation(
                    context,
                    firstUnsafe,
                    $"La variable '{variableName}' issue de {GetMethodName(invocation)}() doit être vérifiée pour null avant utilisation.",
                    $"Ajouter: if ({variableName} == null) return; // ou gérer le cas null"));
            }
        }

        return violations;
    }

    /// <summary>
    /// Parcourt toutes les utilisations de la variable après la déclaration,
    /// dans l'ordre du code, et retourne la première utilisation non protégée.
    /// </summary>
    private static IdentifierNameSyntax? FindFirstUnsafeUsage(
        SyntaxNode scope,
        string variableName,
        InvocationExpressionSyntax declaration)
    {
        var sourceCollectionExpression = GetSourceCollectionExpression(declaration);
        var declarationEnd = declaration.Span.End;

        var usages = scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == variableName && id.SpanStart > declarationEnd)
            .OrderBy(id => id.SpanStart);

        foreach (var usage in usages)
        {
            // Réassignation (item = ...) → la variable change de valeur, on arrête
            if (IsReassignment(usage))
                return null;

            // Redéclaration comme variable de boucle foreach → variable originale hors scope, on arrête
            if (IsShadowedByForeach(usage, variableName))
                return null;

            // Seuls les accès membres directs (item.Foo) sont dangereux
            // item?.Foo → ConditionalAccessExpression (pas MemberAccess) → ignoré
            // item ?? x → BinaryExpression → ignoré
            // Foo(item) → Argument → ignoré
            if (usage.Parent is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Expression != usage)
                continue;

            // Protégé par un if (item != null) ancêtre ?
            if (IsInsideNullCheck(usage, variableName))
                continue;

            // Protégé par un guard clause (if item == null return/throw) avant ?
            if (HasGuardClauseBefore(usage, variableName))
                continue;

            // Dans la condition d'un guard clause : if (item == null || !item.Prop || ...) return;
            // Les accès membres sont sûrs grâce au court-circuit de ||
            if (IsInsideGuardClauseCondition(usage, variableName))
                continue;

            // Dans un || où un opérande précédent utilise item?. :
            // si item est null, item?.X retourne null, et null != value → true → court-circuit
            // → item.Prop n'est jamais évalué quand item est null
            if (IsProtectedByConditionalAccessInOrChain(usage, variableName))
                continue;

            // Protégé par un check "collection non vide" avant l'accès membre
            // Ex: list.Count() != 0 && item.Prop ...
            // Ex: list.Count() == 0 || item.Prop ...
            if (sourceCollectionExpression != null
                && IsProtectedByNonEmptyCollectionCheckInLogicalChain(usage, sourceCollectionExpression))
                continue;

            // Protégé par un ternaire : item != null ? item.Prop : default
            // ou : item == null ? default : item.Prop
            if (IsInsideConditionalNotNullBranch(usage, variableName))
                continue;

            // Première utilisation non protégée trouvée
            return usage;
        }

        return null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static bool IsTargetMethod(InvocationExpressionSyntax invocation)
    {
        var methodName = GetMethodName(invocation);
        return TargetMethods.Contains(methodName);
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };
    }

    private static VariableDeclaratorSyntax? GetVariableDeclaration(InvocationExpressionSyntax invocation)
    {
        var parent = invocation.Parent;

        while (parent != null)
        {
            if (parent is VariableDeclaratorSyntax declarator)
                return declarator;

            if (parent is LocalDeclarationStatementSyntax)
                break;

            if (parent is StatementSyntax)
                break;

            parent = parent.Parent;
        }

        var equalsClause = invocation.Ancestors()
            .OfType<EqualsValueClauseSyntax>()
            .FirstOrDefault();

        return equalsClause?.Parent as VariableDeclaratorSyntax;
    }

    /// <summary>
    /// Vérifie si la variable est de type valeur (int, struct, enum, etc.).
    /// Les types valeur ne peuvent pas être null → pas de faux positif.
    /// Si le SemanticModel n'est pas disponible, retourne false (on suppose type référence par défaut).
    /// </summary>
    private static bool IsValueType(VariableDeclaratorSyntax declarator, SemanticModel? semanticModel)
    {
        if (semanticModel == null)
            return false;

        var symbol = semanticModel.GetDeclaredSymbol(declarator);
        if (symbol is ILocalSymbol local)
        {
            var type = local.Type;
            // int, struct, enum → type valeur, pas de risque null
            // int?, Nullable<T> → type valeur nullable, peut être null → on traite comme référence
            return type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
                                    && type.NullableAnnotation != NullableAnnotation.Annotated;
        }

        return false;
    }

    private static ExpressionSyntax? GetSourceCollectionExpression(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? memberAccess.Expression
            : null;
    }

    /// <summary>
    /// Vérifie si l'usage est protégé par un opérande précédent d'une chaîne logique
    /// (&& ou ||) qui garantit que la collection source de *OrDefault() n'est pas vide
    /// au moment où l'opérande contenant l'usage est évalué.
    /// </summary>
    private static bool IsProtectedByNonEmptyCollectionCheckInLogicalChain(
        IdentifierNameSyntax usage,
        ExpressionSyntax sourceCollectionExpression)
    {
        var current = (SyntaxNode)usage;
        while (current != null)
        {
            if (current.Parent is BinaryExpressionSyntax binaryExpr
                && (binaryExpr.IsKind(SyntaxKind.LogicalAndExpression)
                    || binaryExpr.IsKind(SyntaxKind.LogicalOrExpression))
                && binaryExpr.Span.Contains(usage.Span))
            {
                var operatorKind = binaryExpr.Kind();

                var topLogical = binaryExpr;
                while (topLogical.Parent is BinaryExpressionSyntax parentLogical
                       && parentLogical.Kind() == operatorKind
                       && parentLogical.Span.Contains(usage.Span))
                {
                    topLogical = parentLogical;
                }

                var operands = new List<ExpressionSyntax>();
                CollectLogicalOperands(topLogical, operatorKind, operands);

                foreach (var operand in operands)
                {
                    if (operand.Span.Contains(usage.Span))
                        break;

                    var guaranteesNonEmpty = operatorKind == SyntaxKind.LogicalAndExpression
                        ? ConditionGuaranteesCollectionNonEmpty(operand, sourceCollectionExpression, whenTrue: true)
                        : ConditionGuaranteesCollectionNonEmpty(operand, sourceCollectionExpression, whenTrue: false);

                    if (guaranteesNonEmpty)
                        return true;
                }

                return false;
            }

            if (current is StatementSyntax or BaseMethodDeclarationSyntax)
                break;

            current = current.Parent;
        }

        return false;
    }

    private static void CollectLogicalOperands(
        ExpressionSyntax expr,
        SyntaxKind logicalOperatorKind,
        List<ExpressionSyntax> operands)
    {
        switch (expr)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(logicalOperatorKind):
                CollectLogicalOperands(binary.Left, logicalOperatorKind, operands);
                CollectLogicalOperands(binary.Right, logicalOperatorKind, operands);
                break;
            case ParenthesizedExpressionSyntax paren:
                CollectLogicalOperands(paren.Expression, logicalOperatorKind, operands);
                break;
            default:
                operands.Add(expr);
                break;
        }
    }

    /// <summary>
    /// Détermine si une condition garantit que la collection n'est pas vide,
    /// selon la polarité attendue (quand l'expression vaut true ou false).
    /// </summary>
    private static bool ConditionGuaranteesCollectionNonEmpty(
        ExpressionSyntax condition,
        ExpressionSyntax sourceCollectionExpression,
        bool whenTrue)
    {
        condition = UnwrapParenthesized(condition);

        return condition switch
        {
            // Pour (a && b):
            // - quand true: les deux sont vrais, il suffit qu'un des deux garantisse non-vide quand true
            // - quand false: au moins un est faux, il faut que les deux garantissent non-vide quand false
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression)
                => whenTrue
                    ? ConditionGuaranteesCollectionNonEmpty(binary.Left, sourceCollectionExpression, whenTrue: true)
                      || ConditionGuaranteesCollectionNonEmpty(binary.Right, sourceCollectionExpression, whenTrue: true)
                    : ConditionGuaranteesCollectionNonEmpty(binary.Left, sourceCollectionExpression, whenTrue: false)
                      && ConditionGuaranteesCollectionNonEmpty(binary.Right, sourceCollectionExpression, whenTrue: false),

            // Pour (a || b):
            // - quand true: au moins un est vrai, les deux doivent garantir non-vide quand true
            // - quand false: les deux sont faux, il suffit qu'un côté garantisse non-vide quand false
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalOrExpression)
                => whenTrue
                    ? ConditionGuaranteesCollectionNonEmpty(binary.Left, sourceCollectionExpression, whenTrue: true)
                      && ConditionGuaranteesCollectionNonEmpty(binary.Right, sourceCollectionExpression, whenTrue: true)
                    : ConditionGuaranteesCollectionNonEmpty(binary.Left, sourceCollectionExpression, whenTrue: false)
                      || ConditionGuaranteesCollectionNonEmpty(binary.Right, sourceCollectionExpression, whenTrue: false),

            // !a -> inverse la polarité
            PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression)
                => ConditionGuaranteesCollectionNonEmpty(prefix.Operand, sourceCollectionExpression, whenTrue: !whenTrue),

            InvocationExpressionSyntax invocation
                => whenTrue && IsAnyCall(invocation, sourceCollectionExpression),

            BinaryExpressionSyntax comparison
                => IsCountComparisonGuaranteeingNonEmpty(comparison, sourceCollectionExpression, whenTrue),

            _ => false
        };
    }

    private static ExpressionSyntax UnwrapParenthesized(ExpressionSyntax expr)
    {
        while (expr is ParenthesizedExpressionSyntax paren)
            expr = paren.Expression;

        return expr;
    }

    private static bool IsAnyCall(InvocationExpressionSyntax invocation, ExpressionSyntax sourceCollectionExpression)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "Any")
            return false;

        if (invocation.ArgumentList.Arguments.Count != 0)
            return false;

        return IsSameExpression(memberAccess.Expression, sourceCollectionExpression);
    }

    private static bool IsCountComparisonGuaranteeingNonEmpty(
        BinaryExpressionSyntax comparison,
        ExpressionSyntax sourceCollectionExpression,
        bool whenTrue)
    {
        var left = UnwrapParenthesized(comparison.Left);
        var right = UnwrapParenthesized(comparison.Right);

        if (IsCountAccess(left, sourceCollectionExpression) && TryGetIntLiteral(right, out var rightValue))
            return IsCountComparisonGuaranteeingNonEmpty(comparison.Kind(), rightValue, countOnLeft: true, whenTrue);

        if (IsCountAccess(right, sourceCollectionExpression) && TryGetIntLiteral(left, out var leftValue))
            return IsCountComparisonGuaranteeingNonEmpty(comparison.Kind(), leftValue, countOnLeft: false, whenTrue);

        return false;
    }

    private static bool IsCountComparisonGuaranteeingNonEmpty(
        SyntaxKind kind,
        int literalValue,
        bool countOnLeft,
        bool whenTrue)
    {
        return whenTrue
            ? IsPositiveCountComparisonKind(kind, literalValue, countOnLeft)
            : IsZeroCountComparisonKind(kind, literalValue, countOnLeft);
    }

    private static bool IsPositiveCountComparisonKind(SyntaxKind kind, int literalValue, bool countOnLeft)
    {
        return kind switch
        {
            SyntaxKind.NotEqualsExpression => literalValue == 0,

            // count > 0 / count >= 1
            SyntaxKind.GreaterThanExpression when countOnLeft => literalValue == 0,
            SyntaxKind.GreaterThanOrEqualExpression when countOnLeft => literalValue <= 1,

            // 0 < count / 1 <= count
            SyntaxKind.LessThanExpression when !countOnLeft => literalValue == 0,
            SyntaxKind.LessThanOrEqualExpression when !countOnLeft => literalValue <= 1,

            _ => false
        };
    }

    private static bool IsZeroCountComparisonKind(SyntaxKind kind, int literalValue, bool countOnLeft)
    {
        return kind switch
        {
            SyntaxKind.EqualsExpression => literalValue == 0,

            // count <= 0 / count < 1
            SyntaxKind.LessThanOrEqualExpression when countOnLeft => literalValue == 0,
            SyntaxKind.LessThanExpression when countOnLeft => literalValue <= 1,

            // 0 >= count / 1 > count
            SyntaxKind.GreaterThanOrEqualExpression when !countOnLeft => literalValue == 0,
            SyntaxKind.GreaterThanExpression when !countOnLeft => literalValue <= 1,

            _ => false
        };
    }

    private static bool IsCountAccess(ExpressionSyntax expr, ExpressionSyntax sourceCollectionExpression)
    {
        expr = UnwrapParenthesized(expr);

        // list.Count()
        if (expr is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax methodAccess
            && methodAccess.Name.Identifier.Text == "Count"
            && invocation.ArgumentList.Arguments.Count == 0)
        {
            return IsSameExpression(methodAccess.Expression, sourceCollectionExpression);
        }

        // list.Count
        if (expr is MemberAccessExpressionSyntax propertyAccess
            && propertyAccess.Name.Identifier.Text == "Count")
        {
            return IsSameExpression(propertyAccess.Expression, sourceCollectionExpression);
        }

        return false;
    }

    private static bool IsSameExpression(ExpressionSyntax left, ExpressionSyntax right)
    {
        return SyntaxFactory.AreEquivalent(UnwrapParenthesized(left), UnwrapParenthesized(right));
    }

    private static bool TryGetIntLiteral(ExpressionSyntax expr, out int value)
    {
        expr = UnwrapParenthesized(expr);

        if (expr is PrefixUnaryExpressionSyntax unary
            && unary.IsKind(SyntaxKind.UnaryMinusExpression)
            && unary.Operand is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.NumericLiteralExpression)
            && int.TryParse(literal.Token.ValueText, out var negativeLiteral))
        {
            value = -negativeLiteral;
            return true;
        }

        if (expr is LiteralExpressionSyntax numeric
            && numeric.IsKind(SyntaxKind.NumericLiteralExpression)
            && int.TryParse(numeric.Token.ValueText, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Vérifie si l'invocation est enveloppée dans un opérateur ?? (coalesce).
    /// Ex: var item = list.SingleOrDefault() ?? fallback; → item est garanti non-null.
    /// </summary>
    private static bool IsWrappedInCoalesce(InvocationExpressionSyntax invocation)
    {
        var current = invocation.Parent;
        while (current != null)
        {
            if (current is BinaryExpressionSyntax binary
                && binary.IsKind(SyntaxKind.CoalesceExpression)
                && binary.Left.Span.Contains(invocation.Span))
                return true;

            if (current is EqualsValueClauseSyntax or StatementSyntax)
                break;

            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Vérifie si l'identifiant est une réassignation (item = newValue).
    /// </summary>
    private static bool IsReassignment(IdentifierNameSyntax usage)
    {
        return usage.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == usage;
    }

    /// <summary>
    /// Vérifie si l'usage est à l'intérieur du body d'un foreach dont la variable de boucle
    /// porte le même nom que la variable originale. Dans ce cas, il s'agit d'une variable
    /// différente redéclarée dans un scope distinct — la variable originale n'est plus accessible.
    /// Ex: foreach (Item evtPosition in list) { evtPosition.Prop → autre variable, pas un faux positif }
    /// </summary>
    private static bool IsShadowedByForeach(IdentifierNameSyntax usage, string variableName)
    {
        var current = (SyntaxNode)usage;
        while (current != null)
        {
            if (current.Parent is ForEachStatementSyntax forEach
                && forEach.Identifier.Text == variableName
                && forEach.Statement.Span.Contains(usage.Span))
                return true;

            if (current is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax)
                break;

            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Vérifie si l'usage (var.Prop) se trouve dans une chaîne || où un opérande précédent
    /// contient une ConditionalAccessExpression (var?.) sur la même variable.
    /// Quand var est null : var?.X retourne null → null != value → true → court-circuit →
    /// var.Prop n'est jamais évalué. Protège le pattern : var?.X != value || var.Prop
    /// </summary>
    private static bool IsProtectedByConditionalAccessInOrChain(IdentifierNameSyntax usage, string variableName)
    {
        var current = (SyntaxNode)usage;
        while (current != null)
        {
            if (current.Parent is IfStatementSyntax ifStatement
                && ifStatement.Condition.Span.Contains(usage.Span))
            {
                var operands = new List<ExpressionSyntax>();
                CollectOrOperands(ifStatement.Condition, operands);

                foreach (var operand in operands)
                {
                    if (operand.Span.Contains(usage.Span))
                        break; // atteint notre usage sans trouver de var?. avant → pas protégé

                    if (ContainsConditionalAccessFor(operand, variableName))
                        return true;
                }
                break;
            }

            if (current is StatementSyntax or BaseMethodDeclarationSyntax)
                break;

            current = current.Parent;
        }
        return false;
    }

    private static bool ContainsConditionalAccessFor(SyntaxNode expr, string variableName)
    {
        return expr.DescendantNodesAndSelf()
            .OfType<ConditionalAccessExpressionSyntax>()
            .Any(ca => ca.Expression is IdentifierNameSyntax id && id.Identifier.Text == variableName);
    }

    /// <summary>
    /// Vérifie si l'usage est dans la condition d'un if statement qui est lui-même un guard clause,
    /// ET que le null check apparaît avant l'accès membre dans l'ordre d'évaluation de ||.
    /// Ex safe   : if (user == null || !user.Prop || ...) return;  → null check en premier
    /// Ex unsafe : if (!user.Prop || user == null) return;          → accès membre en premier → violation
    /// </summary>
    private static bool IsInsideGuardClauseCondition(SyntaxNode node, string variableName)
    {
        var current = node.Parent;
        SyntaxNode? previous = node;
        while (current != null)
        {
            if (current is IfStatementSyntax ifStatement)
            {
                if (previous != null
                    && ifStatement.Condition.Span.Contains(previous.Span)
                    && IsNullGuardClause(ifStatement, variableName)
                    && NullCheckPrecedesMemberAccess(ifStatement.Condition, node, variableName))
                {
                    return true;
                }
                break;
            }

            if (current is BlockSyntax)
                break;

            previous = current;
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Parcourt les opérandes d'une chaîne || de gauche à droite et vérifie
    /// qu'un null check pour la variable précède l'accès membre (memberAccessNode).
    /// </summary>
    private static bool NullCheckPrecedesMemberAccess(
        ExpressionSyntax condition, SyntaxNode memberAccessNode, string variableName)
    {
        var operands = new List<ExpressionSyntax>();
        CollectOrOperands(condition, operands);

        foreach (var operand in operands)
        {
            if (ContainsNullCheckForVariable(operand, variableName))
                return true;

            if (operand.Span.Contains(memberAccessNode.Span))
                return false; // accès membre atteint sans null check avant
        }

        return false;
    }

    private static void CollectOrOperands(ExpressionSyntax expr, List<ExpressionSyntax> operands)
    {
        switch (expr)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalOrExpression):
                CollectOrOperands(binary.Left, operands);
                CollectOrOperands(binary.Right, operands);
                break;
            case ParenthesizedExpressionSyntax paren:
                CollectOrOperands(paren.Expression, operands);
                break;
            default:
                operands.Add(expr);
                break;
        }
    }

    private static bool ContainsNullCheckForVariable(ExpressionSyntax expr, string variableName)
    {
        // variableName == null / null == variableName
        if (expr is BinaryExpressionSyntax bin
            && bin.IsKind(SyntaxKind.EqualsExpression)
            && IsNullComparison(bin, variableName))
            return true;

        // variableName is null
        if (expr is IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax cp } isExpr
            && cp.Expression.IsKind(SyntaxKind.NullLiteralExpression)
            && IsIdentifier(isExpr.Expression, variableName))
            return true;

        // string.IsNullOrEmpty(variableName) / string.IsNullOrWhiteSpace(variableName)
        if (expr is InvocationExpressionSyntax invocation)
            return IsNullOrEmptyCall(invocation, variableName);

        return false;
    }

    /// <summary>
    /// Vérifie si l'usage est dans la branche safe d'un opérateur ternaire (? :).
    /// - item != null ? item.Prop : default → WhenTrue est safe
    /// - item == null ? default : item.Prop → WhenFalse est safe
    /// </summary>
    private static bool IsInsideConditionalNotNullBranch(SyntaxNode node, string variableName)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is ConditionalExpressionSyntax conditional)
            {
                // item != null ? [item.Prop] : default
                if (conditional.WhenTrue.Span.Contains(node.Span)
                    && ConditionGuaranteesNotNull(conditional.Condition, variableName))
                    return true;

                // item == null ? default : [item.Prop]
                if (conditional.WhenFalse.Span.Contains(node.Span)
                    && ConditionGuaranteesNull(conditional.Condition, variableName))
                    return true;

                break;
            }

            if (current is StatementSyntax)
                break;

            current = current.Parent;
        }
        return false;
    }

    // ── Analyse de flux : protection par null check ──────────────────────────

    /// <summary>
    /// Remonte l'arbre syntaxique pour vérifier si l'usage est à l'intérieur
    /// d'un if dont la condition garantit que la variable n'est pas null.
    /// </summary>
    private static bool IsInsideNullCheck(SyntaxNode node, string variableName)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is IfStatementSyntax ifStatement)
            {
                // if (var != null) { ... usage ... }
                if (ConditionGuaranteesNotNull(ifStatement.Condition, variableName))
                    return true;

                // if (var == null) { throw/return; } else { ... usage ... }
                // Dans le else, la variable est garantie non-null
                if (ConditionGuaranteesNull(ifStatement.Condition, variableName)
                    && ifStatement.Else != null
                    && ifStatement.Else.Span.Contains(node.Span))
                    return true;
            }
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Remonte les blocs parents pour vérifier si un guard clause
    /// (if variable == null return/throw) précède l'usage.
    /// </summary>
    private static bool HasGuardClauseBefore(SyntaxNode usage, string variableName)
    {
        var current = usage;
        while (current != null)
        {
            if (current.Parent is BlockSyntax block)
            {
                foreach (var statement in block.Statements)
                {
                    if (statement.SpanStart >= current.SpanStart)
                        break;

                    if (IsNullGuardClause(statement, variableName))
                        return true;
                }
            }
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Vérifie si un statement est un guard clause : if (condition-null) { return/throw; }
    /// </summary>
    private static bool IsNullGuardClause(StatementSyntax statement, string variableName)
    {
        if (statement is not IfStatementSyntax ifStatement)
            return false;

        if (!ConditionGuaranteesNull(ifStatement.Condition, variableName))
            return false;

        // Le body doit contenir return, throw, continue ou break (DescendantNodesAndSelf pour le cas sans accolades)
        return ifStatement.Statement.DescendantNodesAndSelf()
            .Any(n => n is ReturnStatementSyntax or ThrowStatementSyntax or ThrowExpressionSyntax
                        or ContinueStatementSyntax or BreakStatementSyntax);
    }

    // ── Analyse AST des conditions ────────────────────────────────────────────

    /// <summary>
    /// La condition garantit-elle que var N'EST PAS null ?
    /// Utilisé pour les wrapping checks : if (cond) { usage safe ici }
    /// - var != null → OUI
    /// - var is not null → OUI
    /// - !string.IsNullOrEmpty(var) → OUI
    /// - var != null &amp;&amp; x → OUI (les deux doivent être vraies)
    /// - var != null || x → NON (on peut entrer avec var == null)
    /// </summary>
    private static bool ConditionGuaranteesNotNull(ExpressionSyntax condition, string variableName)
    {
        switch (condition)
        {
            // (expr) → unwrap parenthèses
            case ParenthesizedExpressionSyntax paren:
                return ConditionGuaranteesNotNull(paren.Expression, variableName);

            // a && b → OK si l'un des côtés garantit not-null
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression):
                return ConditionGuaranteesNotNull(binary.Left, variableName)
                    || ConditionGuaranteesNotNull(binary.Right, variableName);

            // a || b → NON (peut entrer avec var == null)
            case BinaryExpressionSyntax when condition.IsKind(SyntaxKind.LogicalOrExpression):
                return false;

            // var != null / null != var
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.NotEqualsExpression):
                return IsNullComparison(binary, variableName);

            // var is not null
            case IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax { Pattern: ConstantPatternSyntax cp } } isExpr
                when cp.Expression.IsKind(SyntaxKind.NullLiteralExpression)
                  && IsIdentifier(isExpr.Expression, variableName):
                return true;

            // !string.IsNullOrEmpty(var) / !string.IsNullOrWhiteSpace(var)
            case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression):
                return IsNullOrEmptyCall(prefix.Operand, variableName);

            default:
                return false;
        }
    }

    /// <summary>
    /// La condition garantit-elle que var EST null (ou vide) ?
    /// Utilisé pour les guard clauses : if (cond) { return/throw; } → après, var est safe
    /// - var == null → OUI
    /// - var is null → OUI
    /// - string.IsNullOrEmpty(var) → OUI
    /// - var == null || x → OUI (retourne si null OU si x)
    /// - var == null &amp;&amp; x → NON (ne retourne que si les deux sont vrais)
    /// </summary>
    private static bool ConditionGuaranteesNull(ExpressionSyntax condition, string variableName)
    {
        switch (condition)
        {
            case ParenthesizedExpressionSyntax paren:
                return ConditionGuaranteesNull(paren.Expression, variableName);

            // a || b → OK si l'un des côtés vérifie null (retourne dès que null)
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalOrExpression):
                return ConditionGuaranteesNull(binary.Left, variableName)
                    || ConditionGuaranteesNull(binary.Right, variableName);

            // a && b → NON (ne retourne que si les DEUX sont vrais → pas garanti)
            case BinaryExpressionSyntax when condition.IsKind(SyntaxKind.LogicalAndExpression):
                return false;

            // var == null / null == var
            // var?.X == null → quand var est null : null == null → true → guard déclenché
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression):
                if (IsNullComparison(binary, variableName)) return true;
                if ((ContainsConditionalAccessFor(binary.Left, variableName) && binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
                 || (ContainsConditionalAccessFor(binary.Right, variableName) && binary.Left.IsKind(SyntaxKind.NullLiteralExpression)))
                    return true;
                return false;

            // var?.X != nonNullValue → quand var est null : null != nonNull → true → guard déclenché
            // (exclut var?.X != null qui retourne false quand var est null)
            case BinaryExpressionSyntax binary2 when binary2.IsKind(SyntaxKind.NotEqualsExpression):
                if ((ContainsConditionalAccessFor(binary2.Left, variableName) && !binary2.Right.IsKind(SyntaxKind.NullLiteralExpression))
                 || (ContainsConditionalAccessFor(binary2.Right, variableName) && !binary2.Left.IsKind(SyntaxKind.NullLiteralExpression)))
                    return true;
                return false;

            // var is null
            case IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax cp } isExpr
                when cp.Expression.IsKind(SyntaxKind.NullLiteralExpression)
                  && IsIdentifier(isExpr.Expression, variableName):
                return true;

            // string.IsNullOrEmpty(var) / string.IsNullOrWhiteSpace(var) (sans !)
            case InvocationExpressionSyntax invocation:
                return IsNullOrEmptyCall(invocation, variableName);

            default:
                return false;
        }
    }

    // ── Helpers AST atomiques ─────────────────────────────────────────────────

    /// <summary>
    /// Vérifie si l'expression binaire compare la variable avec null.
    /// Gère var==null, null==var, var !=null, etc. (pas de dépendance aux espaces).
    /// </summary>
    private static bool IsNullComparison(BinaryExpressionSyntax binary, string variableName)
    {
        return (IsIdentifier(binary.Left, variableName) && binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
            || (IsIdentifier(binary.Right, variableName) && binary.Left.IsKind(SyntaxKind.NullLiteralExpression));
    }

    private static bool IsIdentifier(ExpressionSyntax expr, string name)
    {
        return expr is IdentifierNameSyntax id && id.Identifier.Text == name;
    }

    /// <summary>
    /// Vérifie si l'expression est un appel string.IsNullOrEmpty(var) ou string.IsNullOrWhiteSpace(var).
    /// </summary>
    private static bool IsNullOrEmptyCall(ExpressionSyntax expression, string variableName)
    {
        if (expression is not InvocationExpressionSyntax invocation)
            return false;

        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            _ => null
        };

        if (methodName is not ("IsNullOrEmpty" or "IsNullOrWhiteSpace"))
            return false;

        var args = invocation.ArgumentList.Arguments;
        return args.Count == 1 && IsIdentifier(args[0].Expression, variableName);
    }
}
