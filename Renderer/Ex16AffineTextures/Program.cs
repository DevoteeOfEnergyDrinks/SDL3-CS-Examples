/*
* This example creates an SDL window and renderer, and then draws a cube
* using affine-transformed textures every frame.
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

        SetAppMetadata("Example Renderer Affine Textures", "1.0", "com.example.renderer-affine-textures");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/clear", WindowWidth, WindowHeight, WindowFlags.Resizable, out window, out renderer))
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
        Delay(6);
        float x0 = 0.5f * WindowWidth;
        float y0 = 0.5f * WindowHeight;
        float px = Math.Min(WindowWidth, WindowHeight) / MathF.Sqrt(3.0f);

        ulong now = GetTicks();
        float rad = (((float)((int)(now % 2000))) / 2000.0f) * MathF.PI * 2;
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        float[] k = [3.0f / MathF.Sqrt(50.0f), 4.0f / MathF.Sqrt(50.0f), 5.0f / MathF.Sqrt(50.0f)];
        float[] mat =
        [
             cos      + (1.0f-cos)*k[0]*k[0], -sin*k[2] + (1.0f-cos)*k[0]*k[1],  sin*k[1] + (1.0f-cos)*k[0]*k[2],
             sin*k[2] + (1.0f-cos)*k[0]*k[1],  cos      + (1.0f-cos)*k[1]*k[1], -sin*k[0] + (1.0f-cos)*k[1]*k[2],
            -sin*k[1] + (1.0f-cos)*k[0]*k[2],  sin*k[0] + (1.0f-cos)*k[1]*k[2],  cos      + (1.0f-cos)*k[2]*k[2],
        ];

        float[] corners = new float[16];
        int i;

        for (i = 0; i < 8; i++)
        {
            float x = (i & 1) != 0 ? -0.5f : 0.5f;
            float y = (i & 2) != 0 ? -0.5f : 0.5f;
            float z = (i & 4) != 0 ? -0.5f : 0.5f;
            corners[0 + 2 * i] = mat[0] * x + mat[1] * y + mat[2] * z;
            corners[1 + 2 * i] = mat[3] * x + mat[4] * y + mat[5] * z;
        }

        SetRenderDrawColor(renderer, 0x42, 0x87, 0xf5, byte.MaxValue);  // light blue background.
        RenderClear(renderer);

        for (i = 1; i < 7; i++)
        {
            int dir = 3 & ((i & 4) != 0 ? ~i : i);
            int odd = (i & 1) ^ ((i & 2) >> 1) ^ ((i & 4) >> 2);
            if (0 < (odd != 0 ? 1.0f : -1.0f) * mat[5 + dir]) continue;
            int origin_index = (1 << ((dir - 1) % 3));
            int right_index = (1 << ((dir + odd) % 3)) | origin_index;
            int down_index = (1 << ((dir + (odd ^ 1)) % 3)) | origin_index;
            if (odd != 0)
            {
                origin_index ^= 7;
                right_index ^= 7;
                down_index ^= 7;
            }
            FPoint origin, right, down;
            origin.X = x0 + px * corners[0 + 2 * origin_index];
            origin.Y = y0 + px * corners[1 + 2 * origin_index];
            right.X = x0 + px * corners[0 + 2 * right_index];
            right.Y = y0 + px * corners[1 + 2 * right_index];
            down.X = x0 + px * corners[0 + 2 * down_index];
            down.Y = y0 + px * corners[1 + 2 * down_index];

            RenderTextureAffine(renderer, texture, IntPtr.Zero, in origin, in right, in down);
        }
        
        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        DestroyTexture(texture);
        // SDL will clean up the window/renderer for us.
    }
}