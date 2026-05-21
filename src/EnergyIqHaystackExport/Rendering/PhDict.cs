namespace Meshmakers.EnergyIq.HaystackExport.Rendering;

/// <summary>
/// Project Haystack 4 dict — an ordered list of (tag-name, value) pairs.
/// Values are .NET primitives (bool / long / double / string / DateTime) plus
/// the <see cref="PhRef"/>, <see cref="PhMarker"/>, <see cref="PhNumber"/> wrappers.
/// </summary>
public sealed class PhDict
{
    private readonly List<KeyValuePair<string, object?>> _tags = [];

    public IReadOnlyList<KeyValuePair<string, object?>> Tags => _tags;

    public void Set(string name, object? value) => _tags.Add(new KeyValuePair<string, object?>(name, value));

    public bool Has(string name) => _tags.Any(t => t.Key == name);
}

/// <summary>Haystack marker — a tag with no value.</summary>
public sealed record PhMarker
{
    public static readonly PhMarker Instance = new();
    private PhMarker() { }
}

/// <summary>Haystack number with optional unit.</summary>
public sealed record PhNumber(double Value, string? Unit = null);

/// <summary>Haystack ref — points at another dict by id.</summary>
public sealed record PhRef(string Id, string? Dis = null);
