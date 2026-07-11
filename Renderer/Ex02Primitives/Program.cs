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
    public static FPoint[] points = new FPoint[500];


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
        int i;

        SetAppMetadata("Example Renderer Primitives", "1.0", "com.example.renderer-primitives");

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

        // set up some random points
        for (i = 0; i < points.Length; i++)
        {
            points[i].X = (RandF() * 440.0f) + 100.0f;
            points[i].Y = (RandF() * 280.0f) + 100.0f;
        }

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
        FRect rect;

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 33, 33, 33, 255);  // dark grey, full alpha
        RenderClear(renderer); // start with a blank canvas.

        // draw a filled rectangle in the middle of the canvas.
        SetRenderDrawColor(renderer, 0, 0, 255, 255); // blue, full alpha
        rect.X = 100;
        rect.Y = 100;
        rect.W = 440;
        rect.H = 280;
        RenderFillRect(renderer, in rect);

        // draw some points across the canvas.
        SetRenderDrawColor(renderer, 255, 0, 0, 255); // red, full alpha
        RenderPoints(renderer, points, points.Length);

        // draw a unfilled rectangle in-set a little bit.
        SetRenderDrawColor(renderer, 0, 255, 0, 255); // green, full alpha
        rect.X += 30;
        rect.Y += 30;
        rect.W -= 60;
        rect.H -= 60;
        RenderRect(renderer, in rect);

        // draw two lines in an X across the wole canvas.
        SetRenderDrawColor(renderer, 255, 255, 0, 255); // yellow, full alpha
        RenderLine(renderer, 0, 0, 640, 480);
        RenderLine(renderer, 0, 480, 640, 0);

        RenderPresent(renderer); // put it all on the screen!

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
}