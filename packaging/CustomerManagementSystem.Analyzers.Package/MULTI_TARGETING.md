# NuGet Multi-Targeting for Roslyn Analyzers and Source Generators

## Overview

In standard .NET NuGet packages, **multi-targeting** means packaging code compiled for multiple Target Framework Monikers (TFMs)—such as `netstandard2.0`, `net8.0`, and `net9.0`—in a single `.nupkg`. NuGet and MSBuild select the most compatible asset based on the consuming project's target framework.

However, Roslyn analyzers and source generators behave differently:
- They **do not execute within the target application's runtime**.
- They **execute inside the compiler process** (`csc`, Visual Studio, Rider, or `dotnet build`).

As a result, packaging Roslyn components involves two distinct targeting concepts:
1. **Runtime Framework Multi-Targeting (`lib/`)**
2. **Roslyn Compiler Host Multi-Targeting (`analyzers/dotnet/`)**

---

## 1. Runtime Multi-Targeting (`lib/`) vs. Compiler Packaging (`analyzers/`)

### Runtime Assets (`lib/<tfm>/`)
- Placed under `lib/net8.0/`, `lib/netstandard2.0/`, etc.
- Deployed with the consuming application and executed at runtime.
- Used when a package exposes types, APIs, or attributes that application code calls directly.

### Compiler Assets (`analyzers/dotnet/`)
- Placed under `analyzers/dotnet/cs/` or `analyzers/dotnet/roslynX.Y/cs/`.
- Loaded dynamically by the compiler host to perform static analysis, diagnostic reporting, code fixes, and source generation.
- Never referenced as runtime dependencies by the consuming application.

---

## 2. Roslyn Host Version Multi-Targeting (`roslynX.Y`)

Different versions of Visual Studio, Rider, and the .NET SDK run different versions of the Roslyn compiler host. Newer generator features (such as `IIncrementalGenerator`) require minimum compiler host versions.

NuGet allows targeting specific compiler host versions through versioned directory conventions:

| Folder Path | Minimum Roslyn Version | Supported IDEs / SDKs | Capabilities |
| :--- | :--- | :--- | :--- |
| `analyzers/dotnet/cs/` | Roslyn 3.x fallback | Visual Studio 2019 / .NET Core 3.x / .NET 5 | Classic `DiagnosticAnalyzer`, `CodeFixProvider`, `ISourceGenerator` |
| `analyzers/dotnet/roslyn4.0/cs/` | Roslyn 4.0 | Visual Studio 2022 (17.0+) / .NET 6+ SDK | `IIncrementalGenerator` support |
| `analyzers/dotnet/roslyn4.4/cs/` | Roslyn 4.4 | Visual Studio 2022 (17.4+) / .NET 7+ SDK | Enhanced incremental generator pipelines (`ForAttributeWithMetadataName`) |
| `analyzers/dotnet/roslyn4.8/cs/` | Roslyn 4.8 | Visual Studio 2022 (17.8+) / .NET 8+ SDK | Interceptors, compiler pipeline optimizations |

### Host Resolution Algorithm
When MSBuild processes an analyzer package:
1. The compiler checks its own Roslyn host version (e.g., Roslyn `4.12` or `5.0`).
2. It searches the package for the highest `roslynX.Y` folder where $X.Y \le \text{Host Version}$.
3. If a matching `roslynX.Y` folder is found, the compiler loads analyzers exclusively from that folder and ignores others.
4. If no `roslynX.Y` folder matches (or on Roslyn 3.x), it falls back to loading from `analyzers/dotnet/cs/`.

---

## 3. Why `roslyn4.0` in Packaging vs. `5.9.0` in `Directory.Packages.props`

A common question is why `Directory.Packages.props` references `Microsoft.CodeAnalysis.*` version `5.9.0` while the NuGet package folder uses `roslyn4.0`.

```
Directory.Packages.props  -->  Microsoft.CodeAnalysis.CSharp (5.9.0)  [Build-Time Toolchain]
CustomerManagementSystem.Analyzers.Package.csproj  -->  analyzers/dotnet/roslyn4.0/cs/  [Host Compatibility Gate]
```

