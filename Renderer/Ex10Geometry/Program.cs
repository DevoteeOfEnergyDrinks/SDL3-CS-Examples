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

        SetAppMetadata("Example Renderer Scaling Textures", "1.0", "com.example.renderer-geometry");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/geometry", WindowWidth, WindowHeight, WindowFlags.Resizable, out window, out renderer))
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
        // Adding a delay to stop very high FPS
        Delay(6);
        ulong now = GetTicks();

        // we'll have the triangle grow and shrink over a few seconds.
        float direction = ((now % 2000) >= 1000) ? 1.0f : -1.0f;
        float scale = ((float)(((int)(now % 1000)) - 500) / 500.0f) * direction;
        float size = 200.0f + (200.0f * scale);

        Vertex[] vertices = new Vertex[4];
        int i;

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);  // black, full alpha
        RenderClear(renderer);  // start with a blank canvas.

        // Draw a single triangle with a different color at each vertex. Center this one and make it grow and shrink.
        // You always draw triangles with this, but you can string triangles together to form polygons.
        vertices[0].Position.X = ((float)WindowWidth) / 2.0f;
        vertices[0].Position.Y = (((float)WindowHeight) - size) / 2.0f;
        vertices[0].Color.R = 1.0f;
        vertices[0].Color.A = 1.0f;
        vertices[1].Position.X = (((float)WindowWidth) + size) / 2.0f;
        vertices[1].Position.Y = (((float)WindowHeight) + size) / 2.0f;
        vertices[1].Color.G = 1.0f;
        vertices[1].Color.A = 1.0f;
        vertices[2].Position.X = (((float)WindowWidth) - size) / 2.0f;
        vertices[2].Position.Y = (((float)WindowHeight) + size) / 2.0f;
        vertices[2].Color.B = 1.0f;
        vertices[2].Color.A = 1.0f;

        RenderGeometry(renderer, IntPtr.Zero, vertices, 3, IntPtr.Zero, 0);

        // you can also map a texture to the geometry! Texture coordinates go from 0.0f to 1.0f. 
        // That will be the location in the texture bound to this vertex.
        vertices[0].Position.X = 10.0f;
        vertices[0].Position.Y = 10.0f;
        vertices[0].Color.R = vertices[0].Color.G = vertices[0].Color.B = vertices[0].Color.A = 1.0f;
        vertices[0].TexCoord.X = 0.0f;
        vertices[0].TexCoord.Y = 0.0f;
        vertices[1].Position.X = 150.0f;
        vertices[1].Position.Y = 10.0f;
        vertices[1].Color.R = vertices[1].Color.G = vertices[1].Color.B = vertices[1].Color.A = 1.0f;
        vertices[1].TexCoord.X = 1.0f;
        vertices[1].TexCoord.Y = 0.0f;
        vertices[2].Position.X = 10.0f;
        vertices[2].Position.Y = 150.0f;
        vertices[2].Color.R = vertices[2].Color.G = vertices[2].Color.B = vertices[2].Color.A = 1.0f;
        vertices[2].TexCoord.X = 0.0f;
        vertices[2].TexCoord.Y = 1.0f;
        RenderGeometry(renderer, texture, vertices, 3, IntPtr.Zero, 0);

        // Did that only draw half of the texture? You can do multiple triangles sharing some vertices,
        // using indices, to get the whole thing on the screen:

        // Let's just move this over so it doesn't overlap...
        for (i = 0; i < 3; i++)
        {
            vertices[i].Position.X += 450;
        }

        // we need one more vertex, since the two triangles can share two of them.
        vertices[3].Position.X = 600.0f;
        vertices[3].Position.Y = 150.0f;
        vertices[3].Color.R = vertices[3].Color.G = vertices[3].Color.B = vertices[3].Color.A = 1.0f;
        vertices[3].TexCoord.X = 1.0f;
        vertices[3].TexCoord.Y = 1.0f;

        // And an index to tell it to reuse some of the vertices between triangles...
        {
            // 4 vertices, but 6 actual places they used. Indices need less bandwidth to transfer and can reorder vertices easily!
            int[] indices = [0, 1, 2, 1, 2, 3];
            RenderGeometry(renderer, texture, vertices, 4, indices, indices.Length);
        }

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