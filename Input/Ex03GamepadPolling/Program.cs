/*
 * This example code looks for the current gamepad state once per frame,
 * and draws a visual representation of it. See 01-joystick-polling for the
 * equivalent example code for the lower-level joystick API.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */

//Joysticks are low-level interfaces: there's something with a bunch of
//buttons, axes and hats, in no understood order or position. This is
//a flexible interface, but you'll need to build some sort of configuration
//UI to let people tell you what button, etc, does what. On top of this
//interface, SDL offers the "gamepad" API, which works with lots of devices,
//and knows how to map arbitrary buttons and such to look like an
//Xbox/PlayStation/etc gamepad. This is easier, and better, for many games,
//but isn't necessarily a good fit for complex apps and hardware. A flight
//simulator, a realistic racing game, etc, might want the joystick interface
//instead of gamepads. */

// SDL can handle multiple gamepads, but for simplicity, this program only
// deals with the first gamepad it sees.
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
    public static IntPtr texture = IntPtr.Zero;
    public static IntPtr gamepad = IntPtr.Zero;

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
        string pngPath;
        IntPtr surfacePtr = IntPtr.Zero;

        SetAppMetadata("Example Input Gamepad Polling", "1.0", "com.example.input-gamepad-polling");

        if (!Init(InitFlags.Video | InitFlags.Gamepad))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/input/gamepad-polling", WindowWidth, WindowHeight, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        if (!SetRenderLogicalPresentation(renderer, WindowWidth, WindowHeight, RendererLogicalPresentation.Stretch))
        {
            return AppResult.Failure;
        }

        // Textures are pixel data that we upload to the video hardware for fast drawing. 
        // Lots of 2D engines refer to these as "sprites." 
        // We'll do a static texture (upload once, draw many times) with data from a bitmap file.

        // SDL_Surface is pixel data the CPU can access. SDL_Texture is pixel data the GPU can access.
        // Load a .png into a surface, move it to a texture from there.
        pngPath = GetBasePath() + "Assets/gamepad_front.png";  // build the string of the full file path
        surfacePtr = LoadPNG(pngPath);
        if (surfacePtr == IntPtr.Zero)
        {
            Log($"Couldn't load bitmap: {GetError()}");
            return AppResult.Failure;
        }

        texture = CreateTextureFromSurface(renderer, surfacePtr);
        if (texture == IntPtr.Zero)
        {
            Log($"Couldn't create static texture: {GetError()}");
            return AppResult.Failure;
        }

        DestroySurface(surfacePtr);  // done with this, the texture has a copy of the pixels now.

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
        {
            // this event is sent for each hotplugged gamepad, but also each already-connected gamepad during SDL_Init().
            if (gamepad == IntPtr.Zero)
            {   // we don't have a stick yet and one was added, open it!
                gamepad = OpenGamepad(evt.GDevice.Which);
                if (gamepad == IntPtr.Zero)
                {
                    Log($"Failed to open gamepad ID {evt.GDevice.Which}: {GetError()}");
                }
            }
        }
        else if (evt.Type == (uint)EventType.GamepadRemoved)
        {
            if (gamepad != IntPtr.Zero && (GetGamepadID(gamepad) == evt.GDevice.Which))
            {
                CloseGamepad(gamepad);  // our controller was unplugged.
                gamepad = IntPtr.Zero;
            }
        }
        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        string text = "Plug in a gamepad, please.";
        ulong leftthumblast = 0xFFFFFFFF;
        ulong rightthumblast = 0xFFFFFFFF;
        ulong now = GetTicks();
        short axisX, axisY;
        float x, y;
        int i;

        if (gamepad != IntPtr.Zero)
        {   // we have a stick opened?
            text = GetGamepadName(gamepad)!;
        }

        SetRenderDrawColor(renderer, 0xFF, 0xFF, 0xFF, 0xFF);   // white
        RenderClear(renderer);

        // note that you can get input as events, instead of polling, which is
        // better since it won't miss button presses if the system is lagging,
        // but often times checking the current state per-frame is good enough,
        // and maybe better if you'd rather _drop_ inputs due to lag.

        if (gamepad != IntPtr.Zero)
        {   // we have a stick opened?
            // where to draw the buttons
            FRect[] buttons = [
                new() { X = 497, Y = 266,  W = 38, H = 38 },    // SDL_GAMEPAD_BUTTON_SOUTH
                new() { X = 550, Y = 217,  W = 38, H = 38 },    // SDL_GAMEPAD_BUTTON_EAST
                new() { X = 445, Y = 221,  W = 38, H = 38 },    // SDL_GAMEPAD_BUTTON_WEST
                new() { X = 499, Y = 173,  W = 38, H = 38 },    // SDL_GAMEPAD_BUTTON_NORTH
                new() { X = 235, Y = 228,  W = 32, H = 29 },    // SDL_GAMEPAD_BUTTON_BACK
                new() { X = 287, Y = 195,  W = 69, H = 69 },    // SDL_GAMEPAD_BUTTON_GUIDE
                new() { X = 377, Y = 228,  W = 32, H = 29 },    // SDL_GAMEPAD_BUTTON_START
                new() { X = 91,  Y = 234,  W = 63, H = 63 },    // SDL_GAMEPAD_BUTTON_LEFT_STICK
                new() { X = 381, Y = 354,  W = 63, H = 63 },    // SDL_GAMEPAD_BUTTON_RIGHT_STICK
                new() { X = 74,  Y = 73,   W = 102,H = 29 },    // SDL_GAMEPAD_BUTTON_LEFT_SHOULDER
                new() { X = 468, Y = 73,   W = 102,H = 29 },    // SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER
                new() { X = 207, Y = 316,  W = 32, H = 32 },    // SDL_GAMEPAD_BUTTON_DPAD_UP
                new() { X = 207, Y = 384,  W = 32, H = 32 },    // SDL_GAMEPAD_BUTTON_DPAD_DOWN
                new() { X = 173, Y = 351,  W = 32, H = 32 },    // SDL_GAMEPAD_BUTTON_DPAD_LEFT
                new() { X = 242, Y = 351,  W = 32, H = 32 },    // SDL_GAMEPAD_BUTTON_DPAD_RIGHT
                new() { X = 310, Y = 286,  W = 23, H = 27 },    // SDL_GAMEPAD_BUTTON_MISC1
                // there are other buttons: paddles on the back of the gamepad, touchpads, etc, but this is good enough for now.
            ];

            RenderTexture(renderer, texture, IntPtr.Zero, IntPtr.Zero);  // draw the gamepad picture to the whole window.

            // draw green boxes over buttons that are currently pressed.
            SetRenderDrawColor(renderer, 0x00, 0xFF, 0x00, 0xFF);   // green
            for (i = 0; i < buttons.Length; i++)
            {
                if (GetGamepadButton(gamepad, (GamepadButton)i))
                {
                    RenderFillRect(renderer, buttons[i]);
                }
            }

            // draw axes in blue.
            SetRenderDrawColor(renderer, 0x00, 0x00, 0xFF, 0xFF);   // blue

            // left thumb axis.
            axisX = GetGamepadAxis(gamepad, GamepadAxis.LeftX);
            axisY = GetGamepadAxis(gamepad, GamepadAxis.LeftY);
            if ((MathF.Abs(axisX) > 1000) || (MathF.Abs(axisY) > 1000))
            {   // zero means centered, but it might be a little off zero...
                leftthumblast = now;    // keep drawing, we're still moving.
            }
            if ((now - leftthumblast) < 500)
            {   // draw if there was movement in the last half-second.
                FRect box = new() { X = 107 + ((axisX / 32767.0f) * 30.0f), Y = 252 + ((axisY / 32767.0f) * 30.0f), W = 30, H = 30 };
                RenderFillRect(renderer, in box);
            }

            // right thumb axis.
            axisX = GetGamepadAxis(gamepad, GamepadAxis.RightX);
            axisY = GetGamepadAxis(gamepad, GamepadAxis.RightY);
            if ((MathF.Abs(axisX) > 1000) || (MathF.Abs(axisY) > 1000))
            {   // zero means centered, but it might be a little off zero...
                rightthumblast = now;   // keep drawing, we're still moving.
            }
            if ((now - rightthumblast) < 500)
            {   // draw if there was movement in the last half-second.
                FRect box = new() { X = 397 + ((axisX / 32767.0f) * 30.0f), Y = 370 + ((axisY / 32767.0f) * 30.0f), W = 30, H = 30 };
                RenderFillRect(renderer, in box);
            }

            // left trigger.
            axisY = GetGamepadAxis(gamepad, GamepadAxis.LeftTrigger);
            if (axisY > 1000)
            {   // zero means unpressed, but it might be a little off zero...
                float height = ((axisY / 32767.0f) * 65.0f);
                FRect box = new() { X = 127, Y = 1 + (65.0f - height), W = 37, H = height };
                RenderFillRect(renderer, in box);
            }

            // right trigger.
            axisY = GetGamepadAxis(gamepad, GamepadAxis.RightTrigger);
            if (axisY > 1000)
            {   // zero means unpressed, but it might be a little off zero...
                float height = ((axisY / 32767.0f) * 65.0f);
                FRect box = new() { X = 481, Y = 1 + (65.0f - height), W = 37, H = height };
                RenderFillRect(renderer, in box);
            }
        }

        x = (((float)WindowWidth) - ((text.Length) * DebugTextFontCharacterSize)) / 2.0f;
        if (gamepad != IntPtr.Zero)
        {
            y = (float)(WindowHeight - (DebugTextFontCharacterSize + 2));
        }
        else
        {
            y = (((float)WindowHeight) - DebugTextFontCharacterSize) / 2.0f;
        }
        SetRenderDrawColor(renderer, 0x00, 0x00, 0xFF, 0xFF);  // blue
        RenderDebugText(renderer, x, y, text);
        RenderPresent(renderer);
        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        DestroyTexture(texture);
        CloseGamepad(gamepad);
        // SDL will clean up the window/renderer for us.
    }
}