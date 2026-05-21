using System.Globalization;
using System.Text;
using Meshmakers.EnergyIq.HaystackExport.Mapping;

namespace Meshmakers.EnergyIq.HaystackExport.Rendering;

/// <summary>
/// Emits a list of PH dicts as Project Haystack 4 Trio (text dict format).
///
/// Format (see project-haystack.org/doc/docHaystack/Trio):
/// <code>
/// id: @energyiq:site-firmianstrasse-31a
/// dis: "Firmianstraße 31A"
/// site
/// tz: "Europe/Vienna"
/// geoCoord: C(47.7833,13.0333)
/// ---
/// id: @energyiq:bldg-hauptgebaeude
/// space
/// building
/// ...
/// </code>
///
/// Marker tags appear bare (no value). Other values use the Zinc literal syntax
/// (numbers with optional unit, refs as <c>@id</c>, strings double-quoted, etc.).
/// Dicts are separated by a line containing exactly "---".
///
/// Unlike Zinc, Trio has no grid header — the lib metadata is emitted as a
/// leading comment block so the file stays self-describing.
/// </summary>
public sealed class TrioWriter : IGridWriter
{
    public void Write(IReadOnlyList<PhDict> dicts, PhLibIndex index, Stream output)
    {
        using var w = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true)
        {
            NewLine = "\n",
        };

        // Leading metadata as a Trio comment block.
        w.Write("// EnergyIQ → Haystack ");
        w.Write(index.PhLib.HaystackVersion);
        w.WriteLine(" Trio export");
        w.Write("// phLib: ");
        w.Write(index.PhLib.Name);
        w.Write(' ');
        w.WriteLine(index.PhLib.Version);
        w.WriteLine();

        var first = true;
        foreach (var dict in dicts)
        {
            if (!first)
            {
                w.WriteLine("---");
            }
            first = false;

            foreach (var (name, value) in dict.Tags)
            {
                if (value is PhMarker)
                {
                    w.WriteLine(name);
                }
                else
                {
                    w.Write(name);
                    w.Write(": ");
                    WriteValue(w, value);
                    w.WriteLine();
                }
            }
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
                // Should be handled by caller; emit as marker keyword for safety.
                w.Write('M');
                return;
            case PhRef r:
                w.Write('@');
                w.Write(r.Id);
                if (!string.IsNullOrEmpty(r.Dis))
                {
                    w.Write(' ');
                    WriteString(w, r.Dis);
                }
                return;
            case PhNumber n:
                WriteNumber(w, n.Value);
                if (n.Unit is not null) w.Write(n.Unit);
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
        var s = value.ToString("G", CultureInfo.InvariantCulture);
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
}
