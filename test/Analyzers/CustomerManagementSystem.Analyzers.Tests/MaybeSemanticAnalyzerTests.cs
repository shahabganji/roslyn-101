using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace CustomerManagementSystem.Analyzers.Tests;

public class MaybeSemanticAnalyzerTests
{
    [Fact]
    public async Task Detects_diagnostic_for_throwing_error_when_a_method_returns_Maybe_of_T()
    {
        // Arrange
        const string source = """
                              using System;
                              using CustomerManagementSystem.Domain.Fx;

                              public class Program
                              {
                                  public Maybe<int> GetValue(string number)
                                  {
                                     throw new InvalidOperationException("Could not parse the number");
                                  }
                              }
                              """;

        var analyserTest = DiagnosticTestUtilities.GetAnalyzerForOption<MaybeSemanticAnalyzer, DefaultVerifier>(
            source, [CSharpAnalyzerVerifier<MaybeSemanticAnalyzer, DefaultVerifier>.Diagnostic().WithLocation(8, 8)]);

        // Assert
        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Detects_diagnostic_for_throwing_error_when_a_method_returns_Task_Maybe_of_T()
    {
        // Arrange
        const string source = """
                              using System;
                              using System.Threading.Tasks;
                              using CustomerManagementSystem.Domain.Fx;

                              public class Program
                              {
                                  public Task<Maybe<int>> GetValue(string number)
                                  {
                                     throw new InvalidOperationException("Could not parse the number");
                                  }
                              }
                              """;

        var analyserTest = DiagnosticTestUtilities.GetAnalyzerForOption<MaybeSemanticAnalyzer, DefaultVerifier>(
            source, [CSharpAnalyzerVerifier<MaybeSemanticAnalyzer, DefaultVerifier>.Diagnostic().WithLocation(9, 8)]);

        // Assert
        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task Detects_diagnostic_for_throwing_error_on_a_nested_level_when_a_method_returns_Maybe_of_T()
    {
        // Arrange
        const string source = """
                              using System;
                              using CustomerManagementSystem.Domain.Fx;

                              public class Program
                              {
                                  public Maybe<int> GetValue(string number)
                                  {
                                      var parsed = int.TryParse(number, out var n);
                                      if (!parsed)
                                          throw new InvalidOperationException("Could not parse the number");
                              
                                      return n;
                                  }
                              }
                              """;

        var analyserTest = DiagnosticTestUtilities.GetAnalyzerForOption<MaybeSemanticAnalyzer, DefaultVerifier>(
            source, [CSharpAnalyzerVerifier<MaybeSemanticAnalyzer, DefaultVerifier>.Diagnostic().WithLocation(10, 13)]);

        // Assert
        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Detects_NO_diagnostic_for_throwing_error_when_a_method_returns_normal_values()
    {
        // Arrange
        const string source = """
                              using System;
                              using CustomerManagementSystem.Domain.Fx;

                              public class Program
                              {
                                  public int GetValue(string number)
                                  {
                                      var parsed = int.TryParse(number, out var n);
                                      if (!parsed)
                                          throw new InvalidOperationException("Could not parse the number");
                              
                                      return n;
                                  }
                              }
                              """;

        var analyserTest = DiagnosticTestUtilities.GetAnalyzerForOption<MaybeSemanticAnalyzer, DefaultVerifier>(
            source, ImmutableArray<DiagnosticResult>.Empty);

        // Assert
        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Detects_NO_diagnostic_and_does_not_crash_for_throwing_error_inside_a_constructor()
    {
        // Arrange
        const string source = """
                              using System;

                              public class Program
                              {
                                  public Program(string value)
                                  {
                                      if (value == null)
                                          throw new ArgumentNullException(nameof(value));
                                  }
                              }
                              """;

        var analyserTest = DiagnosticTestUtilities.GetAnalyzerForOption<MaybeSemanticAnalyzer, DefaultVerifier>(
            source, ImmutableArray<DiagnosticResult>.Empty);

        // Assert
        await analyserTest.RunAsync(TestContext.Current.CancellationToken);
    }
}
