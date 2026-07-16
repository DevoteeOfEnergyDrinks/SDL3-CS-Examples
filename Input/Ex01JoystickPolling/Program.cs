/*
 * This example code looks for the current joystick state once per frame,
 * and draws a visual representation of it.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */

/* Joysticks are low-level interfaces: there's something with a bunch of
   buttons, axes and hats, in no understood order or position. This is
   a flexible interface, but you'll need to build some sort of configuration
   UI to let people tell you what button, etc, does what. On top of this
   interface, SDL offers the "gamepad" API, which works with lots of devices,
   and knows how to map arbitrary buttons and such to look like an
   Xbox/PlayStation/etc gamepad. This is easier, and better, for many games,
   but isn't necessarily a good fit for complex apps and hardware. A flight
   simulator, a realistic racing game, etc, might want the joystick interface
   instead of gamepads. */

/* SDL can handle multiple joysticks, but for simplicity, this program only
   deals with the first stick it sees. */
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
    public static IntPtr joystick = IntPtr.Zero;
    public static Color[] colors = new Color[64];


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

        SetAppMetadata("Example Input Joystick Polling", "1.0", "com.example.input-joystick-polling");

        if (!Init(InitFlags.Video | InitFlags.Joystick))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/input/joystick-polling", 640, 480, WindowFlags.Resizable, out window, out renderer))
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

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        if (evt.Type == (uint)EventType.Quit)
        {
            return AppResult.Success;   // end the program, reporting success to the OS.
        }
        else if (evt.Type == ((uint)EventType.JoystickAdded))
        {
            // this event is sent for each hotplugged stick, but also each already-connected joystick during SDL_Init().
            if (joystick == IntPtr.Zero)
            {  // we don't have a stick yet and one was added, open it!
                joystick = OpenJoystick(evt.JDevice.Which);
                if (joystick == IntPtr.Zero)
                {
                    Log($"Failed to open joystick ID {evt.JDevice.Which}: {GetError()}");
                }
            }
        }
        else if (evt.Type == ((uint)EventType.JoystickRemoved))
        {
            if ((joystick == IntPtr.Zero) && (GetJoystickID(joystick) == evt.JDevice.Which))
            {
                CloseJoystick(joystick);  // our joystick was unplugged.
                joystick = IntPtr.Zero;
            }
        }
        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        int windowWidth = 640;
        int windowHeight = 480;
        string text = "Plug in a joystick, please.";
        float x, y;
        int i;

        if (joystick != IntPtr.Zero)
        {  /* we have a stick opened? */
            text = GetJoystickName(joystick)!;
        }

        SetRenderDrawColor(renderer, 0, 0, 0, 255);
        RenderClear(renderer);
        GetWindowSize(window, out windowWidth, out windowHeight);

        // note that you can get input as events, instead of polling, which is
        // better since it won't miss button presses if the system is lagging,
        // but often times checking the current state per-frame is good enough,
        // and maybe better if you'd rather _drop_ inputs due to lag.

        if (joystick != IntPtr.Zero)
        {  // we have a stick opened?
            float size = 30.0f;
            int total;

            // draw axes as bars going across middle of screen. We don't know if it's an X or Y or whatever axis, so we can't do more than this. */
            total = GetNumJoystickAxes(joystick);
            y = (windowHeight - (total * size)) / 2;
            x = ((float)windowWidth) / 2.0f;
            for (i = 0; i < total; i++)
            {
                Color color = colors[i % (colors.Length)];
                float val = (((float)GetJoystickAxis(joystick, i)) / 32767.0f);  /* make it -1.0f to 1.0f */
                float dx = x + (val * x);
                FRect dst = new() { X = dx, Y = y, W = x - MathF.Abs(dx), H = size };
                SetRenderDrawColor(renderer, color.R, color.G, color.B, color.A);
                RenderFillRect(renderer, in dst);
                y += size;
            }

            /* draw buttons as blocks across top of window. We only know the button numbers, but not where they are on the device. */
            total = GetNumJoystickButtons(joystick);
            x = (windowWidth - (total * size)) / 2;
            for (i = 0; i < total; i++)
            {
                Color color = colors[i % colors.Length];
                FRect dst = new() { X = x, Y = 0.0f, W = size, H = size };
                if (GetJoystickButton(joystick, i))
                {
                    SetRenderDrawColor(renderer, color.R, color.G, color.B, color.A);
                }
                else
                {
                    SetRenderDrawColor(renderer, 0, 0, 0, 255);
                }
                RenderFillRect(renderer, in dst);
                SetRenderDrawColor(renderer, 255, 255, 255, color.A);
                RenderRect(renderer, in dst);  /* outline it */
                x += size;
            }

            /* draw hats across the bottom of the screen. */
            total = GetNumJoystickHats(joystick);
            x = ((windowWidth - (total * (size * 2.0f))) / 2.0f) + (size / 2.0f);
            y = ((float)windowHeight) - size;
            for (i = 0; i < total; i++)
            {
                Color color = colors[i % colors.Length];
                float thirdsize = size / 3.0f;
                FRect[] cross =
                [
                    new FRect{ X = x, Y = y + thirdsize, W =  size, H = thirdsize },
                    new FRect{ X = x + thirdsize, Y = y, W = thirdsize, H = size }
                ];

                byte hat = (byte)GetJoystickHat(joystick, i);

                SetRenderDrawColor(renderer, 90, 90, 90, 255);
                RenderFillRects(renderer, cross, cross.Length);

                SetRenderDrawColor(renderer, color.R, color.G, color.B, color.A);

                if (hat == (byte)JoystickHat.Up)
                {
                    FRect dst = new() { X = x + thirdsize, Y = y, W = thirdsize, H = thirdsize };
                    RenderFillRect(renderer, in dst);
                }

                if (hat == ((byte)JoystickHat.Right))
                {
                    FRect dst = new() { X = x + (thirdsize * 2), Y = y + thirdsize, W = thirdsize, H = thirdsize };
                    RenderFillRect(renderer, in dst);
                }

                if (hat == ((byte)JoystickHat.Down))
                {
                    FRect dst = new() { X = x + thirdsize, Y = y + (thirdsize * 2), W = thirdsize, H = thirdsize };
                    RenderFillRect(renderer, in dst);
                }

                if (hat == ((byte)JoystickHat.Left))
                {
                    FRect dst = new() { X = x, Y = y + thirdsize, W = thirdsize, H = thirdsize };
                    RenderFillRect(renderer, in dst);
                }

                x += size * 2;
            }
        }

        x = (((float)windowWidth) - (text.Length) * DebugTextFontCharacterSize) / 2.0f;
        y = (((float)windowHeight) - DebugTextFontCharacterSize) / 2.0f;
        SetRenderDrawColor(renderer, 255, 255, 255, 255);
        RenderDebugText(renderer, x, y, text);
        RenderPresent(renderer);
        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
}