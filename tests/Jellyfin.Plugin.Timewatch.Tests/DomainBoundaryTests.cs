using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// Jellyfin's assemblies are referenced with ExcludeAssets=runtime and are absent from the
/// test output, so a single Jellyfin type reachable from Domain makes the whole engine seam
/// untestable. This fails loudly at the moment one is introduced rather than later, as a
/// TypeLoadException in an unrelated test.
/// </summary>
public class DomainBoundaryTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void No_domain_type_exposes_a_jellyfin_type()
    {
        var offenders = new List<string>();

        foreach (var name in DomainTypeNames())
        {
            try
            {
                var type = typeof(TimewatchSettings).Assembly.GetType(name, throwOnError: true)!;

                foreach (var subject in type.GetNestedTypes(BindingFlags.Public).Prepend(type))
                {
                    offenders.AddRange(ReferencedTypes(subject)
                        .Where(IsJellyfinType)
                        .Select(referenced => $"{subject.Name} -> {referenced.FullName}"));
                }
            }
            catch (Exception ex) when (ex is TypeLoadException or FileNotFoundException or FileLoadException)
            {
                offenders.Add($"{name} -> will not load: {ex.Message}");
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Every type declared in the Domain namespace, read out of the assembly's metadata
    /// rather than via <c>Assembly.GetTypes</c>: a Domain type referencing a Jellyfin struct
    /// or enum fails to load and so vanishes from a reflection listing, which is exactly the
    /// case this test has to catch. Loading each one by name turns that into a failure.
    /// </summary>
    private static List<string> DomainTypeNames()
    {
        var domain = typeof(TimewatchSettings).Namespace;

        using var stream = File.OpenRead(typeof(TimewatchSettings).Assembly.Location);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        var names = new List<string>();
        foreach (var handle in metadata.TypeDefinitions)
        {
            var definition = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(definition.Namespace) == domain)
            {
                names.Add($"{domain}.{metadata.GetString(definition.Name)}");
            }
        }

        Assert.NotEmpty(names);
        return names;
    }

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }

        foreach (var contract in type.GetInterfaces())
        {
            yield return contract;
        }

        foreach (var member in type.GetMembers(Declared))
        {
            foreach (var referenced in MemberTypes(member))
            {
                yield return referenced;

                foreach (var argument in referenced.GetGenericArguments())
                {
                    yield return argument;
                }
            }
        }
    }

    private static IEnumerable<Type> MemberTypes(MemberInfo member) => member switch
    {
        PropertyInfo property => [property.PropertyType],
        FieldInfo field => [field.FieldType],
        MethodInfo method => [method.ReturnType, .. method.GetParameters().Select(p => p.ParameterType)],
        ConstructorInfo constructor => constructor.GetParameters().Select(p => p.ParameterType),
        _ => []
    };

    private static bool IsJellyfinType(Type type)
    {
        var assembly = type.Assembly.GetName().Name ?? string.Empty;

        return (assembly.StartsWith("Jellyfin", StringComparison.Ordinal)
                || assembly.StartsWith("MediaBrowser", StringComparison.Ordinal))
               && !assembly.StartsWith("Jellyfin.Plugin.Timewatch", StringComparison.Ordinal);
    }
}
