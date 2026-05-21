using Meshmakers.EnergyIq.HaystackExport.Mapping;
using Meshmakers.EnergyIq.HaystackExport.Rendering;
using Meshmakers.EnergyIq.HaystackExport.Runtime;

namespace Meshmakers.EnergyIq.HaystackExport;

/// <summary>
/// CLI front-end for the EnergyIQ → Haystack 4 export renderer.
/// Reads an OctoMesh runtime model YAML + a mapping library directory,
/// emits a PH4 JSON grid.
///
/// Usage:
///   dotnet run --project src/EnergyIqHaystackExport -- \
///     --rt data/bim/rt-firmianstrasse.yaml \
///     --mapping src/EnergyIqHaystackMapping/mapping \
///     --output out/firmianstrasse-haystack.json
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

            var output = opts.OutputPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            using var fs = File.Create(output);
            new JsonGridWriter().Write(dicts, mappings.Index, fs);
            Console.Error.WriteLine($"[write] {output}");

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
        Console.Error.WriteLine("  --output <path>    Output JSON grid path");
    }
}

internal sealed record CliOptions(string RtPath, string MappingDir, string OutputPath)
{
    public static CliOptions? Parse(string[] args)
    {
        string? rt = null, mapping = null, output = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--rt" when i + 1 < args.Length: rt = args[++i]; break;
                case "--mapping" when i + 1 < args.Length: mapping = args[++i]; break;
                case "--output" when i + 1 < args.Length: output = args[++i]; break;
            }
        }
        if (rt is null || mapping is null || output is null) return null;
        return new CliOptions(rt, mapping, output);
    }
}
