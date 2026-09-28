using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CustomerManagementSystem.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CustomerManagementSystem.CodeFixProviders;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PartialMethodExistenceCodeFixProvider)), Shared]
public class PartialMethodExistenceCodeFixProvider : CodeFixProvider
{
    private const string EquivalenceKeyPrefix = "AddApplyMethod_";

    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [PartialMethodExistenceAnalyzer.DiagnosticId];

    // BatchFixer merges independent text changes and conflicts when several Apply methods are appended to the
    // same class, so all diagnostics of a document are applied sequentially on the same syntax root instead.
    public override FixAllProvider GetFixAllProvider() =>
        FixAllProvider.Create(async (context, document, diagnostics) =>
            diagnostics.IsDefaultOrEmpty
                ? document
                : await AddApplyMethodsAsync(document, diagnostics, context.CancellationToken).ConfigureAwait(false));

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetEventInfo(diagnostic, out var eventInfo))
                continue;

            if (FindTypeDeclaration(root, diagnostic) is null)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: string.Format(CodeFixResources.SHG002CodeFixTitle, eventInfo.Name),
                    token => AddApplyMethodsAsync(context.Document, [diagnostic], token),
                    equivalenceKey: EquivalenceKeyPrefix + eventInfo.Name),
                diagnostic);
        }
    }

    private static async Task<Document> AddApplyMethodsAsync(
        Document document, ImmutableArray<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit || semanticModel is null)
            return document;

        // Resolve every diagnostic against the original tree first, then apply the edits on a tracked root.
        var edits = new List<(TypeDeclarationSyntax TypeDeclaration, EventInfo Event)>();
        var namespacesToImport = new List<string>();
        foreach (var diagnostic in diagnostics)
        {
            if (!TryGetEventInfo(diagnostic, out var eventInfo)
                || FindTypeDeclaration(root, diagnostic) is not { } typeDeclaration)
                continue;

            if (edits.Any(e => e.TypeDeclaration == typeDeclaration && e.Event.Name == eventInfo.Name
                                                                    && e.Event.FullyQualifiedName ==
                                                                    eventInfo.FullyQualifiedName))
                continue;

            edits.Add((typeDeclaration, eventInfo));

            if (RequiresUsing(semanticModel, typeDeclaration, eventInfo)
                && !namespacesToImport.Contains(eventInfo.Namespace))
                namespacesToImport.Add(eventInfo.Namespace);
        }

        if (edits.Count == 0)
            return document;

        var newLine = GetNewLine(compilationUnit);
        var trackedRoot = compilationUnit.TrackNodes(edits.Select(e => e.TypeDeclaration).Distinct());

        foreach (var (typeDeclaration, eventInfo) in edits)
        {
            var current = trackedRoot.GetCurrentNode(typeDeclaration)!;
            var updated = current.AddMembers(CreateApplyMethod(eventInfo, current.Members.Any(), newLine));
            trackedRoot = trackedRoot.ReplaceNode(current, updated);
        }

        trackedRoot = AddUsings(trackedRoot, namespacesToImport, newLine);

        var newDocument = document.WithSyntaxRoot(trackedRoot);
        newDocument = await Simplifier.ReduceAsync(newDocument, Simplifier.Annotation, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return await Formatter.FormatAsync(newDocument, Formatter.Annotation, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static MethodDeclarationSyntax CreateApplyMethod(EventInfo eventInfo, bool hasMembers, string newLine)
    {
        var parameterType = ParseTypeName(eventInfo.FullyQualifiedName)
            .WithAdditionalAnnotations(Simplifier.Annotation);

        var method = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("Apply"))
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(
                Parameter(Identifier(TriviaList(), SyntaxKind.IdentifierToken, "@event", "event", TriviaList()))
                    .WithType(parameterType))))
            .WithBody(Block())
            .WithAdditionalAnnotations(Formatter.Annotation);

        // Separate the new method from the preceding member with a blank line.
        return hasMembers ? method.WithLeadingTrivia(EndOfLine(newLine)) : method;
    }

    private static CompilationUnitSyntax AddUsings(
        CompilationUnitSyntax root, IReadOnlyCollection<string> namespaces, string newLine)
    {
        if (namespaces.Count == 0)
            return root;

        var hadUsings = root.Usings.Any();
        var usings = namespaces
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .Select(n => UsingDirective(ParseName(n))
                .WithTrailingTrivia(EndOfLine(newLine))
                .WithAdditionalAnnotations(Formatter.Annotation))
            .ToArray();

        if (hadUsings)
            return root.AddUsings(usings);

        // Move the leading trivia (e.g. file header) of the first member in front of the new usings,
        // and keep a blank line between the usings and the rest of the file.
        var firstToken = root.GetFirstToken(includeZeroWidth: true);
        var leadingTrivia = firstToken.LeadingTrivia;
        var newRoot = root.ReplaceToken(firstToken, firstToken.WithLeadingTrivia(EndOfLine(newLine)));
        usings[0] = usings[0].WithLeadingTrivia(leadingTrivia);
        return newRoot.WithUsings(List(usings));
    }

    private static bool RequiresUsing(SemanticModel semanticModel, SyntaxNode typeDeclaration, EventInfo eventInfo)
    {
        if (string.IsNullOrEmpty(eventInfo.Namespace))
            return false;

        var prefix = "global::" + eventInfo.Namespace + ".";
        if (!eventInfo.FullyQualifiedName.StartsWith(prefix, System.StringComparison.Ordinal))
            return false;

        // The outermost type of the event must be resolvable by its simple name at the aggregate declaration.
        var typeName = eventInfo.FullyQualifiedName.Substring(prefix.Length);
        var outermostName = typeName.Split('.', '<')[0];
        var outermostFullyQualifiedName = prefix + outermostName;

        return !semanticModel.LookupNamespacesAndTypes(typeDeclaration.SpanStart, name: outermostName)
            .OfType<INamedTypeSymbol>()
            .Any(s => s.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Split('<')[0] == outermostFullyQualifiedName);
    }

    private static TypeDeclarationSyntax? FindTypeDeclaration(SyntaxNode root, Diagnostic diagnostic)
    {
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        return token.Parent as TypeDeclarationSyntax;
    }

    private static bool TryGetEventInfo(Diagnostic diagnostic, out EventInfo eventInfo)
    {
        eventInfo = default;
        if (!diagnostic.Properties.TryGetValue(PartialMethodExistenceAnalyzer.EventNamePropertyKey, out var name)
            || string.IsNullOrEmpty(name)
            || !diagnostic.Properties.TryGetValue(PartialMethodExistenceAnalyzer.EventMetadataNamePropertyKey,
                out var fullyQualifiedName)
            || string.IsNullOrEmpty(fullyQualifiedName))
            return false;

        diagnostic.Properties.TryGetValue(PartialMethodExistenceAnalyzer.EventNamespacePropertyKey, out var @namespace);
        eventInfo = new EventInfo(name!, @namespace ?? string.Empty, fullyQualifiedName!);
        return true;
    }

    private static string GetNewLine(SyntaxNode root) =>
        root.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia)).ToString() is { Length: > 0 } eol
            ? eol
            : "\n";

    private readonly struct EventInfo(string name, string @namespace, string fullyQualifiedName)
    {
        public string Name { get; } = name;
        public string Namespace { get; } = @namespace;
        public string FullyQualifiedName { get; } = fullyQualifiedName;
    }
}
