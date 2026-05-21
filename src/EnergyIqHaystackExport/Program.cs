using Meshmakers.EnergyIq.HaystackExport.Generation;
using Meshmakers.EnergyIq.HaystackExport.Mapping;
using Meshmakers.EnergyIq.HaystackExport.Rendering;
using Meshmakers.EnergyIq.HaystackExport.Runtime;

namespace Meshmakers.EnergyIq.HaystackExport;

/// <summary>
/// CLI front-end for the EnergyIQ → Haystack 4 tooling. Two modes:
///
/// <list type="bullet">
/// <item><c>--mode export</c> (default) reads an OctoMesh runtime model YAML and
///   emits a PH4 grid in JSON, Zinc, or Trio format.</item>
/// <item><c>--mode lib</c> reads only the mapping library and emits a Xeto lib
///   spec file describing the EnergyIQ→PH4 type mapping.</item>
/// </list>
///
/// Examples:
/// <code>
///   # Export instances:
///   dotnet run -- --mode export \
///     --rt data/bim/rt-firmianstrasse.yaml \
///     --mapping src/EnergyIqHaystackMapping/mapping \
///     --output out/firmianstrasse.json
///
///   # Generate lib:
///   dotnet run -- --mode lib \
///     --mapping src/EnergyIqHaystackMapping/mapping \
///     --output out/energyIq.xeto
/// </code>
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

            Console.Error.WriteLine($"[load] Mappings: {opts.MappingDir}");
            var mappings = new MappingLoader().Load(opts.MappingDir);
            Console.Error.WriteLine($"[load] PhLib {mappings.Index.PhLib.Name} {mappings.Index.PhLib.Version}, {mappings.All.Count} type mappings");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(opts.OutputPath))!);

            switch (opts.Mode)
            {
                case ToolMode.Export:
                    return RunExport(opts, mappings);

                case ToolMode.Lib:
                    return RunLib(opts, mappings);

                default:
                    throw new ArgumentOutOfRangeException(nameof(opts.Mode));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static int RunExport(CliOptions opts, MappingLibrary mappings)
    {
        if (opts.RtPath is null)
        {
            Console.Error.WriteLine("ERROR: --rt <path> is required for --mode export.");
            return 2;
        }

        Console.Error.WriteLine($"[load] RT: {opts.RtPath}");
        var model = new RtModelLoader().Load(opts.RtPath);
        Console.Error.WriteLine($"[load] {model.All.Count()} entities, deps: {string.Join(", ", model.Dependencies)}");

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

        using var fs = File.Create(opts.OutputPath);
        writer.Write(dicts, mappings.Index, fs);
        Console.Error.WriteLine($"[write] {opts.OutputPath} ({opts.Format})");
        return 0;
    }

    private static int RunLib(CliOptions opts, MappingLibrary mappings)
    {
        Console.Error.WriteLine("[generate] producing Xeto lib...");
        using var fs = File.Create(opts.OutputPath);
        new LibGenerator().Write(mappings, fs);
        Console.Error.WriteLine($"[write] {opts.OutputPath} (Xeto lib)");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("EnergyIQ Haystack Export");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  --mode <mode>      export | lib (default: export)");
        Console.Error.WriteLine("  --rt <path>        Runtime model YAML — required for --mode export");
        Console.Error.WriteLine("  --mapping <dir>    Mapping library directory (required)");
        Console.Error.WriteLine("  --output <path>    Output file path (required)");
        Console.Error.WriteLine("  --format <fmt>     export only: json | zinc | trio (default: inferred from extension)");
    }
}

internal enum ToolMode { Export, Lib }
internal enum OutputFormat { Json, Zinc, Trio }

internal sealed record CliOptions(ToolMode Mode, string? RtPath, string MappingDir, string OutputPath, OutputFormat Format)
{
    public static CliOptions? Parse(string[] args)
    {
        string? modeStr = null, rt = null, mapping = null, output = null, formatStr = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode" when i + 1 < args.Length: modeStr = args[++i]; break;
                case "--rt" when i + 1 < args.Length: rt = args[++i]; break;
                case "--mapping" when i + 1 < args.Length: mapping = args[++i]; break;
                case "--output" when i + 1 < args.Length: output = args[++i]; break;
                case "--format" when i + 1 < args.Length: formatStr = args[++i]; break;
            }
        }
        if (mapping is null || output is null) return null;

        var mode = modeStr?.ToLowerInvariant() switch
        {
            null or "export" => ToolMode.Export,
            "lib" => ToolMode.Lib,
            _ => throw new ArgumentException($"Unknown --mode '{modeStr}'. Use export | lib."),
        };

        var format = ResolveFormat(formatStr, output);
        return new CliOptions(mode, rt, mapping, output, format);
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

        return Path.GetExtension(outputPath).ToLowerInvariant() switch
        {
            ".json" => OutputFormat.Json,
            ".zinc" => OutputFormat.Zinc,
            ".trio" => OutputFormat.Trio,
            _ => OutputFormat.Json,
        };
    }
}
