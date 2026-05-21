using Meshmakers.EnergyIq.HaystackExport.Mapping;
using Meshmakers.EnergyIq.HaystackExport.Runtime;

namespace Meshmakers.EnergyIq.HaystackExport.Rendering;

/// <summary>
/// Renders RT entities to PH4 dicts using the mapping library. One RT entity may
/// produce one main dict (mapped entity) plus N synthetic Point dicts (one per
/// <see cref="PointMapping"/> on the type).
/// </summary>
public sealed class EntityRenderer
{
    private readonly MappingLibrary _mappings;
    private readonly RtModel _model;
    private readonly IdentityStrategy _identity;
    private readonly List<TagSpec> _defaultTags;

    public EntityRenderer(MappingLibrary mappings, RtModel model)
    {
        _mappings = mappings;
        _model = model;
        _identity = mappings.Index.IdentityStrategy;
        _defaultTags = mappings.Index.DefaultTags;
    }

    public IEnumerable<PhDict> RenderAll()
    {
        foreach (var entity in _model.All)
        {
            var mapping = _mappings.Find(entity.CkTypeId);
            if (mapping is null) continue;   // unmapped type — silently skip

            yield return RenderMain(entity, mapping);

            foreach (var pointDict in RenderPoints(entity, mapping))
            {
                yield return pointDict;
            }
        }
    }

    private PhDict RenderMain(RtEntity entity, TypeMapping mapping)
    {
        var dict = new PhDict();

        // id — derived from identity strategy
        dict.Set("id", new PhRef(EntityId(entity)));

        // markers
        foreach (var marker in mapping.Markers)
        {
            dict.Set(marker, PhMarker.Instance);
        }

        // default tags (e.g. tz) — fall through to per-type overrides
        foreach (var tag in _defaultTags)
        {
            if (dict.Has(tag.Name)) continue;
            ApplyTagSpec(entity, tag, dict);
        }

        // per-type tags
        foreach (var tag in mapping.Tags)
        {
            ApplyTagSpec(entity, tag, dict);
        }

        // refs (siteRef / spaceRef / equipRef ...)
        foreach (var refMap in mapping.Refs)
        {
            var target = ResolveRef(entity, refMap);
            if (target is not null)
            {
                dict.Set(refMap.PhRef, new PhRef(EntityId(target)));
            }
        }

        // attributes — flat tags on this dict
        foreach (var attr in mapping.Attributes)
        {
            var value = ReadAttribute(entity, attr.CkAttribute);
            if (value is null) continue;

            var phValue = ConvertAttributeValue(value, attr);
            if (phValue is not null)
            {
                dict.Set(attr.PhTag, phValue);
            }
        }

        return dict;
    }

    private IEnumerable<PhDict> RenderPoints(RtEntity entity, TypeMapping mapping)
    {
        foreach (var point in mapping.Points)
        {
            var value = ReadAttribute(entity, point.CkAttribute);
            // Emit Point dict even when value is null — the point exists structurally;
            // curVal will be omitted but tags / refs still describe the point.

            var dict = new PhDict();
            var pointId = $"{EntityId(entity)}.{point.CkAttribute}";
            dict.Set("id", new PhRef(pointId));

            foreach (var marker in point.Markers)
            {
                dict.Set(marker, PhMarker.Instance);
            }

            foreach (var tag in _defaultTags)
            {
                if (dict.Has(tag.Name)) continue;
                ApplyTagSpec(entity, tag, dict);
            }

            // kind / unit / writable as tags on the point
            dict.Set("kind", point.Kind);
            if (!string.IsNullOrEmpty(point.Unit)) dict.Set("unit", point.Unit);
            if (point.Writable) dict.Set("writable", PhMarker.Instance);
            if (!string.IsNullOrEmpty(point.NavName)) dict.Set("navName", point.NavName);
            // dis = name of the source entity + nav suffix
            dict.Set("dis", BuildPointDis(entity, point));

            // Ref back to the source entity (self-ref)
            if (point.RefTo is { Target: "self" } refTo)
            {
                dict.Set(refTo.PhRef, new PhRef(EntityId(entity)));
            }

            if (value is not null)
            {
                var phValue = ConvertAttributeValue(
                    value,
                    new AttributeMapping
                    {
                        CkAttribute = point.CkAttribute,
                        PhTag = "curVal",
                        Kind = point.Kind,
                        Unit = point.Unit,
                        EnumMapping = point.EnumMapping,
                    });
                if (phValue is not null)
                {
                    dict.Set("curVal", phValue);
                }
            }

            yield return dict;
        }
    }

