using Meshmakers.EnergyIq.HaystackExport.Mapping;

namespace Meshmakers.EnergyIq.HaystackExport.Rendering;

/// <summary>
/// Writes a sequence of PH dicts to a stream in one of the PH4 wire formats
/// (JSON / Zinc / Trio).
/// </summary>
public interface IGridWriter
{
    void Write(IReadOnlyList<PhDict> dicts, PhLibIndex index, Stream output);
}
