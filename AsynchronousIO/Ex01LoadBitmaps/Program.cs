/*
 * This example code loads a bitmap with asynchronous i/o and renders it.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */
using System.Runtime.InteropServices;

internal class Program
{
    #region Main
    // These delegates map our C# methods to the internal SDL3 lifecycle events.
    private static readonly AppInitFunc _init = new(AppInit);
    private static readonly AppIterateFunc _iterate = new(AppIterate);
    private static readonly AppEventFunc _event = new(AppEvent);
    private static readonly AppQuitFunc _quit = new(AppQuit);


    private static void Main(string[] args)
    {
        // SDL3 expects C-style command line arguments (where argv[0] is the executable name).
        // Environment.GetCommandLineArgs() in .NET includes the executable name at index 0,
        // which matches what SDL3 expects.
        string[] arguments = Environment.GetCommandLineArgs();

        // RunApp starts the SDL engine and tells it to call our defined callbacks.
        RunApp(arguments.Length, arguments, MyRunAppCallback, IntPtr.Zero);
    }


    // This acts as the entry point for the SDL3 Callback System.
    // For more information about the Callback System being used by none C/C++ languages
    // check this wiki entry: https://wiki.libsdl.org/SDL3/NonstandardStartup
    static int MyRunAppCallback(int argc, string[]? argv)
    {
        return EnterAppMainCallbacks(argc, argv, _init, _iterate, _event, _quit);
    }
    #endregion


    #region Fields
    // We use IntPtr (Integer Pointers) because SDL3 is a C library.
    // These variables hold the memory addresses of the window and the renderer.
    public static IntPtr window = IntPtr.Zero;
    public static IntPtr renderer = IntPtr.Zero;
    public static IntPtr queue = IntPtr.Zero;

    public const int TotalTexture = 4;
    public static string[] pngs = ["sample.png", "gamepad_front.png", "speaker.png", "icon2x.png"];
    public static IntPtr[] textures = new IntPtr[TotalTexture];
    public static FRect[] textureRects =
    [
        new(){X = 116, Y = 156, W = 408, H = 167},
        new(){X = 20, Y = 200, W = 96, H = 60},
        new(){X = 525, Y = 180, W = 96, H = 96},
        new(){X = 288, Y = 375, W = 64, H = 64},
    ];
    #endregion


    #region AppInit
    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        int i;

        if (!Init(InitFlags.Video))
        {
            ShowSimpleMessageBox(MessageBoxFlags.Error, "Couldn't initialize SDL!", GetError(), IntPtr.Zero);
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/asyncio/load-bitmaps", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            ShowSimpleMessageBox(MessageBoxFlags.Error, "Couldn't create window/renderer!", GetError(), IntPtr.Zero);
            return AppResult.Failure;
        }
        SetRenderLogicalPresentation(renderer, 640, 480, RendererLogicalPresentation.Letterbox);

        queue = CreateAsyncIOQueue();
        if (queue == IntPtr.Zero)
        {
            ShowSimpleMessageBox(MessageBoxFlags.Error, "Couldn't create async i/o queue!", GetError(), IntPtr.Zero);
            return AppResult.Failure;
        }

        // Load some .png files asynchronously from wherever the app is being run from, put them in the same queue.
        for (i = 0; i < pngs.Length; i++)
        {
            string path = GetBasePath() + "Assets/" + pngs[i];  // build a string of the full file path

            GCHandle handle = GCHandle.Alloc(pngs[i]);
            IntPtr userdata = GCHandle.ToIntPtr(handle);

            // you should check for failure, but we'll just go on without files here.
            LoadFileAsync(path, queue, userdata);  // attach the filename as app-specific data, so we can see it later.
        }
        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppEvent
    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        if (evt.Type == (uint)EventType.Quit)
        {
            return AppResult.Success;   // end the program, reporting success to the OS.
        }
        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppIterate
    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        AsyncIOOutcome outcome;
        int i;

        if (GetAsyncIOResult(queue, out outcome))
        {   // a .png file load has finished?
            if (outcome.Result == AsyncIOResult.Complete)
            {
                GCHandle handle = GCHandle.FromIntPtr(outcome.Userdata);
                string userdata = (string)handle.Target!;

                // this might be _any_ of the pngs; they might finish loading in any order.
                for (i = 0; i < pngs.Length; i++)
                {
                    // Original C example: this doesn't need a strcmp because we gave the pointer from this array to SDL_LoadFileAsync
                    // C# Example: here were are using a string comparison for it it easier to do in C# than comparing pointer
                    if (userdata == pngs[i])
                    {
                        break;
                    }
                }

                handle.Free();

                if (i < pngs.Length)
                {   // (just in case.)
                    IntPtr surfacePtr = LoadPNGIO(IOFromConstMem(outcome.Buffer, (UIntPtr)outcome.BytesTransferred), true);
                    if (surfacePtr != IntPtr.Zero)
                    {   // the renderer is not multithreaded, so create the texture here once the data loads.
                        textures[i] = CreateTextureFromSurface(renderer, surfacePtr);
                        if (textures[i] == IntPtr.Zero)
                        {
                            ShowSimpleMessageBox(MessageBoxFlags.Error, "Couldn't create texture!", GetError(), IntPtr.Zero);
                            return AppResult.Failure;
                        }
                        DestroySurface(surfacePtr);
                    }
                }
            }
            Free(outcome.Buffer);
        }

        SetRenderDrawColor(renderer, 0, 0, 0, 255);
        RenderClear(renderer);

        for (i = 0; i < textures.Length; i++)
        {
            RenderTexture(renderer, textures[i], IntPtr.Zero, in textureRects[i]);
        }

        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppQuit
    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        int i;

        DestroyAsyncIOQueue(queue);

        for (i = 0; i < textures.Length; i++)
        {
            DestroyTexture(textures[i]);
        }
        // SDL will clean up the window/renderer for us.
    }
    #endregion
}