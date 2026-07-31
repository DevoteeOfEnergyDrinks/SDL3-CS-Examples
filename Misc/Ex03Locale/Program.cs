/*
 * This example code reports the currently selected locales.
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
    #endregion


    #region AppInit
    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        SetAppMetadata("Example Misc Locale", "1.0", "com.example.misc-locale");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/misc/locale", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }
        SetRenderLogicalPresentation(renderer, 640, 480, RendererLogicalPresentation.Letterbox);

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
        FRect frame = new() { X = 0, Y = 0, W = 640, H = 480 };
        Locale[]? locales;
        string message = "";
        int count, i;
        float x, y;

        SetRenderDrawColor(renderer, 0, 0, 0, 255);
        RenderClear(renderer);

        locales = GetPreferredLocales(out count);
        if (locales == null)
        {
            x = frame.X + ((frame.W - (DebugTextFontCharacterSize * message.Length)) / 2.0f);
            y = frame.Y;
            SetRenderDrawColor(renderer, 255, 255, 255, 255);
            RenderDebugText(renderer, x, y, message);
        }
        else
        {
            message = $"Locales, in order of preference ({count} total:)";

            x = frame.X + ((frame.W - (DebugTextFontCharacterSize * message.Length)) / 2.0f);
            y = frame.Y;
            SetRenderDrawColor(renderer, 255, 255, 255, 255);
            RenderDebugText(renderer, x, y, message);

            for (i = 0; i < count; ++i)
            {
                Locale locale = locales![i];
                string? country = locale.Country;

                message = $" - {locale.Language}{(string.IsNullOrEmpty(country) ? "_" : "")}{country}";
  
                x = frame.X + ((frame.W - (DebugTextFontCharacterSize * message.Length)) / 2.0f);
                y = frame.Y + ((DebugTextFontCharacterSize * 2) * (i + 1));
                SetRenderDrawColor(renderer, 255, 255, 255, 255);
                RenderDebugText(renderer, x, y, message);
            }
        }

        // put the new rendering on the screen.
        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppQuit
    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
    #endregion
}