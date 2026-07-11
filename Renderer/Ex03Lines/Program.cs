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
        SetAppMetadata("Example Renderer Lines", "1.0", "com.example.renderer-lines");

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
        int i;

        // Lines (line segments, really) are drawn in terms of points:
        // a set of X and Y coordinates, one set for each end of the line.
        // (0, 0) is the top left of the window, and larger numbers go down and to the right.
        // This isn't how geometry works, but this is pretty standard in 2D graphics.
        FPoint[] linePoints =
        [
          new FPoint { X = 100f, Y = 354f }, new FPoint { X = 220f, Y = 230f }, new FPoint { X = 140f, Y = 230f },
          new FPoint { X = 320f, Y = 100f }, new FPoint { X = 500f, Y = 230f }, new FPoint { X = 420f, Y = 230f },
          new FPoint { X = 540f, Y = 354f }, new FPoint { X = 400f, Y = 354f }, new FPoint { X = 100f, Y = 354f }
        ];

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 100, 100, 100, 255);  // grey, full alpha
        RenderClear(renderer);  // start with a blank canvas.

        // You can draw lines, one at a time, like these brown ones...
        SetRenderDrawColor(renderer, 127, 49, 32, 255);
        RenderLine(renderer, 240, 450, 400, 450);
        RenderLine(renderer, 240, 356, 400, 356);
        RenderLine(renderer, 240, 356, 240, 450);
        RenderLine(renderer, 400, 356, 400, 450);

        // You can also draw a series of connected lines in a single batch...
        SetRenderDrawColor(renderer, 0, 255, 0, 255);
        RenderLines(renderer, linePoints, linePoints.Length);

        // here's a bunch of lines drawn out from a center point in a circle.
        // we randomize the color of each line, so it functions as animation.
        for (i = 0; i < 360; i++)
        {
            float size = 30.0f;
            float x = 320.0f;
            float y = 95.0f - (size / 2.0f);
            float r = (float)i * (MathF.PI / 180.0f);
            SetRenderDrawColor(renderer, (byte)Rand(256), (byte)Rand(256), (byte)Rand(256), byte.MaxValue);
            RenderLine(renderer, x, y, x + MathF.Cos(r) * size, y + MathF.Sin(r) * size);
        }

        RenderPresent(renderer);  //put it all on the screen!

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
}