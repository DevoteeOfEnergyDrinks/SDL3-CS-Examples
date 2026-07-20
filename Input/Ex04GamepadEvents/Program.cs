/*
 * This example code looks for joystick input in the event handler, and
 * reports any changes as a flood of info.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */

// Joysticks are low-level interfaces: there's something with a bunch of
// buttons, axes and hats, in no understood order or position. This is
// a flexible interface, but you'll need to build some sort of configuration
// UI to let people tell you what button, etc, does what. On top of this
// interface, SDL offers the "gamepad" API, which works with lots of devices,
// and knows how to map arbitrary buttons and such to look like an
// Xbox/PlayStation/etc gamepad. This is easier, and better, for many games,
// but isn't necessarily a good fit for complex apps and hardware. A flight
// simulator, a realistic racing game, etc, might want the joystick interface
// instead of gamepads.
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
    public static Color[] colors = new Color[64];

    const int MotionEventCooldown = 40;

    public class EventMessage()
    {
        public string Text = "";
        public Color Color;
        public ulong StartTicks;
    }

    public static LinkedList<EventMessage> messages = [];


    public static string BatteryStateString(PowerState state)
    {
        switch (state)
        {
            case PowerState.Error:
                return "ERROR";
            case PowerState.Unknown:
                return "UNKNOWN";
            case PowerState.OnBattery:
                return "ON BATTERY";
            case PowerState.NoBattery:
                return "NO BATTERY";
            case PowerState.Charging:
                return "CHARGING";
            case PowerState.Charged:
                return "CHARGED";
            default:
                break;
        }
        return "UNKNOWN";
    }


    public static void AddMessage(uint joystickID, string message)
    {
        messages.AddLast
        (
            new EventMessage
            {
                Text = message,
                Color = colors[joystickID % colors.Length],
                StartTicks = GetTicks()
            }
        );
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
        int i;

        SetAppMetadata("Example Input Gamepad Events", "1.0", "com.example.input-gamepad-events");

        if (!Init(InitFlags.Video | InitFlags.Gamepad))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/input/gamepad-events", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        colors[0].R = colors[0].G = colors[0].B = colors[0].A = byte.MaxValue;
        for (i = 1; i < colors.Length; i++)
        {
            colors[i].R = (byte)Rand(byte.MaxValue);
            colors[i].G = (byte)Rand(byte.MaxValue);
            colors[i].B = (byte)Rand(byte.MaxValue);
            colors[i].A = byte.MaxValue;
        }

        AddMessage(0, "Please plug in a joystick");

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        if (evt.Type == (uint)EventType.Quit)
        {
            return AppResult.Success;   // end the program, reporting success to the OS.
        }
        else if (evt.Type == (uint)EventType.GamepadAdded)
        {   // this event is sent for each hotplugged stick, but also each already-connected gamepad during SDL_Init().
            uint which = evt.GDevice.Which;
            IntPtr gamepadPtr = OpenGamepad(which);
            if (gamepadPtr == IntPtr.Zero)
            {
                AddMessage(which, $"Gamepad #{which} add, but not opened: {GetError()}");
            }
            else
            {
                string? mapping = GetGamepadMapping(gamepadPtr);
                AddMessage(which, $"Gamepad #{which} ('{GetGamepadName(gamepadPtr)}') added");
                if (string.IsNullOrEmpty(mapping))
                {
                    AddMessage(which, $"Gamepad #{which} mapping: {mapping}");
                }
            }
        }
        else if (evt.Type == (uint)EventType.GamepadRemoved)
        {
            uint which = evt.GDevice.Which;
            IntPtr gamepadPtr = GetGamepadFromID(which);
            if (gamepadPtr == IntPtr.Zero)
            {
                CloseGamepad(gamepadPtr);  // the gamepad was unplugged.
            }
            AddMessage(which, $"Gamepad #{which} removed");
        }
        else if (evt.Type == (uint)EventType.GamepadAxisMotion)
        {
            ulong axisMotionCooldownTime = 0;  // these are spammy, only show every X milliseconds.
            ulong now = GetTicks();
            if (now >= axisMotionCooldownTime)
            {
                uint which = evt.GAxis.Which;
                axisMotionCooldownTime = now + MotionEventCooldown;
                if (-1000 < evt.GAxis.Value && evt.GAxis.Value > 1000)
                {
                    AddMessage(which, $"Gamepad #{which} axis {GetGamepadStringForAxis((GamepadAxis)evt.GAxis.Axis)} -> {evt.GAxis.Value}");
                }
            }
        }
        else if ((evt.Type == (uint)EventType.GamepadButtonUp) || (evt.Type == (uint)EventType.GamepadButtonDown))
        {
            uint which = evt.GButton.Which;
            AddMessage(which, $"Gamepad #{which} button {GetGamepadStringForButton((GamepadButton)evt.GButton.Button)} -> {(evt.GButton.Down ? "PRESSED" : "RELEASED")}");
        }
        else if (evt.Type == (uint)EventType.JoystickBatteryUpdated)
        {
            uint which = evt.JBattery.Which;
            if (IsGamepad(which))
            {  // this is only reported for joysticks, so make sure this joystick is _actually_ a gamepad.
                AddMessage(which, $"Gamepad #{which} battery -> {BatteryStateString(evt.JBattery.State)} - {evt.JBattery.Percent}%");
            }
        }
        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        ulong now = GetTicks();
        float messageLifetime = 3500.0f;  // milliseconds a message lives for.
        float previousY = 0.0f;
        int windowWidth = 640, windowHeight = 480;

        SetRenderDrawColor(renderer, 0, 0, 0, 255);
        RenderClear(renderer);
        GetWindowSize(window, out windowWidth, out windowHeight);

        LinkedListNode<EventMessage>? node = messages.First;

        while (node != null)
        {
            LinkedListNode<EventMessage>? next = node.Next;
            EventMessage message = node.Value;

            float x, y;
            float lifePercent = ((float)(now - message.StartTicks)) / messageLifetime;
            if (lifePercent >= 1.0f)
            {   // msg is done.
                messages.Remove(node);
                node = next;
                continue;
            }
            x = (((float)windowWidth) - (message.Text.Length * DebugTextFontCharacterSize)) / 2.0f;
            y = ((float)windowHeight) * lifePercent;
            if ((previousY != 0.0f) && ((previousY - y) < ((float)DebugTextFontCharacterSize)))
            {
                message.StartTicks = now;
                break;  // wait for the previous message to tick up a little.
            }

            SetRenderDrawColor(renderer, message.Color.R, message.Color.G, message.Color.B, (byte)(((float)message.Color.A) * (1.0f - lifePercent)));
            RenderDebugText(renderer, x, y, message.Text);

            previousY = y;
            node = next;
        }

        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        Quit();
        // SDL will clean up the window/renderer for us. We let the joysticks leak.
    }
}