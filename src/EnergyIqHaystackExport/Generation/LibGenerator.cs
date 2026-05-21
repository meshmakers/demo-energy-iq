using System.Globalization;
using System.Text;
using Meshmakers.EnergyIq.HaystackExport.Mapping;

namespace Meshmakers.EnergyIq.HaystackExport.Generation;

/// <summary>
/// Emits the EnergyIQ → Project Haystack 4 mapping as a Xeto lib definition
/// (see https://project-haystack.org/doc/docHaystack/Xeto).
///
/// Each non-abstract EnergyIQ CK type becomes one Xeto spec extending the
/// corresponding PH4 base spec. Markers, tag attributes, refs, and synthetic
/// Points (for actuators / plant equipment) are emitted as Xeto slots.
///
/// The output is intended as a starting point for registering EnergyIQ as a
/// formal PH4 lib in tools like SkySpark or FIN Framework. Exact registration
/// syntax varies slightly across tools — adjust the pragma and spec syntax to
/// match the target if needed.
/// </summary>
public sealed class LibGenerator
{
    /// <summary>
    /// Writes a Xeto lib for the given mapping library to <paramref name="output"/>.
    /// </summary>
    public void Write(MappingLibrary library, Stream output)
    {
        using var w = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true)
        {
            NewLine = "\n",
        };

