using System.Threading.Tasks;
using CustomerManagementSystem.Analyzers;
using CustomerManagementSystem.Domain.Fx;
using CustomerManagementSystem.CodeFixProviders;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    CustomerManagementSystem.Analyzers.PartialMethodExistenceAnalyzer,
    CustomerManagementSystem.CodeFixProviders.PartialMethodExistenceCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace CustomerManagementSystem.Analyzers.Tests;

public class PartialMethodExistenceCodeFixProviderTests
{
    [Fact]
    public async Task Adds_apply_method_when_single_event_is_missing()
    {
        const string source = """
                              namespace Demo;

                              public interface IAmAggregateRoot;
                              public interface IEvent<TA> where TA : IAmAggregateRoot, new();

                              public sealed record CustomerRegistered() : IEvent<Customer>;

                              public sealed class Customer : IAmAggregateRoot
                              {
                              }
                              """;

        const string fixedCode = """
                                 namespace Demo;

                                 public interface IAmAggregateRoot;
                                 public interface IEvent<TA> where TA : IAmAggregateRoot, new();

                                 public sealed record CustomerRegistered() : IEvent<Customer>;

                                 public sealed class Customer : IAmAggregateRoot
                                 {
                                     private void Apply(CustomerRegistered @event)
                                     {
                                     }
                                 }
                                 """;

        var expected = Verifier.Diagnostic(PartialMethodExistenceAnalyzer.DiagnosticId)
            .WithLocation(8, 21)
            .WithArguments("Customer", "CustomerRegistered");

        var codeFixTester = DiagnosticTestUtilities.GetCodeFixAnalyzerForOption<
            PartialMethodExistenceAnalyzer,
            PartialMethodExistenceCodeFixProvider,
            DefaultVerifier>(source, [expected], fixedCode);

        codeFixTester.CodeActionEquivalenceKey = "AddApplyMethod_CustomerRegistered";

        await codeFixTester.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Adds_using_and_apply_method_when_event_is_in_different_namespace()
    {
        const string aggregateSource = """
                                       namespace Demo;

                                       public interface IAmAggregateRoot;
                                       public interface IEvent<TA> where TA : IAmAggregateRoot, new();

                                       public sealed class Customer : IAmAggregateRoot
                                       {
                                       }
                                       """;

        const string eventSource = """
                                   namespace Demo.Events;

                                   public sealed record CustomerRegistered() : Demo.IEvent<Demo.Customer>;
                                   """;

        const string fixedAggregateSource = """
                                            using Demo.Events;

                                            namespace Demo;

                                            public interface IAmAggregateRoot;
                                            public interface IEvent<TA> where TA : IAmAggregateRoot, new();

                                            public sealed class Customer : IAmAggregateRoot
                                            {
                                                private void Apply(CustomerRegistered @event)
                                                {
                                                }
                                            }
                                            """;

        var expected = Verifier.Diagnostic(PartialMethodExistenceAnalyzer.DiagnosticId)
            .WithLocation("Aggregate.cs", 6, 21)
            .WithArguments("Customer", "CustomerRegistered");

        var codeFixTest =
            new Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
                PartialMethodExistenceAnalyzer,
                PartialMethodExistenceCodeFixProvider,
                DefaultVerifier>
            {
                TestState =
                {
                    Sources =
                    {
                        ("Aggregate.cs", aggregateSource),
                        ("Event.cs", eventSource)
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
                    AdditionalReferences =
                    {
                        Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(None).Assembly.Location),
                    }
                },
                FixedState =
                {
                    Sources =
                    {
                        ("Aggregate.cs", fixedAggregateSource),
                        ("Event.cs", eventSource)
                    }
                }
            };

        codeFixTest.ExpectedDiagnostics.Add(expected);
        codeFixTest.CodeActionEquivalenceKey = "AddApplyMethod_CustomerRegistered";

        await codeFixTest.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Fix_all_adds_apply_methods_for_all_missing_events_in_diagnostic_order()
    {
        const string source = """
                              namespace Demo;

                              public interface IAmAggregateRoot;
                              public interface IEvent<TA> where TA : IAmAggregateRoot, new();

                              public sealed record CustomerRegistered() : IEvent<Customer>;
                              public sealed record RegistrationConfirmed() : IEvent<Customer>;

                              public sealed class Customer : IAmAggregateRoot
                              {
                              }
                              """;

        const string fixedCode = """
                                 namespace Demo;

                                 public interface IAmAggregateRoot;
                                 public interface IEvent<TA> where TA : IAmAggregateRoot, new();

                                 public sealed record CustomerRegistered() : IEvent<Customer>;
                                 public sealed record RegistrationConfirmed() : IEvent<Customer>;

                                 public sealed class Customer : IAmAggregateRoot
                                 {
                                     private void Apply(CustomerRegistered @event)
                                     {
                                     }

                                     private void Apply(RegistrationConfirmed @event)
                                     {
                                     }
                                 }
                                 """;

        var expectedMissingCustomerRegistered = Verifier.Diagnostic(PartialMethodExistenceAnalyzer.DiagnosticId)
            .WithLocation(9, 21)
            .WithArguments("Customer", "CustomerRegistered");

        var expectedMissingRegistrationConfirmed = Verifier.Diagnostic(PartialMethodExistenceAnalyzer.DiagnosticId)
            .WithLocation(9, 21)
            .WithArguments("Customer", "RegistrationConfirmed");

        // Applying the single code fix selected by the equivalence key only adds the 'CustomerRegistered' method;
        // the 'RegistrationConfirmed' diagnostic remains and has a different equivalence key.
        const string singleFixedCode = """
                                       namespace Demo;

                                       public interface IAmAggregateRoot;
                                       public interface IEvent<TA> where TA : IAmAggregateRoot, new();

                                       public sealed record CustomerRegistered() : IEvent<Customer>;
                                       public sealed record RegistrationConfirmed() : IEvent<Customer>;

                                       public sealed class Customer : IAmAggregateRoot
                                       {
                                           private void Apply(CustomerRegistered @event)
                                           {
                                           }
                                       }
                                       """;

        var codeFixTester = DiagnosticTestUtilities.GetCodeFixAnalyzerForOption<
            PartialMethodExistenceAnalyzer,
            PartialMethodExistenceCodeFixProvider,
            DefaultVerifier>(
            source,
            [expectedMissingCustomerRegistered, expectedMissingRegistrationConfirmed],
            singleFixedCode);

        codeFixTester.FixedState.ExpectedDiagnostics.Add(expectedMissingRegistrationConfirmed);
        codeFixTester.BatchFixedCode = fixedCode;
        codeFixTester.CodeActionEquivalenceKey = "AddApplyMethod_CustomerRegistered";
        codeFixTester.NumberOfIncrementalIterations = 1;
        codeFixTester.NumberOfFixAllIterations = 1;

        await codeFixTester.RunAsync(TestContext.Current.CancellationToken);
    }
}
