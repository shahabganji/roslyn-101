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
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CustomerManagementSystem.CodeFixProviders;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MaybeCodeFixProvider)), Shared]
public class MaybeCodeFixProvider : CodeFixProvider
{
    // Specify the diagnostic IDs of analyzers that are expected to be linked.
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
#if ROSLYN_HAS_COLLECTION_EXPRESSIONS
        [MaybeSemanticAnalyzer.DiagnosticId];
#else
        ImmutableArray.Create(MaybeSemanticAnalyzer.DiagnosticId);
#endif

    // If you don't need the 'fix all' behaviour, return null.
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // We link only one diagnostic and assume there is only one diagnostic in the context.
        var diagnostic = context.Diagnostics.Single();

        // 'SourceSpan' of 'Location' is the highlighted area. We're going to use this area to find the 'SyntaxNode' to replace.
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Get the root of Syntax Tree that contains the highlighted diagnostic.
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // Find SyntaxNode corresponding to the diagnostic.
        var diagnosticNode = root?.FindNode(diagnosticSpan);

        // To get the required metadata, we should match the Node to the specific type: 'ThrowStatementSyntax'.
        if (diagnosticNode is not ThrowStatementSyntax throwStatementSyntax)
            return;

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(CodeFixResources.SHG001CodeFixTitle, "Maybe.None", "throw"),
                token => ReplaceThrowWithReturnStatement(context.Document, throwStatementSyntax, token),
                equivalenceKey: nameof(CodeFixResources.SHG001CodeFixTitle)),
            diagnostic
        );
    }

    private static Task<Document> ReplaceThrowWithReturnStatement(
        Document document, CSharpSyntaxNode throwSyntaxNode, CancellationToken cancellationToken)
    {
        return Task.FromResult(document);
    }
}
