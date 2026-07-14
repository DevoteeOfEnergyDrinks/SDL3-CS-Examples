/*
 * This example creates an SDL window and renderer, and then draws a scene
 * to it every frame, while sliding around a clipping rectangle.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */
using System.Runtime.InteropServices;

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
    public static FPoint clipRectPosition;
    public static FPoint clipRectDirection;
    public static ulong lastTime = 0;

    const int WindowWidth = 640;
    const int WindowHeigth = 480;
    const int ClipRectSize = 250;
    const int ClipRectSpeed = 200; // pixels per second


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

    // A lot of this program is examples/renderer/02-primitives, 
    // so we have a good visual that we can slide a clip rect around. 
    // The actual new magic in here is the SDL_SetRenderClipRect() function.

    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        IntPtr surfacePtr = IntPtr.Zero;
        string pngPath;

        SetAppMetadata("Example Renderer Clipping Rectangle", "1.0", "com.example.renderer-cliprect");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/cliprect", WindowWidth, WindowHeigth, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        SetRenderLogicalPresentation(renderer, WindowWidth, WindowHeigth, RendererLogicalPresentation.Letterbox);

        clipRectDirection.X = clipRectDirection.Y = 1.0f;

        lastTime = GetTicks();

        // Textures are pixel data that we upload to the video hardware for fast drawing. 
        // Lots of 2D engines refer to these as "sprites." 
        // We'll do a static texture (upload once, draw many times) with data from a bitmap file.

        // SDL_Surface is pixel data the CPU can access. SDL_Texture is pixel data the GPU can access.
        // Load a .png into a surface, move it to a texture from there. 
        pngPath = GetBasePath() + "assets/sample.png";  // build a string of the full file path
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
        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(8);
        Rect cliprect = new()
        {
            X = (int)MathF.Round(clipRectPosition.X),
            Y = (int)MathF.Round(clipRectPosition.Y),
            H = ClipRectSize,
            W = ClipRectSize
        };
        ulong now = GetTicks();
        float elapsed = ((float)(now - lastTime)) / 1000.0f;  // seconds since last iteration
        float distance = elapsed * ClipRectSpeed;

        // Set a new clipping rectangle position
        clipRectPosition.X += distance * clipRectDirection.X;
        if (clipRectPosition.X < -ClipRectSize)
        {
            clipRectPosition.X = -ClipRectSize;
            clipRectDirection.X = 1.0f;
        }
        else if (clipRectPosition.X >= WindowWidth)
        {
            clipRectPosition.X = WindowWidth - 1;
            clipRectDirection.X = -1.0f;
        }

        clipRectPosition.Y += distance * clipRectDirection.Y;
        if (clipRectPosition.Y < -ClipRectSize)
        {
            clipRectPosition.Y = -ClipRectSize;
            clipRectDirection.Y = 1.0f;
        }
        else if (clipRectPosition.Y >= WindowHeigth)
        {
            clipRectPosition.Y = WindowHeigth - 1;
            clipRectDirection.Y = -1.0f;
        }
        SetRenderClipRect(renderer, in cliprect);

        lastTime = now;

        // okay, now draw!

        // Note that SDL_RenderClear is _not_ affected by the clipping rectangle!
        SetRenderDrawColor(renderer, 33, 33, 33, byte.MaxValue);  // grey, full alpha
        RenderClear(renderer);  // start with a blank canvas.

        // stretch the texture across the entire window. Only the piece in the
        // clipping rectangle will actually render, though!
        RenderTexture(renderer, texture, IntPtr.Zero, IntPtr.Zero);

        RenderPresent(renderer);  // put it all on the screen!

        return AppResult.Continue;
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        DestroyTexture(texture);
        // SDL will clean up the window/renderer for us.
    }
}