using System.Runtime.InteropServices;
using System.Text;
using Interop.DbgEng;

namespace DmpStack;

sealed class DumpAnalyzer : IDisposable
{
    private bool IsDisposed;
    private IDebugClient Client;

    private DumpAnalyzer(IDebugClient client)
    {
        Client = client;
    }

    public static DumpAnalyzer Create(string dumpFile, string imagePaths, string symbolPaths)
    {
        var root = IDebugClient.Create();

        root.OpenDumpFile(dumpFile);

        ((IDebugControl)root).WaitForEvent(0, 0);

        var symbols = (IDebugSymbols)root;

        symbols.SetImagePath(imagePaths);
        symbols.SetSymbolPath(symbolPaths);

        return new DumpAnalyzer(root);
    }

    public List<Frame> GetExceptionStackTrace()
    {
        var symbols = (IDebugSymbols)Client;
        var control = (IDebugControl4)Client;

        control.GetStoredEventInformation(out _, out _, out _
                                         , null, 0, out var contextSize
                                         , null, 0, out var extraInfoSize
                                         );

        var context = new byte[contextSize];
        var extraInfo = new byte[extraInfoSize];
        control.GetStoredEventInformation(out _, out _, out _
                                         , context, contextSize, out _
                                         , extraInfo, extraInfoSize, out _
                                         );

        uint maxFrames = 150;
        byte[] frameContexts = new byte[maxFrames * contextSize];
        DebugStackFrame[] stackFrames = new DebugStackFrame[maxFrames];
        control.GetContextStackTrace(context, contextSize
                                    , stackFrames, maxFrames
                                    , frameContexts, (uint)frameContexts.Length, contextSize
                                    , out var frames);

        const int nameSpanSize = 512;

        Span<byte> imageNameSpan = stackalloc byte[nameSpanSize];
        Span<byte> moduleNameSpan = stackalloc byte[nameSpanSize];
        Span<byte> loadedImageNameSpan = stackalloc byte[nameSpanSize];
        Span<byte> symbolNameSpan = stackalloc byte[nameSpanSize];

        var stackTrace = new List<Frame>((int)frames);

        for (int f = 0; f < frames; f++)
        {
            var frame = new Frame();
            var pc = frame.InstructionAddress = stackFrames[f].InstructionOffset;

            uint moduleIndex;
            ulong moduleBase;
            try
            {
                symbols.GetModuleByOffset(pc, 0, out moduleIndex, out moduleBase);
            }
            catch (COMException)
            {
                stackTrace.Add(frame);
                continue;
            }

            frame.ModuleBaseAddress = moduleBase;

            string? loadedImageName;
            try
            {
                symbols.GetModuleNames(moduleIndex, moduleBase
                                      , imageNameSpan, nameSpanSize, out var imageNameSize
                                      , moduleNameSpan, nameSpanSize, out var moduleNameSize
                                      , loadedImageNameSpan, nameSpanSize, out var loadedImageNameSize
                                      );

                var imageName = imageNameSpan.GetString(imageNameSize);
                var moduleName = moduleNameSpan.GetString(moduleNameSize);
                loadedImageName = loadedImageNameSpan.GetString(loadedImageNameSize);
            }
            catch (COMException)
            {
                loadedImageName = null;
            }

            frame.ModuleName = String.IsNullOrWhiteSpace(loadedImageName) ? $"<unknown_{moduleBase}>" : loadedImageName;

            string symbolName;
            try
            {
                symbols.GetNameByOffset(pc, symbolNameSpan, nameSpanSize, out var symbolNameSize, out _);

                symbolName = symbolNameSpan.GetString(symbolNameSize);
                symbolName = symbolName.Contains('!') ? symbolName[(symbolName.IndexOf('!') + 1)..] : "<unknown>";
            }
            catch (COMException)
            {
                stackTrace.Add(frame);
                continue;
            }

            frame.SymbolName = symbolName;

            stackTrace.Add(frame);
        }

        return stackTrace;
    }

    private void Destroy()
    {
        if (IsDisposed)
        {
            return;
        }

        if (Client is not null)
        {
            Client.EndSession(0);
            Client = null!;
        }

        IsDisposed = true;
    }

    ~DumpAnalyzer()
    {
        Destroy();
    }

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }
}