### Build-Time Package Version (`5.9.0`)
- **What it is:** The version of the Roslyn SDK / compiler libraries referenced when compiling the analyzer and source generator projects.
- **Purpose:** Gives the analyzer developer access to modern compiler API definitions, performance improvements, and bug fixes during compilation.

### Package Folder Version (`roslyn4.0`)
- **What it is:** The **minimum runtime version gate** required for consumer compiler hosts to load the package.
- **Why not `roslyn5.9`?** 
  - `roslynX.Y` folders correspond to Roslyn major.minor compiler versions.
  - If the folder were named `roslyn5.9` (or a non-existent host version), consumers building on Roslyn 4.x (such as Visual Studio 2022 17.0 through 17.12, .NET 6, .NET 7, and .NET 8 SDKs) would fail the threshold check and skip the folder entirely.
  - Specifying `roslyn4.0` ensures any consumer using Roslyn 4.0 or newer (all versions of Visual Studio 2022 and modern .NET SDKs) successfully loads the incremental generator.

---

## 4. Comparison: `System.Text.Json` vs. `CustomerManagementSystem.Analyzers.Package`

| Feature | `System.Text.Json` | `CustomerManagementSystem.Analyzers.Package` |
| :--- | :--- | :--- |
| **Package Purpose** | Runtime library + Source Generator | Analyzer, Code Fix & Incremental Source Generator |
| **Runtime Folders (`lib/`)** | Multi-targets `net8.0`, `net9.0`, `netstandard2.0` | None (`<IncludeBuildOutput>false</IncludeBuildOutput>`) |
| **Compiler Folders (`analyzers/`)** | `analyzers/dotnet/roslyn4.4/cs/` | `analyzers/dotnet/cs/`<br/>`analyzers/dotnet/roslyn4.0/cs/` |
| **Target Framework of Assemblies** | `netstandard2.0` (for analyzers) | `netstandard2.0` (for both analyzers and generators) |
| **Roslyn 3.x Behavior** | No analyzer loaded (runtime APIs only) | Loads fallback `DiagnosticAnalyzer` & `CodeFixProvider` from `cs/` |
| **Roslyn 4.0+ Behavior** | Loads `System.Text.Json.SourceGeneration.dll` from `roslyn4.4/cs` | Loads analyzers, code fixes, and `IIncrementalGenerator` from `roslyn4.0/cs` |

### Package Structure Comparison

```
System.Text.Json.nupkg
├── lib/
│   ├── net8.0/System.Text.Json.dll
│   ├── net9.0/System.Text.Json.dll
│   └── netstandard2.0/System.Text.Json.dll
└── analyzers/
    └── dotnet/
        └── roslyn4.4/
            └── cs/System.Text.Json.SourceGeneration.dll

CustomerManagementSystem.Analyzers.Package.nupkg
└── analyzers/
    └── dotnet/
        ├── cs/
        │   ├── CustomerManagementSystem.Analyzers.dll
        │   └── CustomerManagementSystem.CodeFixProviders.dll
        └── roslyn4.0/
            └── cs/
                ├── CustomerManagementSystem.Analyzers.dll
                ├── CustomerManagementSystem.CodeFixProviders.dll
                └── CustomerManagementSystem.EventStoreSrouceGenerator.dll
```

---

## 5. Best Practices & Rules of Thumb

1. **Target `netstandard2.0`:** Always target `netstandard2.0` in analyzer and source generator projects so assemblies can be loaded by both `dotnet build` (.NET Core runtime) and Visual Studio/MSBuild (.NET Framework runtime).
2. **Use `IncludeBuildOutput = false`:** Set `<IncludeBuildOutput>false</IncludeBuildOutput>` in analyzer-only packages to prevent shipping empty or unintended `lib/` binaries.
3. **Partition by Minimum Host Version:**
   - Put legacy analyzers/fixes compatible with Roslyn 3.x in `analyzers/dotnet/cs/`.
   - Put `IIncrementalGenerator` implementations in `analyzers/dotnet/roslyn4.0/cs/` (or higher if requiring newer APIs like `ForAttributeWithMetadataName` in `roslyn4.4`).
4. **Decouple Package Versions from Folder Gates:** Keep build-time `Microsoft.CodeAnalysis` packages updated without changing folder gate names unless targeting newer host-specific APIs.
