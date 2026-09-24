using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ThingsBooksy.Shared.IntegrationTests.Tooling;

/// <summary>
/// Fleet substrate S4a (decision D-4a): dumps the TYPE SURFACE of every module's Core assembly —
/// domain entities, read models, DbContexts with their DbSets, and factory method signatures —
/// into <c>generated/core-surface.json</c>. The blind test-designer reads this file instead of the
/// source code: names and shapes carry no business logic, so they do not bias test scenarios.
///
/// Run: dotnet test backend/src/Shared/ThingsBooksy.Shared.IntegrationTests --filter Category=Tooling
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
[Trait("Category", "Tooling")]
public class CoreSurfaceExportTests
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public CoreSurfaceExportTests(ThingsBooksyWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExportCoreSurface_FromLoadedModuleAssemblies_WritesGeneratedArtifact()
    {
        _ = _factory.Services; // ensures module assemblies are loaded into the AppDomain

        var coreAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is { } n && n.StartsWith("ThingsBooksy.Modules.") && n.EndsWith(".Core"))
            .OrderBy(a => a.GetName().Name)
            .ToList();

        Assert.NotEmpty(coreAssemblies);

        var modules = new List<object>();
        foreach (var asm in coreAssemblies)
        {
            var moduleName = asm.GetName().Name!.Replace("ThingsBooksy.Modules.", "").Replace(".Core", "");
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t is not null).ToArray()!; }

            var surfaced = types
                .Where(t => (t.IsClass || t.IsEnum) && !t.IsNested && !t.Name.Contains('<'))
                .Where(t => t.Namespace is { } ns && (ns.Contains(".Domain") || ns.Contains(".ReadModels") || ns.Contains(".DAL") || ns.Contains(".Exceptions")))
                .OrderBy(t => t.FullName)
                .Select(DescribeType)
                .ToList();

            modules.Add(new { module = moduleName, assembly = asm.GetName().Name, types = surfaced });
        }

        var payload = new { generated_from = "CoreSurfaceExportTests", modules };
        var outDir = ResolveGeneratedDir();
        Directory.CreateDirectory(outDir);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        await File.WriteAllTextAsync(Path.Combine(outDir, "core-surface.json"), json);
    }

    private static object DescribeType(Type t)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        if (t.IsEnum)
            return new { name = t.Name, ns = t.Namespace, kind = "enum", values = Enum.GetNames(t) };

        var isDbContext = typeof(DbContext).IsAssignableFrom(t);
        var properties = t.GetProperties(Any)
            .Where(p => !p.Name.Contains('<'))
            .Select(p => new
            {
                name = p.Name,
                type = Pretty(p.PropertyType),
                setter = p.SetMethod is null ? "none" : p.SetMethod.IsPublic ? "public" : p.SetMethod.IsAssembly ? "internal" : "private",
            })
            .ToList();

        var factories = t.GetMethods(Any)
            .Where(m => m.IsStatic && (m.IsPublic || m.IsAssembly) && m.ReturnType == t)
            .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{Pretty(p.ParameterType)} {p.Name}"))})")
            .ToList();

        var instanceMethods = t.GetMethods(Any)
            .Where(m => !m.IsStatic && (m.IsPublic || m.IsAssembly) && !m.IsSpecialName && m.DeclaringType == t)
            .Select(m => $"{Pretty(m.ReturnType)} {m.Name}({string.Join(", ", m.GetParameters().Select(p => Pretty(p.ParameterType)))})")
            .ToList();

        return new
        {
            name = t.Name,
            ns = t.Namespace,
            kind = isDbContext ? "dbcontext" : t.IsAbstract ? "abstract" : "class",
            baseType = t.BaseType is { } b && b != typeof(object) ? Pretty(b) : null,
            ctor = t.GetConstructors(Any).Select(c => c.IsPublic ? "public" : c.IsAssembly ? "internal" : "private").Distinct().ToArray(),
            properties,
            factories,
            methods = instanceMethods,
        };
    }

    private static string Pretty(Type t)
    {
        if (t.IsGenericType)
        {
            var name = t.Name[..t.Name.IndexOf('`')];
            return $"{name}<{string.Join(", ", t.GetGenericArguments().Select(Pretty))}>";
        }
        var nullable = Nullable.GetUnderlyingType(t);
        return nullable is null ? t.Name : Pretty(nullable) + "?";
    }

    private static string ResolveGeneratedDir()
    {
        var env = Environment.GetEnvironmentVariable("FLEET_GENERATED_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md"))) dir = dir.Parent;
        return Path.Combine((dir ?? throw new InvalidOperationException("repo root not found")).FullName, "generated");
    }
}
