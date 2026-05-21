using System.Text.Json;
using Meshmakers.EnergyIq.HaystackExport.Mapping;

namespace Meshmakers.EnergyIq.HaystackExport.Rendering;

/// <summary>
/// Emits a list of PH dicts as a Project Haystack 4 JSON grid.
///
/// Format (simplified, see project-haystack.org/doc/docHaystack/Json):
/// <code>
/// {
///   "_kind": "grid",
///   "meta": { "ver": "4.0", "phLib": "energyIq 2.0.0" },
///   "cols": [ { "name": "id" }, { "name": "dis" }, ... ],
///   "rows": [
///     { "id": { "_kind":"ref", "val":"@energyiq:site-..." }, "dis":"Firmianstraße 31A", ... }
///   ]
/// }
/// </code>
/// Marker tags are encoded as <c>{ "_kind": "marker" }</c>.
/// Number tags are encoded as <c>{ "_kind": "number", "val": 21.5, "unit": "°C" }</c>.
/// </summary>
public sealed class JsonGridWriter : IGridWriter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
    };

    public void Write(IReadOnlyList<PhDict> dicts, PhLibIndex index, Stream output)
    {
        // Column set = union of all tag names in declaration order. id and dis come first.
        var cols = new List<string> { "id", "dis" };
        var colSet = new HashSet<string> { "id", "dis" };
        foreach (var dict in dicts)
        {
            foreach (var kv in dict.Tags)
            {
                if (colSet.Add(kv.Key)) cols.Add(kv.Key);
            }
        }

        using var jw = new Utf8JsonWriter(output, WriterOptions);
        jw.WriteStartObject();
        jw.WriteString("_kind", "grid");

        // meta
        jw.WritePropertyName("meta");
        jw.WriteStartObject();
        jw.WriteString("ver", index.PhLib.HaystackVersion);
        jw.WriteString("phLib", $"{index.PhLib.Name} {index.PhLib.Version}");
        jw.WriteEndObject();

        // cols
        jw.WritePropertyName("cols");
        jw.WriteStartArray();
        foreach (var col in cols)
        {
            jw.WriteStartObject();
            jw.WriteString("name", col);
            jw.WriteEndObject();
        }
        jw.WriteEndArray();

        // rows
        jw.WritePropertyName("rows");
        jw.WriteStartArray();
        foreach (var dict in dicts)
        {
            var byName = dict.Tags.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

            jw.WriteStartObject();
            foreach (var col in cols)
            {
                if (!byName.TryGetValue(col, out var value)) continue;
                jw.WritePropertyName(col);
                WriteValue(jw, value);
            }
            jw.WriteEndObject();
        }
        jw.WriteEndArray();

        jw.WriteEndObject();
        jw.Flush();
    }

    private static void WriteValue(Utf8JsonWriter jw, object? value)
    {
        switch (value)
        {
            case null:
                jw.WriteNullValue();
                return;
            case PhMarker:
                jw.WriteStartObject();
                jw.WriteString("_kind", "marker");
                jw.WriteEndObject();
                return;
            case PhRef r:
                jw.WriteStartObject();
                jw.WriteString("_kind", "ref");
                jw.WriteString("val", r.Id);
                if (r.Dis is not null) jw.WriteString("dis", r.Dis);
                jw.WriteEndObject();
                return;
            case PhNumber n:
                jw.WriteStartObject();
                jw.WriteString("_kind", "number");
                jw.WriteNumber("val", n.Value);
                if (n.Unit is not null) jw.WriteString("unit", n.Unit);
                jw.WriteEndObject();
                return;
            case bool b:
                jw.WriteBooleanValue(b);
                return;
            case long l:
                jw.WriteNumberValue(l);
                return;
            case double d:
                jw.WriteNumberValue(d);
                return;
            case DateTime dt:
                jw.WriteStringValue(dt.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
                return;
            case string s:
                jw.WriteStringValue(s);
                return;
            default:
                jw.WriteStringValue(value.ToString());
                return;
        }
    }
}
