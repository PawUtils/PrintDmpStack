using System.Runtime.CompilerServices;
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

        var hr = ((IDebugControl)root).WaitForEvent(0, 0);
        if (hr < 0)
        {
            throw new COMException("WaitForEvent failed.", hr);
        }

        var symbols = (IDebugSymbols)root;

        symbols.SetImagePath(imagePaths);
        symbols.SetSymbolPath(symbolPaths);

        return new DumpAnalyzer(root);
    }

    public List<Frame> GetExceptionStackTrace()
    {
        var symbols = (IDebugSymbols)Client;
        var control = (IDebugControl4)Client;

        var (contextSize, extraInfoSize) = (0U, 0U);

        control.GetStoredEventInformation(out _, out _, out _
                                         , null, 0, ref contextSize
                                         , null, 0, ref extraInfoSize
                                         );

        var context = new byte[contextSize];
        var extraInfo = new byte[extraInfoSize];
        control.GetStoredEventInformation(out _, out _, out _
                                         , context, contextSize, ref Unsafe.NullRef<uint>()
                                         , extraInfo, extraInfoSize, ref Unsafe.NullRef<uint>()
                                         );

        uint frames = 0, maxFrames = 150;
        byte[] frameContexts = new byte[maxFrames * contextSize];
        DebugStackFrame[] stackFrames = new DebugStackFrame[maxFrames];
        control.GetContextStackTrace(context, contextSize
                                    , stackFrames, maxFrames
                                    , frameContexts, (uint)frameContexts.Length, contextSize
                                    , ref frames);

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

            var moduleIndex = 0U;
            var moduleBase = 0UL;
            try
            {
                symbols.GetModuleByOffset(pc, 0, ref moduleIndex, ref moduleBase);
            }
            catch (Exception ex)
            {
                frame.InteropErrorMessage = $"GetModuleByOffset: {ex}\n\n";
                stackTrace.Add(frame);
                continue;
            }

            frame.ModuleBaseAddress = moduleBase;

            string? loadedImageName;
            try
            {
                var (imageNameSize, moduleNameSize, loadedImageNameSize) = (0U, 0U, 0U);

                symbols.GetModuleNames(moduleIndex, moduleBase
                                      , imageNameSpan, nameSpanSize, ref imageNameSize
                                      , moduleNameSpan, nameSpanSize, ref moduleNameSize
                                      , loadedImageNameSpan, nameSpanSize, ref loadedImageNameSize
                                      );

                var imageName = imageNameSpan.GetString(imageNameSize);
                var moduleName = moduleNameSpan.GetString(moduleNameSize);
                loadedImageName = loadedImageNameSpan.GetString(loadedImageNameSize);
            }
            catch (Exception ex)
            {
                frame.InteropErrorMessage += $"GetModuleNames: {ex}\n\n";
                loadedImageName = null;
            }

            frame.ModuleName = String.IsNullOrWhiteSpace(loadedImageName) ? $"<unknown_{moduleBase}>" : loadedImageName;

            string symbolName;
            try
            {
                var symbolNameSize = 0U;

                symbols.GetNameByOffset(pc, symbolNameSpan, nameSpanSize, ref symbolNameSize, ref Unsafe.NullRef<ulong>());

                symbolName = symbolNameSpan.GetString(symbolNameSize);
                symbolName = symbolName.Contains('!') ? symbolName[(symbolName.IndexOf('!') + 1)..] : "<unknown>";
            }
            catch (Exception ex)
            {
                frame.InteropErrorMessage += $"GetNameByOffset: {ex}\n\n";
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
