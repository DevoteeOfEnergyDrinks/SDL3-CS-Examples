/*
 * This example code reports power status (plugged in, battery level, etc).
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
        SetAppMetadata("Example Misc Power", "1.0", "com.example.misc-power");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/misc/power", 640, 480, WindowFlags.Resizable, out window, out renderer))
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
        FRect frame = new() { X = 100, Y = 200, W = 440, H = 80 };  // the percentage bar dimensions.

        // Query for battery info
        int seconds = 0;
        int percent = 0;
        PowerState state = GetPowerInfo(out seconds, out percent);

        // We set up different drawing details for each power state, then
        // run it all through the same drawing code.
        int clearR = 0, clearG = 0, clearB = 0; // clear window to this color.
        int textR = 255, textG = 255, textB = 255; // draw messages in this color.
        int frameR = 255, frameG = 255, frameB = 255; // draw a percentage bar frame in this color.
        int barR = 0, barG = 0, barB = 0;  // draw a percentage bar in this color.
        string message = "";
        string message2 = "";

        switch (state)
        {
            case PowerState.Error:
                message2 = "ERROR GETTING POWER STATE";
                message = GetError();
                clearR = 255;  // red background
                break;

            default:  // in case this does something unexpected later, treat it as unknown.
            case PowerState.Unknown:
                message = "Power state is unknown.";
                clearR = clearB = clearG = 50;  // grey background
                break;

            case PowerState.OnBattery:
                message = "Running on battery.";
                barR = 255;  // draw in red
                break;

            case PowerState.NoBattery:
                message = "Plugged in, no battery available.";
                clearG = 50;  // green background
                break;

            case PowerState.Charging:
                message = "Charging.";
                barB = barG = 255;  // draw in cyan 
                break;

            case PowerState.Charged:
                message = "Charged.";
                barG = 255;  // draw in green
                break;
        }

        SetRenderDrawColor(renderer, (byte)clearR, (byte)clearG, (byte)clearB, 255);
        RenderClear(renderer);

        if (percent >= 0)
        {
            float x, y;
            FRect percentRect;
            string remainingString;
            string messageBuffer;

            percentRect = frame;
            percentRect.W *= percent / 100.0f;

            if (seconds < 0)
            {
                remainingString = "unknown time";
            }
            else
            {
                int hours, minutes;
                hours = seconds / (60 * 60);
                seconds -= hours * (60 * 60);
                minutes = seconds / 60;
                seconds -= minutes * 60;
                remainingString = $"{hours:00}:{minutes:00}:{seconds:00}";
            }

            messageBuffer = $"Battery: {percent,3} percent, {remainingString} remaining";
            x = frame.X + ((frame.W - (DebugTextFontCharacterSize * messageBuffer.Length)) / 2.0f);
            y = frame.Y + frame.H + DebugTextFontCharacterSize;

            SetRenderDrawColor(renderer, (byte)barR, (byte)barG, (byte)barB, 255);  // draw percent bar.
            RenderFillRect(renderer, percentRect);
            SetRenderDrawColor(renderer, (byte)frameR, (byte)frameG, (byte)frameB, 255);  // draw frame on top of bar.
            RenderRect(renderer, frame);
            SetRenderDrawColor(renderer, (byte)textR, (byte)textG, (byte)textB, 255);
            RenderDebugText(renderer, x, y, messageBuffer);  // draw text about battery level
        }

        if (!string.IsNullOrEmpty(message))
        {
            float x = frame.X + ((frame.W - (DebugTextFontCharacterSize * message.Length)) / 2.0f);
            float y = frame.Y - (DebugTextFontCharacterSize * 2);
            SetRenderDrawColor(renderer, (byte)textR, (byte)textG, (byte)textB, 255);
            RenderDebugText(renderer, x, y, message);
        }

        if (!string.IsNullOrEmpty(message2))
        {
            float x = frame.X + ((frame.W - (DebugTextFontCharacterSize * message2.Length)) / 2.0f);
            float y = frame.Y - (DebugTextFontCharacterSize * 4);
            SetRenderDrawColor(renderer, (byte)textR, (byte)textG, (byte)textB, 255);
            RenderDebugText(renderer, x, y, message2);
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