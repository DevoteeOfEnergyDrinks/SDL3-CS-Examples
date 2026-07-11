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
        SetAppMetadata("Example Renderer Rectangles", "1.0", "com.example.renderer-rectangles");

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
        FRect[] rects = new FRect[16];
        ulong now = GetTicks();
        int i;

        // we'll have the rectangles grow and shrink over a few seconds. */
        float direction = ((now % 2000) >= 1000) ? 1.0f : -1.0f;
        float scale = ((float)(((int)(now % 1000)) - 500) / 500.0f) * direction;

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);  // black, full alpha
        RenderClear(renderer);  // start with a blank canvas.

        // Rectangles are comprised of set of X and Y coordinates, plus width and height. 
        // (0, 0) is the top left of the window, and larger numbers go down and to the right. 
        // This isn't how geometry works, but this is pretty standard in 2D graphics.

        // Let's draw a single rectangle (square, really).
        rects[0].X = rects[0].Y = 100;
        rects[0].W = rects[0].H = 100 + (100 * scale);
        SetRenderDrawColor(renderer, 255, 0, 0, byte.MaxValue);  // red, full alpha
        RenderRect(renderer, in rects[0]);

        // Now let's draw several rectangles with one function call.
        for (i = 0; i < 3; i++)
        {
            float size = (i + 1) * 50.0f;
            rects[i].W = rects[i].H = size + (size * scale);
            rects[i].X = (WindowWidth - rects[i].W) / 2;    // center it.
            rects[i].Y = (WindowHeight - rects[i].H) / 2;   // center it.
        }
        SetRenderDrawColor(renderer, 0, 255, 0, byte.MaxValue);  // green, full alpha
        RenderRects(renderer, rects, 3);  // draw three rectangles at once

        // those were rectangle _outlines_, really. You can also draw _filled_ rectangles!
        rects[0].X = 400;
        rects[0].Y = 50;
        rects[0].W = 100 + (100 * scale);
        rects[0].H = 50 + (50 * scale);
        SetRenderDrawColor(renderer, 0, 0, 255, byte.MaxValue);  // blue, full alpha
        RenderFillRect(renderer, in rects[0]);

        // ...and also fill a bunch of rectangles at once...
        for (i = 0; i < rects.Length; i++)
        {
            float w = ((float)WindowWidth / rects.Length);
            float h = i * 8.0f;
            rects[i].X = i * w;
            rects[i].Y = WindowHeight - h;
            rects[i].W = w;
            rects[i].H = h;
        }
        SetRenderDrawColor(renderer, 255, 255, 255, byte.MaxValue);  // white, full alpha
        RenderFillRects(renderer, rects, rects.Length);

        RenderPresent(renderer);  // put it all on the screen!

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
}