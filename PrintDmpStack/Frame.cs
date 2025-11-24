using System.Diagnostics;

namespace DmpStack;

[DebuggerDisplay("{ToHumanReadable()}")]
public class Frame
{
    public ulong ModuleBaseAddress { get; set; }
    public ulong InstructionAddress { get; set; }
    public string? ModuleName { get; set; }
    public string? SymbolName { get; set; }

    public ulong OffsetInModule => InstructionAddress - ModuleBaseAddress;

    public string ToHumanReadable() => String.IsNullOrWhiteSpace(ModuleName) ? "<unknown>" : $"{ModuleName}!{SymbolName}";
}
