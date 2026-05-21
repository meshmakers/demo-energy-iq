using Meshmakers.EnergyIq.HaystackExport.Mapping;
using Meshmakers.EnergyIq.HaystackExport.Rendering;
using Meshmakers.EnergyIq.HaystackExport.Runtime;

namespace Meshmakers.EnergyIq.HaystackExport;

/// <summary>
/// CLI front-end for the EnergyIQ → Haystack 4 export renderer.
/// Reads an OctoMesh runtime model YAML + a mapping library directory,
/// emits a PH4 grid in JSON, Zinc, or Trio format.
///
/// Usage:
///   dotnet run --project src/EnergyIqHaystackExport -- \
///     --rt data/bim/rt-firmianstrasse.yaml \
///     --mapping src/EnergyIqHaystackMapping/mapping \
///     --output out/firmianstrasse.json \
///     --format json     # or zinc | trio (defaults to extension of --output)
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var opts = CliOptions.Parse(args);
            if (opts is null)
            {
                PrintUsage();
                return 2;
            }

            Console.Error.WriteLine($"[load] RT: {opts.RtPath}");
            var model = new RtModelLoader().Load(opts.RtPath);
            Console.Error.WriteLine($"[load] {model.All.Count()} entities, deps: {string.Join(", ", model.Dependencies)}");

            Console.Error.WriteLine($"[load] Mappings: {opts.MappingDir}");
            var mappings = new MappingLoader().Load(opts.MappingDir);
            Console.Error.WriteLine($"[load] PhLib {mappings.Index.PhLib.Name} {mappings.Index.PhLib.Version}, {mappings.All.Count} type mappings");

            Console.Error.WriteLine("[render] applying mappings...");
            var renderer = new EntityRenderer(mappings, model);
            var dicts = renderer.RenderAll().ToList();
            Console.Error.WriteLine($"[render] {dicts.Count} PH dicts produced");

            IGridWriter writer = opts.Format switch
            {
                OutputFormat.Json => new JsonGridWriter(),
                OutputFormat.Zinc => new ZincGridWriter(),
                OutputFormat.Trio => new TrioWriter(),
                _ => throw new ArgumentOutOfRangeException(nameof(opts.Format)),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(opts.OutputPath))!);
            using var fs = File.Create(opts.OutputPath);
            writer.Write(dicts, mappings.Index, fs);
            Console.Error.WriteLine($"[write] {opts.OutputPath} ({opts.Format})");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("EnergyIQ Haystack Export");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  --rt <path>        OctoMesh runtime model YAML (e.g. rt-firmianstrasse.yaml)");
        Console.Error.WriteLine("  --mapping <dir>    Mapping library directory containing _index.yaml + type files");
        Console.Error.WriteLine("  --output <path>    Output file path");
        Console.Error.WriteLine("  --format <fmt>     json | zinc | trio (default: inferred from output extension)");
    }
}

internal enum OutputFormat { Json, Zinc, Trio }

internal sealed record CliOptions(string RtPath, string MappingDir, string OutputPath, OutputFormat Format)
{
    public static CliOptions? Parse(string[] args)
    {
        string? rt = null, mapping = null, output = null, formatStr = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--rt" when i + 1 < args.Length: rt = args[++i]; break;
                case "--mapping" when i + 1 < args.Length: mapping = args[++i]; break;
                case "--output" when i + 1 < args.Length: output = args[++i]; break;
                case "--format" when i + 1 < args.Length: formatStr = args[++i]; break;
            }
        }
        if (rt is null || mapping is null || output is null) return null;

        var format = ResolveFormat(formatStr, output);
        return new CliOptions(rt, mapping, output, format);
    }

    private static OutputFormat ResolveFormat(string? explicitFormat, string outputPath)
    {
        if (explicitFormat is not null)
        {
            return explicitFormat.ToLowerInvariant() switch
            {
                "json" => OutputFormat.Json,
                "zinc" => OutputFormat.Zinc,
                "trio" => OutputFormat.Trio,
                _ => throw new ArgumentException($"Unknown --format '{explicitFormat}'. Use json | zinc | trio.")
            };
        }

        // Fall back to extension-based inference.
        return Path.GetExtension(outputPath).ToLowerInvariant() switch
        {
            ".json" => OutputFormat.Json,
            ".zinc" => OutputFormat.Zinc,
            ".trio" => OutputFormat.Trio,
            _ => OutputFormat.Json,
        };
    }
}
