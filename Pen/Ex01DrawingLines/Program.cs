/*
 * This example code reads pen/stylus input and draws lines. Darker lines
 * for harder pressure.
 *
 * SDL can track multiple pens, but for simplicity here, this assumes any
 * pen input we see was from one device.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */
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
    public static IntPtr renderTarget = IntPtr.Zero;
    public static float pressure = 0.0f;
    public static float previousTouchX = -1.0f;
    public static float previousTouchY = -1.0f;
    public static float tiltX = 0.0f;
    public static float tiltY = 0.0f;
    #endregion


    #region AppInit
    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        int width;
        int height;

        SetAppMetadata("Example Pen Drawing Lines", "1.0", "com.example.pen-drawing-lines");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/pen/drawing-lines", 640, 480, 0, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        // we make a render target so we can draw lines to it and not have to record and redraw every pen stroke each frame.
        // Instead rendering a frame for us is a single texture draw.

        // make sure the render target matches output size (for hidpi displays, etc) so drawing matches the pen's position on a tablet display.
        GetRenderOutputSize(renderer, out width, out height);
        renderTarget = CreateTexture(renderer, PixelFormat.RGBA8888, TextureAccess.Target, width, height);
        if (renderTarget == IntPtr.Zero)
        {
            Log($"Couldn't create render target: {GetError()}");
            return AppResult.Failure;
        }

        // just blank the render target to gray to start.
        SetRenderTarget(renderer, renderTarget);
        SetRenderDrawColor(renderer, 100, 100, 100, byte.MaxValue);
        RenderClear(renderer);
        SetRenderTarget(renderer, IntPtr.Zero);
        SetRenderDrawBlendMode(renderer, BlendMode.Blend);

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
        // There are several events that track the specific stages of pen activity,
        // but we're only going to look for motion and pressure, for simplicity.
        if (evt.Type == (uint)EventType.PenMotion)
        {
            // you can check for when the pen is touching, but if pressure > 0.0f, it's definitely touching!
            if (pressure > 0.0f)
            {
                if (previousTouchX >= 0.0f)
                {   // only draw if we're moving while touching
                    // draw with the alpha set to the pressure, so you effectively get a fainter line for lighter presses.
                    SetRenderTarget(renderer, renderTarget);
                    SetRenderDrawColorFloat(renderer, 0, 0, 0, pressure);
                    RenderLine(renderer, previousTouchX, previousTouchY, evt.PMotion.X, evt.PMotion.Y);
                }
                previousTouchX = evt.PMotion.X;
                previousTouchY = evt.PMotion.Y;
            }
            else
            {
                previousTouchX = previousTouchY = -1.0f;
            }
        }
        else if (evt.Type == (uint)EventType.PenAxis)
        {
            if (evt.PAxis.Axis == PenAxis.Pressure)
            {
                pressure = evt.PAxis.Value;  // remember new pressure for later draws.
            }
            else if (evt.PAxis.Axis == PenAxis.XTilt)
            {
                tiltX = evt.PAxis.Value;
            }
            else if (evt.PAxis.Axis == PenAxis.YTilt)
            {
                tiltY = evt.PAxis.Value;
            }
        }
        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppIterate
    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        string debugText;
        
        // make sure we're drawing to the window and not the render target */
        SetRenderTarget(renderer, IntPtr.Zero);
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);
        RenderClear(renderer);  // just in case.
        RenderTexture(renderer, renderTarget, IntPtr.Zero, IntPtr.Zero);
        debugText = $"Tilt: {tiltX} {tiltY}";
        RenderDebugText(renderer, 0, 8, debugText);
        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppQuit
    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        DestroyTexture(renderTarget);
        // SDL will clean up the window/renderer for us.
    }
    #endregion
}