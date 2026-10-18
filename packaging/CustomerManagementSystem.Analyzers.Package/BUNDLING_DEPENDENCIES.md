# Bundling Dependencies in a Roslyn Analyzer / Source Generator Package

Two different problems, two different solutions:

1. A library your **generator or analyzer** needs while it runs **inside the compiler**.
2. A library the **generated code** needs when the consuming app runs.

---

## 1. Dependency of the generator/analyzer itself (compile-time)

The compiler does **not** restore NuGet dependencies for analyzers. If your generator
references a library (e.g. `Newtonsoft.Json`), the DLL must be shipped **next to the
generator** in the same `analyzers/dotnet/roslynX.Y/cs` folder, or the compiler fails
to load it.

Mark the reference `PrivateAssets="all"` so it does not leak to consumers as a package
dependency, and use `GeneratePathProperty="true"` to get an MSBuild property
(`$(PkgNewtonsoft_Json)`) pointing at the restored package so you can pack its DLL:

```xml
<ItemGroup>
    <PackageReference Include="Newtonsoft.Json" PrivateAssets="all" GeneratePathProperty="true" />
</ItemGroup>

<ItemGroup>
    <!-- your generator -->
    <None Include="bin\$(Configuration)\netstandard2.0\MyGenerator.dll"
          Pack="true" PackagePath="analyzers/dotnet/roslyn5.9/cs" Visible="false" />
    <!-- its dependency, same folder -->
    <None Include="$(PkgNewtonsoft_Json)\lib\netstandard2.0\Newtonsoft.Json.dll"
          Pack="true" PackagePath="analyzers/dotnet/roslyn5.9/cs" Visible="false" />
</ItemGroup>
```

Note: the path property name replaces dots with underscores
(`Newtonsoft.Json` → `$(PkgNewtonsoft_Json)`). Pick the `netstandard2.0` lib of the
dependency so it loads in both VS/MSBuild (.NET Framework) and `dotnet build` (.NET).

---

## 2. Dependency needed by the generated code (runtime)

Here the **consumer's project** must reference the library, so it has to flow as a
normal NuGet dependency in the nuspec — not as a bundled DLL. Do **not** suppress
dependencies for it; declare it as a regular (non-private) `PackageReference` in the
packaging project:

```xml
<PropertyGroup>
    <!-- remove this line, otherwise no dependency reaches the consumer -->
    <!-- <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking> -->
</PropertyGroup>

<ItemGroup>
    <!-- flows to consumers as a package dependency in the nuspec -->
    <PackageReference Include="My.Runtime.Library" />

    <!-- everything analyzer-only stays PrivateAssets="all" so it does NOT flow -->
</ItemGroup>
```

---

## Rule of thumb

| Who needs the library?            | How to ship it                                                    |
| :-------------------------------- | :---------------------------------------------------------------- |
| The generator/analyzer (compiler) | Copy the DLL into the same `analyzers/dotnet/roslynX.Y/cs` folder |
| The generated code (application)  | Regular `PackageReference` → nuspec dependency                    |
