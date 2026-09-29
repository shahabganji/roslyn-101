using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace CustomerManagementSystem.Analyzers.Tests;

public class PartialMethodExistenceAnalyzerTests
{
    [Fact]
    public async Task Detects_diagnostic_when_aggregate_is_missing_apply_for_an_event()
    {
        const string source = """
                              namespace Demo;

                              public interface IAmAggregateRoot;
                              public interface IEvent<TAggregate>;

                              public sealed record CustomerRegistered() : IEvent<Customer>;
                              public sealed record RegistrationConfirmed() : IEvent<Customer>;

                              public sealed partial class Customer : IAmAggregateRoot
                              {
                                  public void Apply(CustomerRegistered @event) { }
                              }
                              """;

        var expected = CSharpAnalyzerVerifier<PartialMethodExistenceAnalyzer, DefaultVerifier>
            .Diagnostic(PartialMethodExistenceAnalyzer.DiagnosticId)
            .WithLocation(9, 29)
            .WithArguments("Customer", "RegistrationConfirmed");

        var analyserTest =
            DiagnosticTestUtilities.GetAnalyzerForOption<PartialMethodExistenceAnalyzer, DefaultVerifier>(
                source, [expected]);

        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Detects_no_diagnostic_when_all_apply_methods_are_present()
    {
        const string source = """
                              namespace Demo;

                              public interface IAmAggregateRoot;
                              public interface IEvent<TAggregate>;

                              public sealed record CustomerRegistered() : IEvent<Customer>;
                              public sealed record RegistrationConfirmed() : IEvent<Customer>;

                              public sealed partial class Customer : IAmAggregateRoot
                              {
                                  public void Apply(CustomerRegistered @event) { }
                                  public void Apply(RegistrationConfirmed @event) { }
                                  public void Apply(object @event) { }
                              }
                              """;

        var analyserTest =
            DiagnosticTestUtilities.GetAnalyzerForOption<PartialMethodExistenceAnalyzer, DefaultVerifier>(
                source, ImmutableArray<DiagnosticResult>.Empty);

        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Detects_multiple_diagnostics_when_multiple_events_are_missing_apply_methods()
    {
        const string source = """
                              namespace Demo;

                              public interface IAmAggregateRoot;
                              public interface IEvent<TAggregate>;

                              public sealed record CustomerRegistered() : IEvent<Customer>;
                              public sealed record RegistrationConfirmed() : IEvent<Customer>;
                              public sealed record EmailUpdated() : IEvent<Customer>;

                              public sealed partial class Customer : IAmAggregateRoot
                              {
                                  public void Apply(CustomerRegistered @event) { }
                              }
                              """;

        var expectedMissingRegistrationConfirmed =
            CSharpAnalyzerVerifier<PartialMethodExistenceAnalyzer, DefaultVerifier>
                .Diagnostic(PartialMethodExistenceAnalyzer.DiagnosticId)
                .WithLocation(10, 29)
                .WithArguments("Customer", "RegistrationConfirmed");

        var expectedMissingEmailUpdated = CSharpAnalyzerVerifier<PartialMethodExistenceAnalyzer, DefaultVerifier>
            .Diagnostic(PartialMethodExistenceAnalyzer.DiagnosticId)
            .WithLocation(10, 29)
            .WithArguments("Customer", "EmailUpdated");

        var analyserTest =
            DiagnosticTestUtilities.GetAnalyzerForOption<PartialMethodExistenceAnalyzer, DefaultVerifier>(
                source, [expectedMissingRegistrationConfirmed, expectedMissingEmailUpdated]);

        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Detects_diagnostic_only_on_the_aggregate_that_owns_the_unhandled_event()
    {
        const string source = """
                              namespace Demo;

                              public interface IAmAggregateRoot;
                              public interface IEvent<TAggregate>;

                              public sealed record CustomerRegistered() : IEvent<Customer>;
                              public sealed record OtherAggregateEvent() : IEvent<OtherAggregate>;

                              public sealed class OtherAggregate : IAmAggregateRoot
                              {
                                  public void Apply(OtherAggregateEvent @event) { }
                              }

                              public sealed partial class Customer : IAmAggregateRoot
                              {
                                  public void Apply(CustomerRegistered @event) { }
                              }
                              """;

        var analyserTest =
            DiagnosticTestUtilities.GetAnalyzerForOption<PartialMethodExistenceAnalyzer, DefaultVerifier>(
                source, []);

        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Detects_no_diagnostic_when_class_does_not_implement_i_am_aggregate_root()
    {
        const string source = """
                              namespace Demo;

                              public interface IAmAggregateRoot;
                              public interface IEvent<TAggregate>;

                              public sealed record CustomerRegistered() : IEvent<Customer>;

                              public sealed partial class Customer
                              {
                              }
                              """;

        var analyserTest =
            DiagnosticTestUtilities.GetAnalyzerForOption<PartialMethodExistenceAnalyzer, DefaultVerifier>(
                source, ImmutableArray<DiagnosticResult>.Empty);

        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task Detects_diagnostic_when_aggregate_is_partial_class_in_multiple_places()
    {
        const string source = """
                              namespace Demo;

                              public interface IAmAggregateRoot;
                              public interface IEvent<TAggregate>;

                              public sealed record CustomerRegistered() : IEvent<Customer>;
                              public sealed record CustomerDeactivated() : IEvent<Customer>;
                              public sealed record RegistrationConfirmed() : IEvent<Customer>;

                              public sealed partial class Customer : IAmAggregateRoot
                              {
                                  public void Apply(CustomerRegistered @event) { }
                              }
                              
                              public sealed partial class Customer : IAmAggregateRoot
                              {
                                  public void Apply(CustomerDeactivated @event) { }
                              }
                              """;

        var firstExpectationLocation = CSharpAnalyzerVerifier<PartialMethodExistenceAnalyzer, DefaultVerifier>
            .Diagnostic(PartialMethodExistenceAnalyzer.DiagnosticId)
            .WithLocation(10, 29)
            .WithArguments("Customer", "RegistrationConfirmed");

        var secondExpectationLocation = CSharpAnalyzerVerifier<PartialMethodExistenceAnalyzer, DefaultVerifier>
            .Diagnostic(PartialMethodExistenceAnalyzer.DiagnosticId)
            .WithLocation(15, 29)
            .WithArguments("Customer", "RegistrationConfirmed");

        
        var analyserTest =
            DiagnosticTestUtilities.GetAnalyzerForOption<PartialMethodExistenceAnalyzer, DefaultVerifier>(
                source, [firstExpectationLocation, secondExpectationLocation]);

        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }

}