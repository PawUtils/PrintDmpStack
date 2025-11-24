using System.Runtime.InteropServices;
using System.Text.Json;
using DmpStack;

if (args.Length < 3)
{
    if (!File.Exists("params.txt") || File.ReadAllLines("params.txt") is not { Length: >= 3 } lines)
    {
        Console.Error.WriteLine("""
            Need at least 3 positional args, or use a params.txt file to specify them in separated lines (cuz the launchProfile.json is buggy):

                0. The path to the .dmp file

                1. Image paths:
                    Semicolon-separated list of folders to the possible modules in the crashed application.
                    This arg is directly passed to dbgeng.

                2. Symbol paths:
                    Semicolon-separated list of folders to the possible symbols (.pdb files) for the crashed application.
                    This arg is directly passed to dbgeng.

                3. Output format:
                    0 - compacted json, that's default for automation
                    1 - pretty-printed json
                    2 - human readable
            """);

        return -2;
    }
    else
    {
        args = lines;
    }
}

try
{
    using var da = DumpAnalyzer.Create(dumpFile: args[0], imagePaths: args[1], symbolPaths: args[2]);
    using var stdOut = Console.OpenStandardOutput();

    var stack = da.GetExceptionStackTrace().ToArray();

    if (args.Length < 4 || args[3] == "0")
    {
        JsonSerializer.Serialize(stdOut, stack, FramesJsonSerializationContext.Default.FrameArray);
    }
    else if (args[3] == "1")
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        JsonSerializer.Serialize(stdOut, stack, new FramesJsonSerializationContext(options).FrameArray);
    }
    else if (args[3] == "2")
    {
        foreach (var frame in stack)
        {
            Console.WriteLine(frame.ToHumanReadable());
        }
    }
    else
    {
        Console.Error.WriteLine("Please specify a valid output format.");

        return -3;
    }

    return 0;
}
catch (Exception e)
{
    Console.Error.Write(e);

    return e is COMException com ? com.ErrorCode : -4;
}
