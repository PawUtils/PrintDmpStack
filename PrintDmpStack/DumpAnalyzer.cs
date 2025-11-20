using System.Runtime.InteropServices;
using System.Text;
using Interop.DbgEng;

namespace PrintDmpStack;

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

    const int E_Fail = unchecked((int)0x80004005);
    const int E_NoInterface = unchecked((int)0x80004002);

    public List<DumpStackFrame> GetExceptionStackTrace()
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

        var stackTrace = new List<DumpStackFrame>((int)frames);

        for (int f = 0; f < frames; f++)
        {
            var frame = new DumpStackFrame();
            var pc = frame.InstructionAddress = stackFrames[f].InstructionOffset;

            symbols.GetModuleByOffset(pc, 0, out var moduleIndex, out var moduleBase);

            frame.ModuleBaseAddress = moduleBase;

            string loadedImageName;
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
                loadedImageName = $"<unknown_{moduleBase}>";
            }

            frame.ModuleName = String.IsNullOrWhiteSpace(loadedImageName) ? $"<unknown_{moduleBase}>" : loadedImageName;

            try
            {
                symbols.GetNameByOffset(pc, symbolNameSpan, nameSpanSize, out var symbolNameSize, out _);

                var symbolName = symbolNameSpan.GetString(symbolNameSize);
                frame.SymbolName = symbolName.Contains('!') ? symbolName[(symbolName.IndexOf('!') + 1)..] : "<unknown>";
            }
            catch (COMException)
            {
                stackTrace.Add(frame);
                continue;
            }

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
