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
    public static ulong lastTime = 0;

    const int WindowWidth = 640;
    const int WindowHeight = 480;

    const int NumberOfPoints = 500;
    const int MinPixelsPerSecond = 30;  // move at least this many pixels per second.
    const int MaxPixelsPerSecond = 60;  //move this many pixels per second at most. 


    // (track everything as parallel arrays instead of a array of structs,
    // so we can pass the coordinates to the renderer in a single function call.)

    // Points are plotted as a set of X and Y coordinates.
    // (0, 0) is the top left of the window, and larger numbers go down and to the right. 
    // This isn't how geometry works, but this is pretty standard in 2D graphics.
    static readonly FPoint[] points = new FPoint[NumberOfPoints];
    static readonly float[] pointSpeeds = new float[NumberOfPoints];


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

        SetAppMetadata("Example Renderer Points", "1.0", "com.example.renderer-points");

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

        SetRenderLogicalPresentation(renderer, WindowWidth, WindowHeight, RendererLogicalPresentation.Letterbox);

        for (i = 0; i < points.Length; i++)
        {
            points[i].X = RandF() * WindowWidth;
            points[i].Y = RandF() * WindowHeight;
            pointSpeeds[i] = MinPixelsPerSecond + (RandF() * (MaxPixelsPerSecond - MinPixelsPerSecond));
        }

        lastTime = GetTicks();

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
        ulong now = GetTicks();
        float elapsed = ((float)(now - lastTime)) / 1000.0f;  // seconds since last iteration
        int i;

        // let's move all our points a little for a new frame.
        for (i = 0; i < points.Length; i++)
        {
            float distance = elapsed * pointSpeeds[i];
            points[i].X += distance;
            points[i].Y += distance;
            if ((points[i].X >= WindowWidth) || (points[i].Y >= WindowHeight))
            {
                // off the screen; restart it elsewhere!
                if (Rand(2) == 0)
                {
                    points[i].X = RandF() * ((float)WindowWidth);
                    points[i].Y = 0.0f;
                }
                else
                {
                    points[i].X = 0.0f;
                    points[i].Y = RandF() * ((float)WindowHeight);
                }
                pointSpeeds[i] = MinPixelsPerSecond + (RandF() * (MaxPixelsPerSecond - MinPixelsPerSecond));
            }
        }

        lastTime = now;

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);           // black, full alpha
        RenderClear(renderer);                                          // start with a blank canvas.
        SetRenderDrawColor(renderer, 255, 255, 255, byte.MaxValue);     // white, full alpha
        RenderPoints(renderer, points, points.Length);                  // draw all the points!

        // You can also draw single points with SDL_RenderPoint(), 
        // but it's cheaper (sometimes significantly so) to do them all at once.

        RenderPresent(renderer);  // put it all on the screen!

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
}