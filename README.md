# PrintDmpStack.exe
A Windows program to print the stack from a crash dump (.dmp) file, using the [Microsoft Debugger Engine](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/introduction).

The primary usage of this program is in automation. So a Nuget package [DumbPrograms.DmpStack](https://www.nuget.org/packages/DumbPrograms.DmpStack/) with the following JSON serialization related classes are also provided.

1. `DmpStack.Frame`
1. `DmpStack.FramesJsonSerializationContext`

This program is developed in C#, using the interop library [DumbPrograms.DbgEng](https://www.nuget.org/packages/DumbPrograms.DbgEng/).

## Arguments

This program needs at least 3 positional args, or use a `params.txt` file to specify them in separated lines:

    0. The path to the .dmp file
  
    1. Image paths:
        Semicolon-separated list of folders to the possible modules in the crashed application.
        This arg is directly passed to dbgeng.
  
    2. Symbol paths:
        Semicolon-separated list of folders to the possible symbols for the crashed application.
        This arg is directly passed to dbgeng.
  
    3. Output format:
        0 - compacted json, that's default for automation
        1 - pretty-printed json
        2 - human readable

## Build

Open the `PrintDmpStack.slnx` file in Visual Studio.
