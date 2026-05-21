using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Meshmakers.EnergyIq.HaystackExport.Mapping;

/// <summary>
/// Loads the PH-mapping library from a directory containing <c>_index.yaml</c>
/// plus one YAML file per non-abstract EnergyIQ type.
/// </summary>
public sealed class MappingLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Loads index + all type mappings. Throws if <c>_index.yaml</c> is missing or
    /// if two type files declare the same <c>ckTypeId</c>.
    /// </summary>
    public MappingLibrary Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Mapping directory not found: {directory}");
        }

        var indexPath = Path.Combine(directory, "_index.yaml");
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException("_index.yaml is required at the root of the mapping directory.", indexPath);
        }

        var index = Deserializer.Deserialize<PhLibIndex>(File.ReadAllText(indexPath));

        var typeMappings = new Dictionary<string, TypeMapping>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(directory, "*.yaml"))
        {
            if (Path.GetFileName(file).StartsWith('_'))
            {
                continue; // skip _index.yaml and any other underscore-prefixed metadata files
            }

            var mapping = Deserializer.Deserialize<TypeMapping>(File.ReadAllText(file));
            if (mapping is null || string.IsNullOrWhiteSpace(mapping.CkTypeId))
            {
                throw new InvalidDataException($"Mapping file has no ckTypeId: {file}");
            }

            if (!typeMappings.TryAdd(mapping.CkTypeId, mapping))
            {
                throw new InvalidDataException($"Duplicate mapping for ckTypeId '{mapping.CkTypeId}' in {file}");
            }
        }

        return new MappingLibrary(index, typeMappings);
    }
}

/// <summary>
/// In-memory representation of the mapping library: index + ckTypeId → type mapping.
/// </summary>
public sealed class MappingLibrary
{
    public PhLibIndex Index { get; }
    private readonly Dictionary<string, TypeMapping> _byCkTypeId;

    internal MappingLibrary(PhLibIndex index, Dictionary<string, TypeMapping> byCkTypeId)
    {
        Index = index;
        _byCkTypeId = byCkTypeId;
    }

    public TypeMapping? Find(string ckTypeId) =>
        _byCkTypeId.TryGetValue(ckTypeId, out var mapping) ? mapping : null;

    public IReadOnlyCollection<TypeMapping> All => _byCkTypeId.Values;
}
