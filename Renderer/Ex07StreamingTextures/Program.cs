/*
* This example code creates an SDL window and renderer, and then clears the
* window to a different color every frame, so you'll effectively get a window
* that's smoothly fading between colors.
*
* This code is public domain. Feel free to use it for any purpose!
* This code is a port of the official SDL3 examples
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

    const int TextureSize = 150;
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
        SetAppMetadata("Example Renderer Streaming Textures", "1.0", "com.example.renderer-streaming-textures");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/streaming-textures", WindowWidth, WindowHeight, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        SetRenderLogicalPresentation(renderer, WindowWidth, WindowHeight, RendererLogicalPresentation.Letterbox);

        texture = CreateTexture(renderer, PixelFormat.RGBA8888, TextureAccess.Streaming, TextureSize, TextureSize);
        if (texture == IntPtr.Zero)
        {
            Log($"Couldn't create streaming texture: {GetError()}");
            return AppResult.Failure;
        }

        return AppResult.Continue;  // carry on with the program! */
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
        FRect dstRect;
        ulong now = GetTicks();
        IntPtr surfacePtr;

        // we'll have some color move around over a few seconds.
        float direction = ((now % 2000) >= 1000) ? 1.0f : -1.0f;
        float scale = ((float)(((int)(now % 1000)) - 500) / 500.0f) * direction;

        // To update a streaming texture, you need to lock it first. This gets you access to the pixels.
        // Note that this is considered a _write-only_ operation: the buffer you get from locking
        // might not actually have the existing contents of the texture, and you have to write to every locked pixel!

        // You can use SDL_LockTexture() to get an array of raw pixels, but we're going to use
        // SDL_LockTextureToSurface() here, because it wraps that array in a temporary SDL_Surface,
        // letting us use the surface drawing functions instead of lighting up individual pixels.
        if (LockTextureToSurface(texture, IntPtr.Zero, out surfacePtr))
        {
            Rect r;
            // Look at performance and use an unsafe context to access Format directly
            Surface surface = Marshal.PtrToStructure<Surface>(surfacePtr);
            FillSurfaceRect(surfacePtr, IntPtr.Zero, MapRGB(GetPixelFormatDetails(surface.Format), IntPtr.Zero, 0, 0, 0));  // make the whole surface black
            r.W = TextureSize;
            r.H = TextureSize / 10;
            r.X = 0;
            r.Y = (int)(((float)(TextureSize - r.H)) * ((scale + 1.0f) / 2.0f));
            FillSurfaceRect(surfacePtr, in r, MapRGB(GetPixelFormatDetails(surface.Format), IntPtr.Zero, 0, 255, 0));  // make a strip of the surface green
            UnlockTexture(texture);  // upload the changes (and frees the temporary surface)!
        }

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 66, 66, 66, byte.MaxValue);  // grey, full alpha 
        RenderClear(renderer);  // start with a blank canvas.

        // Just draw the static texture a few times. You can think of it like a
        // stamp, there isn't a limit to the number of times you can draw with it.

        // Center this one. It'll draw the latest version of the texture we drew while it was locked.
        dstRect.X = ((float)(WindowWidth - TextureSize)) / 2.0f;
        dstRect.Y = ((float)(WindowHeight - TextureSize)) / 2.0f;
        dstRect.W = dstRect.H = (float)TextureSize;
        RenderTexture(renderer, texture, IntPtr.Zero, in dstRect);

        RenderPresent(renderer);  // put it all on the screen!

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        DestroyTexture(texture);
        // SDL will clean up the window/renderer for us.
    }
}