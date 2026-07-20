/*
 * This example rumbles gamepads on button presses.
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

    public class GamepadInfo
    {
        public uint GamepadID;
        public string? Action;
    }

    public static GamepadInfo[] gamepadsInfo = new GamepadInfo[16];


    public static GamepadInfo? FindGamepadInfo(uint which)
    {
        int i;
        for (i = 0; i < gamepadsInfo.Length; i++)
        {
            if (gamepadsInfo[i].GamepadID == which)
            {
                return gamepadsInfo[i];
            }
        }
        return null;
    }


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
        SetAppMetadata("Example Input Gamepad Rumble", "1.0", "com.example.input-gamepad-rumble");

        if (!Init(InitFlags.Video | InitFlags.Gamepad))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/clear", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        // Initialize each slot with a unique new object
        for (int i = 0; i < gamepadsInfo.Length; i++)
        {
            gamepadsInfo[i] = new GamepadInfo { GamepadID = 0, Action = "idle" };
        }

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        GamepadInfo? gamepadInfo;
        if (evt.Type == (uint)EventType.Quit)
        {
            return AppResult.Success;   // end the program, reporting success to the OS.
        }
        else if (evt.Type == (uint)EventType.GamepadAdded)
        {   // this event is sent for each hotplugged stick, but also each already-connected gamepad during SDL_Init().
            OpenGamepad(evt.GDevice.Which);
            gamepadInfo = FindGamepadInfo(0);  // find an empty space
            if (gamepadInfo != null)
            {
                gamepadInfo.GamepadID = evt.GDevice.Which;
                gamepadInfo.Action = "idle";
            }
        }
        else if (evt.Type == (uint)EventType.GamepadRemoved)
        {
            IntPtr gamepadPtr = GetGamepadFromID(evt.GDevice.Which);
            CloseGamepad(gamepadPtr);
            gamepadInfo = FindGamepadInfo(evt.GDevice.Which);
            if (gamepadInfo != null)
            {
                gamepadInfo.GamepadID = 0;
            }
        }
        else if (evt.Type == (uint)EventType.GamepadButtonDown)
        {
            IntPtr gamepadPtr = GetGamepadFromID(evt.GButton.Which);
            gamepadInfo = FindGamepadInfo(evt.GButton.Which);
            switch (evt.GButton.Button)
            {
                case (byte)GamepadButton.South:
                    RumbleGamepad(gamepadPtr, 0xFFFF, 0x0000, 5000);
                    if (gamepadInfo != null)
                    {
                        gamepadInfo.Action = "rumble high frequency";
                    }
                    break;
                case (byte)GamepadButton.East:
                    RumbleGamepad(gamepadPtr, 0x0000, 0xFFFF, 5000);
                    if (gamepadInfo != null)
                    {
                        gamepadInfo.Action = "rumble low frequency";
                    }
                    break;
                default:
                    break;
            }
        }
        else if (evt.Type == (uint)EventType.GamepadButtonUp)
        {
            IntPtr gamepadPtr = GetGamepadFromID(evt.GButton.Which);
            RumbleGamepad(gamepadPtr, 0x0000, 0x0000, 0);
            gamepadInfo = FindGamepadInfo(evt.GButton.Which);
            if (gamepadInfo != null)
            {
                gamepadInfo.Action = "idle";
            }
        }
        return AppResult.Continue;  // carry on with the program!
    }


    static void DrawCenteredText(int renderWidth, ref int y, string text)
    {
        int x = (renderWidth - (text.Length * DebugTextFontCharacterSize)) / 2;
        if (!string.IsNullOrEmpty(text))
        {
            RenderDebugText(renderer, x, y, text);
        }
        y += DebugTextFontCharacterSize * 2;
    }


    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        int renderWidth;
        int renderHeight;
        int y;
        int i;
        GetCurrentRenderOutputSize(renderer, out renderWidth, out renderHeight);
        SetRenderDrawColor(renderer, 0, 0, 0, 255);  // clear to black
        RenderClear(renderer);

        y = DebugTextFontCharacterSize * 8;
        SetRenderDrawColor(renderer, 255, 255, 0, 255);  // yellow text
        DrawCenteredText(renderWidth, ref y, "Connect gamepads and press buttons to rumble.");
        y += DebugTextFontCharacterSize * 3;

        // report all the visible joysticks and what they are doing at the moment.
        SetRenderDrawColor(renderer, 255, 255, 255, 255);  // white text
        for (i = 0; i < gamepadsInfo.Length; i++)
        {
            uint which = gamepadsInfo[i].GamepadID;
            if (which == 0)
            {
                DrawCenteredText(renderWidth, ref y, "");  // just leave a blank line.
            }
            else
            {
                string text = $"{GetGamepadNameForID(which)}: {gamepadsInfo[i].Action}";
                DrawCenteredText(renderWidth, ref y, text);
            }
        }

        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        Quit();
        // SDL will clean up the window/renderer for us.
    }
}