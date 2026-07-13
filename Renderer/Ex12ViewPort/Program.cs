/*
 * This example creates an SDL window and renderer, and then draws some
 * textures to it every frame, adjusting the viewport.
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

    static int textureWidth = 0;
    static int textureHeight = 0;

    const int WindowWidth = 640;
    const int WindowHeigth = 480;


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
        IntPtr surfacePtr = IntPtr.Zero;
        string pngPath;

        SetAppMetadata("Example Renderer Viewport", "1.0", "com.example.renderer-viewport");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/viewport", WindowWidth, WindowHeigth, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        SetRenderLogicalPresentation(renderer, WindowWidth, WindowHeigth, RendererLogicalPresentation.Letterbox);

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

        Surface surface = Marshal.PtrToStructure<Surface>(surfacePtr);

        textureWidth = surface.Width;
        textureHeight = surface.Height;

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
        FRect dstRect = new() { X = 0, Y = 0, W = (float)textureWidth, H = (float)textureHeight };
        Rect viewport;

        // Setting a viewport has the effect of limiting the area that rendering can happen, 
        // and making coordinate (0, 0) live somewhere else in the window. 
        // It does _not_ scale rendering to fit the viewport.

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);  // black, full alpha 
        RenderClear(renderer);  // start with a blank canvas.

        // Draw once with the whole window as the viewport.
        viewport.X = 0;
        viewport.Y = 0;
        viewport.W = WindowWidth / 2;
        viewport.H = WindowHeigth / 2;
        SetRenderViewport(renderer, IntPtr.Zero);  // IntPtr.Zero means "use the whole window"
        RenderTexture(renderer, texture, IntPtr.Zero, in dstRect);

        // top right quarter of the window.
        viewport.X = WindowWidth / 2;
        viewport.Y = WindowHeigth / 2;
        viewport.W = WindowWidth / 2;
        viewport.H = WindowHeigth / 2;
        SetRenderViewport(renderer, in viewport);
        RenderTexture(renderer, texture, IntPtr.Zero, in dstRect);

        // bottom 20% of the window. Note it clips the width!
        viewport.X = 0;
        viewport.Y = WindowHeigth - (WindowHeigth / 5);
        viewport.W = WindowWidth / 5;
        viewport.H = WindowHeigth / 5;
        SetRenderViewport(renderer, in viewport);
        RenderTexture(renderer, texture, IntPtr.Zero, in dstRect);

        // what happens if you try to draw above the viewport? It should clip!
        viewport.X = 100;
        viewport.Y = 200;
        viewport.W = WindowWidth;
        viewport.H = WindowHeigth;
        SetRenderViewport(renderer, in viewport);
        dstRect.Y = -50;
        RenderTexture(renderer, texture, IntPtr.Zero, in dstRect);
        
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