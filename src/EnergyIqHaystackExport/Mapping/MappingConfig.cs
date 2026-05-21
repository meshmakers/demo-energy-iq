namespace Meshmakers.EnergyIq.HaystackExport.Mapping;

/// <summary>
/// Library-level metadata loaded from _index.yaml.
/// </summary>
public sealed record PhLibIndex
{
    public required PhLib PhLib { get; init; }
    public List<TagSpec> DefaultTags { get; init; } = [];
    public Dictionary<string, string> UnitConventions { get; init; } = [];
    public required IdentityStrategy IdentityStrategy { get; init; }
}

public sealed record PhLib
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string HaystackVersion { get; init; }
    public List<string> BaseLibs { get; init; } = [];
}

public sealed record IdentityStrategy
{
    public required string Preferred { get; init; }
    public required string Fallback { get; init; }
    public required string RefIdPrefix { get; init; }
}

/// <summary>
/// Per-CK-type mapping loaded from one <c>{typeName}.yaml</c> file.
/// </summary>
public sealed record TypeMapping
{
    public required string CkTypeId { get; init; }
    public string? PhSpec { get; init; }
    public List<string> Markers { get; init; } = [];
    public List<TagSpec> Tags { get; init; } = [];
    public List<RefMapping> Refs { get; init; } = [];
    public List<AttributeMapping> Attributes { get; init; } = [];
    public List<PointMapping> Points { get; init; } = [];
}

/// <summary>
/// Inline-flow YAML: <c>{ name: tz, value: "..." }</c> or
/// <c>{ name: dis, sourceAttribute: System/Name }</c>.
/// </summary>
public sealed record TagSpec
{
    public required string Name { get; init; }
    public string? Value { get; init; }
    public string? SourceAttribute { get; init; }
}

public sealed record RefMapping
{
    public required string PhRef { get; init; }
    public required string SourceRole { get; init; }
    public required string Direction { get; init; }   // parent | ancestor | children
    public string? TargetCkTypeId { get; init; }
}

public sealed record AttributeMapping
{
    public required string CkAttribute { get; init; }
    public required string PhTag { get; init; }
    public required string Kind { get; init; }         // Marker | Bool | Number | Str | Ref | Date | Time | DateTime | Coord
    public string? Unit { get; init; }
    public Dictionary<int, string>? EnumMapping { get; init; }
}

/// <summary>
/// Synthetic PH Point derived from a CK attribute on a non-sensor entity
/// (e.g. Valve.Position + Valve.PositionSetpoint → two Points referencing the Valve as equipRef).
/// </summary>
public sealed record PointMapping
{
    public required string CkAttribute { get; init; }
    public List<string> Markers { get; init; } = [];
    public required string Kind { get; init; }
    public string? Unit { get; init; }
    public PointRefTo? RefTo { get; init; }
    public string? NavName { get; init; }
    public bool Writable { get; init; }
    public Dictionary<int, string>? EnumMapping { get; init; }
}

public sealed record PointRefTo
{
    public required string PhRef { get; init; }
    public required string Target { get; init; }       // currently always "self"
}
