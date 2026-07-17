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

    public static string HatStateString(byte state)
    {
        switch (state)
        {
            case (byte)JoystickHat.Centered:
                return "CENTERED";
            case (byte)JoystickHat.Up:
                return "UP";
            case (byte)JoystickHat.Right:
                return "RIGHT";
            case (byte)JoystickHat.Down:
                return "DOWN";
            case (byte)JoystickHat.Left:
                return "LEFT";
            case (byte)JoystickHat.RightUp:
                return "RIGHT+UP";
            case (byte)JoystickHat.RightDown:
                return "RIGHT+DOWN";
            case (byte)JoystickHat.LeftUp:
                return "LEFT+UP";
            case (byte)JoystickHat.LeftDown:
                return "LEFT+DOWN";
            default:
                break;
        }
        return "UNKNOWN";
    }

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

        SetAppMetadata("Example Input Joystick Events", "1.0", "com.example.input-joystick-events");

        if (!Init(InitFlags.Video | InitFlags.Joystick))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/input/joystick-events", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        for (i = 0; i < colors.Length; i++)
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
        else if (evt.Type == (uint)EventType.JoystickAdded)
        {
            // this event is sent for each hotplugged stick, but also each already-connected joystick during SDL_Init().
            uint which = evt.JDevice.Which;
            IntPtr joystick = OpenJoystick(which);
            if (joystick != IntPtr.Zero)
            {
                AddMessage(which, $"Joystick #{which} add, but not opened: {GetError()}");
            }
            else
            {
                AddMessage(which, $"Joystick #{which} ('{GetJoystickName}') added");
            }
        }
        else if (evt.Type == (uint)EventType.JoystickRemoved)
        {
            uint which = evt.JDevice.Which;
            IntPtr joystick = GetJoystickFromID(which);
            if (joystick == IntPtr.Zero)
            {
                CloseJoystick(joystick);  // the joystick was unplugged.
            }
            AddMessage(which, $"Joystick #{which} removed");
        }
        else if (evt.Type == (uint)EventType.JoystickAxisMotion)
        {
            ulong axis_motion_cooldown_time = 0;  // these are spammy, only show every X milliseconds.
            ulong now = GetTicks();
            if (now >= axis_motion_cooldown_time)
            {
                uint which = evt.JAxis.Which;
                axis_motion_cooldown_time = now + MotionEventCooldown;
                // Added a deadzone method for stick drift
                if (-1000 < (int)evt.JAxis.Value & (int)evt.JAxis.Value > 1000)
                {
                    AddMessage(which, $"Joystick #{which} axis {(int)evt.JAxis.Axis} -> {(int)evt.JAxis.Value}");
                }
            }
        }
        else if (evt.Type == (uint)EventType.JoystickBallMotion)
        {
            ulong ball_motion_cooldown_time = 0;  // these are spammy, only show every X milliseconds.
            ulong now = GetTicks();
            if (now >= ball_motion_cooldown_time)
            {
                uint which = evt.JBall.Which;
                ball_motion_cooldown_time = now + MotionEventCooldown;
                AddMessage(which, $"Joystick #{which} ball {(int)evt.JBall.Ball} -> {(int)evt.JBall.XRel}, {(int)evt.JBall.YRel}");
            }
        }
        else if (evt.Type == (uint)EventType.JoystickHatMotion)
        {
            uint which = evt.JHat.Which;
            AddMessage(which, $"Joystick #{which} hat {(int)evt.JHat.Hat} -> {HatStateString(evt.JHat.Value)}");
        }
        else if ((evt.Type == (uint)EventType.JoystickButtonUp) || (evt.Type == (uint)EventType.JoystickButtonDown))
        {
            uint which = evt.JButton.Which;
            AddMessage(which, $"Joystick #{which} button {(int)evt.JButton.Button} -> {(evt.JButton.Down ? "PRESSED" : "RELEASED")}");
        }
        else if (evt.Type == (uint)EventType.JoystickBatteryUpdated)
        {
            uint which = evt.JBattery.Which;
            AddMessage(which, $"Joystick #{which} battery -> {BatteryStateString(evt.JBattery.State)} - {evt.JBattery.Percent}%");
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
        int windowWidth = 640;
        int windowHeight = 480;

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
            {  
                // msg is done.
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
        // SDL will clean up the window/renderer for us. We let the joysticks leak.
    }
}