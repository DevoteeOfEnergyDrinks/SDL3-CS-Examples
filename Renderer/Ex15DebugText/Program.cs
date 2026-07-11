/*
* This example code creates an SDL window and renderer, and then clears the
* window to a different color every frame, so you'll effectively get a window
* that's smoothly fading between colors.
*
* This code is public domain. Feel free to use it for any purpose!
* This code is a port of the official SDL3 examples
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
        SetAppMetadata("Example Renderer Clear", "1.0", "com.example.renderer-clear");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }
        
        if (!CreateWindowAndRenderer("examples/renderer/clear", 640, 480, WindowFlags.Resizable, out window, out renderer))
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
        double now = GetTicks() / 1000.0f;  // convert from milliseconds to seconds.

        // choose the color for the frame we will draw. The sine wave trick makes it fade between colors smoothly.
        float red = (float)(0.5f + 0.5f * Math.Sin(now));
        float green = (float)(0.5f + 0.5f * Math.Sin(now + Math.PI * 2 / 3));
        float blue = (float)(0.5f + 0.5f * Math.Sin(now + Math.PI * 4 / 3));

        SetRenderDrawColorFloat(renderer, red, green, blue, 1.0f);  // new color, full alpha

        // clear the window to the draw color.
        RenderClear(renderer);

        // put the newly-cleared rendering on the screen.
        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
}