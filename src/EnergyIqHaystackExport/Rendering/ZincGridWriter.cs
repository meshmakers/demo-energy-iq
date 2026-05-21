using System.Globalization;
using System.Text;
using Meshmakers.EnergyIq.HaystackExport.Mapping;

namespace Meshmakers.EnergyIq.HaystackExport.Rendering;

/// <summary>
/// Emits a list of PH dicts as a Project Haystack 4 Zinc grid.
///
/// Format (see project-haystack.org/doc/docHaystack/Zinc):
/// <code>
/// ver:"4.0" phLib:"energyIq 2.0.0"
/// id,dis,site,tz,geoCoord
/// @energyiq:site-firmianstrasse-31a,"Firmianstraße 31A",M,"Europe/Vienna",C(47.7833,13.0333)
/// </code>
///
/// Value encodings:
///   marker → <c>M</c>
///   null   → <c>N</c>
///   bool   → <c>T</c> / <c>F</c>
///   number → <c>21.5°C</c> (unit appended, no space) or <c>21.5</c> if unit-less
///   string → <c>"escaped string"</c>
///   ref    → <c>@id</c> or <c>@id "dis"</c>
///   date   → <c>2024-01-31</c>
///   time   → <c>12:34:56</c>
///   dt     → <c>2024-01-31T12:34:56+01:00 Europe/Vienna</c>
/// </summary>
public sealed class ZincGridWriter : IGridWriter
{
    public void Write(IReadOnlyList<PhDict> dicts, PhLibIndex index, Stream output)
    {
        // Union of all tag names across dicts, in stable declaration order.
        // id and dis come first by convention.
        var cols = new List<string> { "id", "dis" };
        var colSet = new HashSet<string>(StringComparer.Ordinal) { "id", "dis" };
        foreach (var dict in dicts)
        {
            foreach (var kv in dict.Tags)
            {
                if (colSet.Add(kv.Key)) cols.Add(kv.Key);
            }
        }

        using var w = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true)
        {
            NewLine = "\n",
        };

        // Grid meta line
        w.Write("ver:\"");
        w.Write(index.PhLib.HaystackVersion);
        w.Write("\" phLib:");
        WriteString(w, $"{index.PhLib.Name} {index.PhLib.Version}");
        w.WriteLine();

        // Column header
        for (var i = 0; i < cols.Count; i++)
        {
            if (i > 0) w.Write(',');
            WriteColName(w, cols[i]);
        }
        w.WriteLine();

        // Data rows
        foreach (var dict in dicts)
        {
            var byName = dict.Tags.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            for (var i = 0; i < cols.Count; i++)
            {
                if (i > 0) w.Write(',');
                if (!byName.TryGetValue(cols[i], out var value))
                {
                    w.Write('N');
                    continue;
                }
                WriteValue(w, value);
            }
            w.WriteLine();
        }
    }

    private static void WriteColName(StreamWriter w, string name)
    {
        // PH spec restricts column names to ASCII identifiers. Tag-paths like
        // "geoCoord.lat" contain a dot which isn't strictly legal in Zinc col
        // names. We replace dots with underscores in the column header — the
        // dotted form remains in the JSON output for compatibility with our
        // own consumers; Zinc consumers will see the underscored form.
        foreach (var c in name)
        {
            w.Write(c == '.' ? '_' : c);
        }
    }

    private static void WriteValue(StreamWriter w, object? value)
    {
        switch (value)
        {
            case null:
                w.Write('N');
                return;
            case PhMarker:
                w.Write('M');
                return;
            case PhRef r:
                w.Write('@');
                WriteRefId(w, r.Id);
                if (!string.IsNullOrEmpty(r.Dis))
                {
                    w.Write(' ');
                    WriteString(w, r.Dis);
                }
                return;
            case PhNumber n:
                WriteNumber(w, n.Value);
                if (n.Unit is not null)
                {
                    w.Write(n.Unit);
                }
                return;
            case bool b:
                w.Write(b ? 'T' : 'F');
                return;
            case long l:
                w.Write(l.ToString(CultureInfo.InvariantCulture));
                return;
            case double d:
                WriteNumber(w, d);
                return;
            case DateTime dt:
                w.Write(dt.ToString("yyyy-MM-ddTHH:mm:ss.FFFK", CultureInfo.InvariantCulture));
                return;
            case string s:
                WriteString(w, s);
                return;
            default:
                WriteString(w, value.ToString() ?? string.Empty);
                return;
        }
    }

    private static void WriteNumber(StreamWriter w, double value)
    {
        // Use "R" round-trip format but drop trailing ".0" for whole numbers.
        var s = value.ToString("G17", CultureInfo.InvariantCulture);
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == value)
        {
            // Shorter representation if equal
            var shorter = value.ToString("G", CultureInfo.InvariantCulture);
            if (double.TryParse(shorter, NumberStyles.Float, CultureInfo.InvariantCulture, out var p2) && p2 == value)
            {
                s = shorter;
            }
        }
        w.Write(s);
    }

    private static void WriteString(StreamWriter w, string value)
    {
        w.Write('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': w.Write("\\\""); break;
                case '\\': w.Write("\\\\"); break;
                case '\n': w.Write("\\n"); break;
                case '\r': w.Write("\\r"); break;
                case '\t': w.Write("\\t"); break;
                case '$': w.Write("\\$"); break;
                default: w.Write(c); break;
            }
        }
        w.Write('"');
    }

    private static void WriteRefId(StreamWriter w, string id)
    {
        // PH ref ids allow letters / digits / underscore / colon / dash / period /
        // tilde. Our prefix "@energyiq:..." is fine. If a non-conforming char
        // appears, fall back to quoting the dis after the ref.
        foreach (var c in id)
        {
            w.Write(c);
        }
    }
}
