/*
 * This example creates an SDL window and renderer, and then draws some
 * textures to it every frame, adjusting their color.
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
        IntPtr surfacePtr = IntPtr.Zero;
        string pngPath;

        SetAppMetadata("Example Renderer Color Mods", "1.0", "com.example.renderer-color-mods");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/color-mods", WindowWidth, WindowHeight, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        SetRenderLogicalPresentation(renderer, WindowWidth, WindowHeight, RendererLogicalPresentation.Letterbox);

        // Textures are pixel data that we upload to the video hardware for fast drawing. 
        // Lots of 2D engines refer to these as "sprites." 
        // We'll do a static texture (upload once, draw many times) with data from a bitmap file.

        // SDL_Surface is pixel data the CPU can access. SDL_Texture is pixel data the GPU can access.
        // Load a .png into a surface, move it to a texture from there. 
        pngPath = GetBasePath() + "Assets/sample.png";  // build a string of the full file path
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
        FRect dstRect;
        double now = ((double)GetTicks()) / 1000.0;  // convert from milliseconds to seconds.
        // choose the modulation values for the center texture. The sine wave trick makes it fade between colors smoothly.
        float red = (float)(0.5 + 0.5 * Math.Sin(now));
        float green = (float)(0.5 + 0.5 * Math.Sin(now + Math.PI * 2 / 3));
        float blue = (float)(0.5 + 0.5 * Math.Sin(now + Math.PI * 4 / 3));

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);  // black, full alpha
        RenderClear(renderer);  // start with a blank canvas.

        // Just draw the static texture a few times. 
        // You can think of it like a stamp, there isn't a limit to the number of times you can draw with it.

        // Color modulation multiplies each pixel's red, green, and blue intensities by the mod values,
        // so multiplying by 1.0f will leave a color intensity alone, 
        // 0.0f will shut off that color completely, etc.

        // top left; let's make this one blue!
        dstRect.X = 0.0f;
        dstRect.Y = 0.0f;
        dstRect.W = (float)textureWidth;
        dstRect.H = (float)textureHeight;
        SetTextureColorModFloat(texture, 0.0f, 0.0f, 1.0f);  // kill all red and green.
        RenderTexture(renderer, texture, IntPtr.Zero, in dstRect);

        // center this one, and have it cycle through red/green/blue modulations.
        dstRect.X = ((float)(WindowWidth - textureWidth)) / 2.0f;
        dstRect.Y = ((float)(WindowHeight - textureHeight)) / 2.0f;
        dstRect.W = (float)textureWidth;
        dstRect.H = (float)textureHeight;
        SetTextureColorModFloat(texture, red, green, blue);
        RenderTexture(renderer, texture, IntPtr.Zero, in dstRect);

        // bottom right; let's make this one red!
        dstRect.X = (float)(WindowWidth - textureWidth);
        dstRect.Y = (float)(WindowHeight - textureHeight);
        dstRect.W = (float)textureWidth;
        dstRect.H = (float)textureHeight;
        SetTextureColorModFloat(texture, 1.0f, 0.0f, 0.0f);  // kill all green and blue.
        RenderTexture(renderer, texture, IntPtr.Zero, dstRect);

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