        WritePragma(w, library.Index);
        WriteAllSpecs(w, library);
    }

    private static void WritePragma(StreamWriter w, PhLibIndex index)
    {
        w.WriteLine("// EnergyIQ → Project Haystack 4 lib (Xeto)");
        w.WriteLine("// Generated from src/EnergyIqHaystackMapping/mapping/");
        w.WriteLine("// Do not edit by hand — regenerate via:");
        w.WriteLine("//   dotnet run --project src/EnergyIqHaystackExport -- --mode lib \\");
        w.WriteLine("//     --mapping src/EnergyIqHaystackMapping/mapping --output out/energyIq.xeto");
        w.WriteLine();
        w.WriteLine("pragma: Lib <");
        w.Write("  doc: \"EnergyIQ building energy modeling — generated from CK mapping config\"\n");
        w.WriteLine($"  version: \"{index.PhLib.Version}\"");
        w.WriteLine($"  haystackVersion: \"{index.PhLib.HaystackVersion}\"");
        w.WriteLine("  depends: {");
        foreach (var dep in index.PhLib.BaseLibs)
        {
            w.WriteLine($"    {{ lib: \"{dep}\" }}");
        }
        w.WriteLine("  }");
        w.WriteLine("  org: {");
        w.WriteLine("    dis: \"meshmakers GmbH\"");
        w.WriteLine("    uri: \"https://www.meshmakers.io\"");
        w.WriteLine("  }");
        w.WriteLine(">");
        w.WriteLine();
    }

    private static void WriteAllSpecs(StreamWriter w, MappingLibrary library)
    {
        // Group by purpose for readability. The grouping uses the CK-type-name
        // (after the slash) to classify; lookup order matches what `dotnet build`
        // would also see.
        var groups = new (string Title, Func<TypeMapping, bool> Predicate)[]
        {
            ("Spatial", m => IsAny(m, "Site", "Building", "BuildingStorey", "Space", "ExternalSpace")),
            ("Plant equipment", m => IsAny(m, "HeatPump", "Boiler", "Chiller", "AirHandlingUnit", "Pump", "ThermalEnergyStorage")),
            ("Room terminals", m => IsAny(m, "Radiator", "RadiantSurface", "ChilledBeam", "FanCoilUnit", "AirTerminal", "ElectricHeater")),
            ("Sensors", m => LocalName(m).EndsWith("Sensor", StringComparison.Ordinal)),
            ("Actuators", m => IsAny(m, "Valve", "Damper", "Dimmer", "Motor")),
            ("Building elements", m => IsAny(m, "Wall", "Door", "Window", "ShadingDevice", "Luminaire")),
            ("Photovoltaic", m => IsAny(m, "PhotovoltaicSystem", "PVString", "Inverter", "BatteryStorage")),
        };

        var remaining = library.All.ToList();
        foreach (var (title, pred) in groups)
        {
            var members = remaining.Where(pred).OrderBy(m => LocalName(m), StringComparer.Ordinal).ToList();
            if (members.Count == 0) continue;

            w.WriteLine($"// ==========================================================================");
            w.WriteLine($"// {title}");
            w.WriteLine($"// ==========================================================================");
            w.WriteLine();
            foreach (var spec in members)
            {
                WriteSpec(w, spec);
                remaining.Remove(spec);
            }
        }

        // Any remaining ungrouped specs — emit them at the end.
        if (remaining.Count > 0)
        {
            w.WriteLine("// ==========================================================================");
            w.WriteLine("// Other");
            w.WriteLine("// ==========================================================================");
            w.WriteLine();
            foreach (var spec in remaining.OrderBy(m => LocalName(m), StringComparer.Ordinal))
            {
                WriteSpec(w, spec);
            }
        }
    }

    private static void WriteSpec(StreamWriter w, TypeMapping mapping)
    {
        var name = LocalName(mapping);
        var baseSpec = mapping.PhSpec ?? "ph::Dict";
        // The base spec is qualified as `ph::Site`, `ph::Space`, etc. Strip the
        // ph:: namespace for the extends-clause — Xeto reads it from the lib's
        // declared dependencies.
        var baseShort = baseSpec.Replace("ph::", string.Empty, StringComparison.Ordinal);

        w.WriteLine($"// CK type: {mapping.CkTypeId}");
        w.WriteLine($"{name}: {baseShort} {{");

        // Markers — emit each as `name: Marker`
        foreach (var marker in mapping.Markers)
        {
            w.WriteLine($"  {marker}: Marker");
        }

        // Static tags — emit as Str slots (sourceAttribute) or with a default value
        foreach (var tag in mapping.Tags)
        {
            if (tag.Value is not null)
            {
                w.WriteLine($"  {tag.Name}: Str <val:\"{EscapeXeto(tag.Value)}\">");
            }
            else if (tag.SourceAttribute is not null)
            {
                w.WriteLine($"  {tag.Name}: Str? // populated from CK {tag.SourceAttribute}");
            }
        }

        // Refs
        foreach (var refMap in mapping.Refs)
        {
            var targetSpec = refMap.TargetCkTypeId is null
                ? "Dict"
                : LocalName(refMap.TargetCkTypeId);
            w.WriteLine($"  {refMap.PhRef}: Ref<of:\"{targetSpec}\">?");
        }

        // Attributes (flat tags on the dict)
        foreach (var attr in mapping.Attributes)
        {
            WriteAttributeSlot(w, attr.PhTag, attr.Kind, attr.Unit, optional: true);
        }

        // Synthetic points — these expand into separate Point specs in the export,
        // but in the lib doc we just note them as sub-points.
        if (mapping.Points.Count > 0)
        {
            w.WriteLine();
            w.WriteLine("  // ── Sub-points (emitted as separate Point dicts at export time) ──");
            foreach (var point in mapping.Points)
            {
                var unitNote = point.Unit is null ? string.Empty : $" <unit:\"{point.Unit}\">";
                var markerList = string.Join(" ", point.Markers);
                w.WriteLine($"  // {point.CkAttribute}: {point.Kind}{unitNote} markers=[{markerList}]");
            }
        }

        w.WriteLine("}");
        w.WriteLine();
    }

    private static void WriteAttributeSlot(StreamWriter w, string phTag, string kind, string? unit, bool optional)
    {
        var optMark = optional ? "?" : string.Empty;
        var meta = unit is null ? string.Empty : $" <unit:\"{unit}\">";
        // PH tag paths use dots (e.g. geoCoord.lat) — Xeto slot names can't contain
        // dots, so flatten to underscore in the lib but keep the original in a comment.
        var slotName = phTag.Replace('.', '_');
        if (slotName != phTag)
        {
            w.WriteLine($"  {slotName}: {kind}{meta}{optMark} // exported as \"{phTag}\"");
        }
        else
        {
            w.WriteLine($"  {slotName}: {kind}{meta}{optMark}");
        }
    }

    private static string LocalName(TypeMapping m) => LocalName(m.CkTypeId);

    private static string LocalName(string ckTypeId)
    {
        var slash = ckTypeId.LastIndexOf('/');
        return slash >= 0 ? ckTypeId[(slash + 1)..] : ckTypeId;
    }

    private static bool IsAny(TypeMapping m, params string[] names)
    {
        var local = LocalName(m);
        return names.Any(n => string.Equals(local, n, StringComparison.Ordinal));
    }

    private static string EscapeXeto(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