    private static string BuildPointDis(RtEntity entity, PointMapping point)
    {
        var entityName = entity.Attributes.TryGetValue("System/Name", out var n) ? n?.ToString() : entity.RtId;
        return !string.IsNullOrEmpty(point.NavName)
            ? $"{entityName} {point.NavName}"
            : $"{entityName} {point.CkAttribute}";
    }

    private RtEntity? ResolveRef(RtEntity entity, RefMapping refMap)
    {
        return refMap.Direction switch
        {
            "parent" => _model.FollowAssociation(entity, refMap.SourceRole, refMap.TargetCkTypeId),
            "ancestor" => refMap.TargetCkTypeId is null
                ? null
                : _model.FindAncestor(entity, refMap.TargetCkTypeId),
            "children" => null, // not used yet — refs are outbound by convention
            _ => null,
        };
    }

    private void ApplyTagSpec(RtEntity entity, TagSpec tag, PhDict dict)
    {
        if (tag.Value is not null)
        {
            dict.Set(tag.Name, tag.Value);
            return;
        }

        if (tag.SourceAttribute is not null)
        {
            var raw = ReadAttribute(entity, tag.SourceAttribute);
            if (raw is not null)
            {
                dict.Set(tag.Name, raw.ToString());
            }
        }
    }

    /// <summary>
    /// Reads an attribute by its CK attribute id. Supports dotted paths into Record
    /// values (e.g. <c>AddressValue.Street</c>, <c>ThermalRequirementsRecord.SpaceTemperature</c>).
    /// </summary>
    private object? ReadAttribute(RtEntity entity, string path)
    {
        var segments = path.Split('.');

        // First segment is on the entity. Try with and without the model prefix to be
        // forgiving about how mapping configs spell their attribute IDs.
        if (!TryFindAttribute(entity.Attributes, segments[0], out var current))
        {
            return null;
        }

        for (var i = 1; i < segments.Length; i++)
        {
            if (current is RtRecord rec && TryFindAttribute(rec.Attributes, segments[i], out var next))
            {
                current = next;
                continue;
            }
            return null;
        }

        return current;
    }

    private static bool TryFindAttribute(IReadOnlyDictionary<string, object?> attrs, string key, out object? value)
    {
        // Exact match first
        if (attrs.TryGetValue(key, out value))
        {
            return true;
        }

        // Then case-insensitive on the local part after the slash
        foreach (var kv in attrs)
        {
            var slashIdx = kv.Key.LastIndexOf('/');
            var local = slashIdx >= 0 ? kv.Key[(slashIdx + 1)..] : kv.Key;
            if (string.Equals(local, key, StringComparison.Ordinal))
            {
                value = kv.Value;
                return true;
            }
        }

        // System/Name special-case: stored under "System/Name" exactly
        if (key.Contains('/') && attrs.TryGetValue(key, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static object? ConvertAttributeValue(object value, AttributeMapping attr)
    {
        return attr.Kind switch
        {
            "Marker" => PhMarker.Instance,
            "Bool" => Convert.ToBoolean(value),
            "Number" => new PhNumber(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture), attr.Unit),
            "Str" => attr.EnumMapping is not null && value is long l && attr.EnumMapping.TryGetValue((int)l, out var mapped)
                ? mapped
                : value.ToString(),
            _ => value.ToString(),
        };
    }

    private string EntityId(RtEntity entity)
    {
        string raw;
        if (_identity.Preferred == "GlobalId"
            && entity.Attributes.TryGetValue("EnergyIQ/GlobalId", out var gid)
            && gid is string s
            && !string.IsNullOrEmpty(s))
        {
            raw = s;
        }
        else
        {
            raw = entity.RtId;
        }
        return _identity.RefIdPrefix + raw;
    }
}
