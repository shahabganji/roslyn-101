using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CustomerManagementSystem.Analyzers;

/// <summary>
/// SHG002: every event declared as <c>IEvent&lt;TAggregate&gt;</c> must have a matching
/// <c>Apply(TheEvent)</c> method on <c>TAggregate</c>. If it doesn't, a warning is shown
/// on the aggregate's class name.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PartialMethodExistenceAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SHG002";

    // Extra data attached to each diagnostic, so the code fix knows which event to generate Apply for.
    public const string EventNamePropertyKey = "EventName";
    public const string EventNamespacePropertyKey = "EventNamespace";
    public const string EventMetadataNamePropertyKey = "EventFullyQualifiedName";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: new LocalizableResourceString(nameof(Resources.SHG002Title), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.SHG002MessageFormat), Resources.ResourceManager, typeof(Resources)),
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(nameof(Resources.SHG002Description), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
#if ROSLYN_HAS_COLLECTION_EXPRESSIONS
        [Rule];
#else
        ImmutableArray.Create(Rule);
#endif

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Roslyn calls AnalyzeType for every type (class, record, ...) declared in the project.
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
    }

    /// <summary>
    /// All types declared in this project that implement <c>IEvent&lt;T&gt;</c>.
    /// </summary>
    private static ImmutableArray<INamedTypeSymbol> FindAllEvents(Compilation compilation,
        CancellationToken cancellationToken) =>
        // GetSymbolsWithName with "match everything" returns every type declared in source (nested types included).
        // Passing the token lets Roslyn itself stop walking the project when analysis is cancelled.
        compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type, cancellationToken)
            .OfType<INamedTypeSymbol>()
            .Where(t => t.AllInterfaces.Any(IsGenericIEvent))
            .ToImmutableArray();

    /// <summary>
    /// True when <paramref name="event"/> implements <c>IEvent&lt;aggregate&gt;</c>.
    /// </summary>
    private static bool BelongsTo(INamedTypeSymbol @event, INamedTypeSymbol aggregate) =>
        @event.AllInterfaces.Any(i =>
            IsGenericIEvent(i) && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], aggregate));

    // IEvent<T> is the generic interface with one type argument (the non-generic IEvent is ignored).
    private static bool IsGenericIEvent(INamedTypeSymbol @interface) =>
        @interface is { Name: "IEvent", Arity: 1 };

    /// <summary>
    /// True when the aggregate has a method <c>Apply(TheEvent)</c> with a body.
    /// </summary>
    private static bool HasApplyMethodFor(INamedTypeSymbol aggregate, INamedTypeSymbol @event) =>
        aggregate.GetMembers("Apply")
            .OfType<IMethodSymbol>()
            .Any(m => m.Parameters.Length == 1
                      && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, @event)
                      // A partial method that is only declared (e.g. by a source generator) but never
                      // implemented doesn't count: the user still has to write the body.
                      && !(m.IsPartialDefinition && m.PartialImplementationPart is null));
}
