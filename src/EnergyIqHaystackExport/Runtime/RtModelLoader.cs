using YamlDotNet.RepresentationModel;

namespace Meshmakers.EnergyIq.HaystackExport.Runtime;

/// <summary>
/// Loads an OctoMesh runtime model YAML file into an in-memory <see cref="RtModel"/>.
/// The file follows the schema at <c>https://schemas.meshmakers.cloud/runtime-model.schema.json</c>:
/// <code>
/// entities:
///   - rtId: ...
///     ckTypeId: ...
///     attributes:
///       - id: ...
///         value: ...
///     associations:
///       - roleId: ...
///         targetRtId: ...
///         targetCkTypeId: ...
/// </code>
/// Records and record arrays are detected by a child <c>ckRecordId</c> key.
/// Arrays inside RecordArray attributes that use the OctoMesh bracket-string
/// encoding (e.g. <c>'[0, 1, 2]'</c>) are returned as the raw string — the
/// renderer can parse on demand.
/// </summary>
public sealed class RtModelLoader
{
    public RtModel Load(string yamlPath)
    {
        using var reader = new StreamReader(yamlPath);
        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException($"Empty or non-mapping YAML root: {yamlPath}");
        }

        var model = new RtModel();

        // Dependencies (optional)
        if (root.Children.TryGetValue(new YamlScalarNode("dependencies"), out var depsNode)
            && depsNode is YamlSequenceNode depsSeq)
        {
            foreach (var dep in depsSeq.Children.OfType<YamlScalarNode>())
            {
                if (dep.Value is not null) model.Dependencies.Add(dep.Value);
            }
        }

        // Entities
        if (!root.Children.TryGetValue(new YamlScalarNode("entities"), out var entitiesNode)
            || entitiesNode is not YamlSequenceNode entitiesSeq)
        {
            throw new InvalidDataException($"Missing 'entities' sequence in: {yamlPath}");
        }

        foreach (var entityNode in entitiesSeq.Children.OfType<YamlMappingNode>())
        {
            model.Add(ParseEntity(entityNode));
        }

        return model;
    }

    private static RtEntity ParseEntity(YamlMappingNode entityNode)
    {
        var rtId = RequireScalar(entityNode, "rtId");
        var ckTypeId = RequireScalar(entityNode, "ckTypeId");

        var entity = new RtEntity { RtId = rtId, CkTypeId = ckTypeId };

        if (TryGetChild(entityNode, "attributes") is YamlSequenceNode attrsSeq)
        {
            foreach (var attrNode in attrsSeq.Children.OfType<YamlMappingNode>())
            {
                var id = RequireScalar(attrNode, "id");
                var valueNode = TryGetChild(attrNode, "value");
                entity.Attributes[id] = ParseValue(valueNode);
            }
        }

        if (TryGetChild(entityNode, "associations") is YamlSequenceNode assocsSeq)
        {
            foreach (var assocNode in assocsSeq.Children.OfType<YamlMappingNode>())
            {
                entity.Associations.Add(new RtAssociation
                {
                    RoleId = RequireScalar(assocNode, "roleId"),
                    TargetRtId = RequireScalar(assocNode, "targetRtId"),
                    TargetCkTypeId = OptionalScalar(assocNode, "targetCkTypeId"),
                });
            }
        }

        return entity;
    }

    private static object? ParseValue(YamlNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case YamlScalarNode scalar:
                return ParseScalar(scalar);
            case YamlMappingNode mapping when HasKey(mapping, "ckRecordId"):
                return ParseRecord(mapping);
            case YamlSequenceNode sequence:
            {
                // A RecordArray is a sequence of mappings with ckRecordId.
                // A primitive array is a sequence of scalars.
                var list = new List<object?>(sequence.Children.Count);
                foreach (var child in sequence.Children)
                {
                    list.Add(ParseValue(child));
                }
                return list;
            }
            case YamlMappingNode:
                throw new InvalidDataException("Mapping value without ckRecordId is not supported.");
            default:
                throw new InvalidDataException($"Unsupported YAML node type: {node.GetType().Name}");
        }
    }

    private static RtRecord ParseRecord(YamlMappingNode mapping)
    {
        var record = new RtRecord
        {
            CkRecordId = RequireScalar(mapping, "ckRecordId"),
        };

        if (TryGetChild(mapping, "attributes") is YamlSequenceNode attrsSeq)
        {
            foreach (var attrNode in attrsSeq.Children.OfType<YamlMappingNode>())
            {
                var id = RequireScalar(attrNode, "id");
                var valueNode = TryGetChild(attrNode, "value");
                record.Attributes[id] = ParseValue(valueNode);
            }
        }

        return record;
    }

    private static object? ParseScalar(YamlScalarNode scalar)
    {
        var s = scalar.Value;
        if (s is null) return null;

        // Heuristic typing — YamlDotNet's representation model is untyped, so we
        // map common shapes to .NET primitives. Quoted strings come through with
        // Style != Plain; treat those as strings always.
        if (scalar.Style is YamlDotNet.Core.ScalarStyle.SingleQuoted or YamlDotNet.Core.ScalarStyle.DoubleQuoted)
        {
            return s;
        }

        if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(s, "null", StringComparison.OrdinalIgnoreCase) || s.Length == 0) return null;

        if (long.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }

        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        return s;
    }

    private static string RequireScalar(YamlMappingNode parent, string key) =>
        OptionalScalar(parent, key) ?? throw new InvalidDataException($"Missing required '{key}' in mapping.");

    private static string? OptionalScalar(YamlMappingNode parent, string key)
    {
        if (parent.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar)
        {
            return scalar.Value;
        }
        return null;
    }

    private static YamlNode? TryGetChild(YamlMappingNode parent, string key) =>
        parent.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;

    private static bool HasKey(YamlMappingNode parent, string key) =>
        parent.Children.ContainsKey(new YamlScalarNode(key));
}
