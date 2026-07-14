/*
 * This example creates an SDL window and renderer, and then draws some text
 * using SDL_RenderDebugText() every frame.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */
internal class Program
{
    // These delegates map our C# methods to the internal SDL3 lifecycle events.
    private static readonly AppInitFunc _init = new(AppInit);
    private static readonly AppIterateFunc _iterate = new(AppIterate);
    private static readonly AppEventFunc _event = new(AppEvent);
    private static readonly AppQuitFunc _quit = new(AppQuit);


    // We use IntPtr (Integer Pointers) because SDL3 is a C library.
    // These variables hold the memory addresses of the window and the renderer.
    public static IntPtr window = IntPtr.Zero;
    public static IntPtr renderer = IntPtr.Zero;

    const int WindowWidth = 640;
    const int WindowHeight = 480;


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


    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        SetAppMetadata("Example Renderer Debug Text", "1.0", "com.example.renderer-debug-text");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/debug-text", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        SetRenderLogicalPresentation(renderer, 640, 480, RendererLogicalPresentation.Letterbox);

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        if (evt.Type == (uint)EventType.Quit)
        {
            return AppResult.Success;   // end the program, reporting success to the OS.
        }
        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        int charsize = DebugTextFontCharacterSize;

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);  // black, full alpha 
        RenderClear(renderer);  // start with a blank canvas.

        SetRenderDrawColor(renderer, 255, 255, 255, byte.MaxValue);  // white, full alpha
        RenderDebugText(renderer, 272, 100, "Hello world!");
        RenderDebugText(renderer, 224, 150, "This is some debug text.");

        SetRenderDrawColor(renderer, 51, 102, 255, byte.MaxValue);  // light blue, full alpha
        RenderDebugText(renderer, 184, 200, "You can do it in different colors.");
        SetRenderDrawColor(renderer, 255, 255, 255, byte.MaxValue);  // white, full alpha

        SetRenderScale(renderer, 4.0f, 4.0f);
        RenderDebugText(renderer, 14, 65, "It can be scaled.");
        SetRenderScale(renderer, 1.0f, 1.0f);
        RenderDebugText(renderer, 64, 350, "This only does ASCII chars. So this laughing emoji won't draw: 🤣");

        RenderDebugTextFormat
        (
            renderer,
            ((float)(WindowWidth - (charsize * 46)) / 2),
            400,
            $"(This program has been running for {GetTicks() / 1000} seconds.)"
        );

        RenderPresent(renderer);  //put it all on the screen!

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
}