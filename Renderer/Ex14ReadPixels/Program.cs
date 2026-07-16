/*
 * This example creates an SDL window and renderer, and draws a
 * rotating texture to it, reads back the rendered pixels, converts them to
 * black and white, and then draws the converted image to a corner of the
 * screen.
 *
 * This isn't necessarily an efficient thing to do--in real life one might
 * want to do this sort of thing with a render target--but it's just a visual
 * example of how to use SDL_RenderReadPixels().
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

    static IntPtr texture = IntPtr.Zero;
    static int textureWidth = 0;
    static int textureHeight = 0;
    static IntPtr convertedTexture = IntPtr.Zero;
    static int convertedTextureWidth = 0;
    static int convertedTextureHeight = 0;

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

        SetAppMetadata("Example Renderer Read Pixels", "1.0", "com.example.renderer-read-pixels");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/read-pixels", WindowWidth, WindowHeight, WindowFlags.Resizable, out window, out renderer))
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
        Delay(6);
        ulong now = GetTicks();
        IntPtr surfacePtr = IntPtr.Zero;
        FPoint center;
        FRect dstRect;

        // we'll have a texture rotate around over 2 seconds (2000 milliseconds). 360 degrees in a circle!
        float rotation = (((float)((int)(now % 2000))) / 2000.0f) * 360.0f;

        // as you can see from this, rendering draws over whatever was drawn before it.
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);  // black, full alpha
        RenderClear(renderer);  // start with a blank canvas.

        // Center this one, and draw it with some rotation so it spins! */
        dstRect.X = ((float)(WindowWidth - textureWidth)) / 2.0f;
        dstRect.Y = ((float)(WindowHeight - textureHeight)) / 2.0f;
        dstRect.W = (float)textureWidth;
        dstRect.H = (float)textureHeight;
        // rotate it around the center of the texture; you can rotate it from a different point, too! */
        center.X = textureWidth / 2.0f;
        center.Y = textureHeight / 2.0f;
        RenderTextureRotated(renderer, texture, IntPtr.Zero, in dstRect, rotation, in center, FlipMode.None);

        // this next whole thing is _super_ expensive. Seriously, don't do this in real life.

        // Download the pixels of what has just been rendered. This has to wait for the GPU to finish rendering it and everything before it,
        // and then make an expensive copy from the GPU to system RAM!
        surfacePtr = RenderReadPixels(renderer, null);

        Surface surface = Marshal.PtrToStructure<Surface>(surfacePtr);
        // This is also expensive, but easier: convert the pixels to a format we want.
        if (surfacePtr != IntPtr.Zero && (surface.Format != PixelFormat.RGBA8888) && (surface.Format != PixelFormat.BGRA8888))
        {
            IntPtr convertedPtr = ConvertSurface(surfacePtr, PixelFormat.RGBA8888);
            DestroySurface(surfacePtr);
            surfacePtr = convertedPtr;
        }

        if (surfacePtr != IntPtr.Zero)
        {
            surface = Marshal.PtrToStructure<Surface>(surfacePtr);
            // Rebuild converted_texture if the dimensions have changed (window resized, etc).
            if ((surface.Width != convertedTextureWidth) || (surface.Height != convertedTextureHeight))
            {
                DestroyTexture(convertedTexture);
                convertedTexture = CreateTexture(renderer, PixelFormat.RGBA8888, TextureAccess.Streaming, surface.Width, surface.Height);
                if (convertedTexture == IntPtr.Zero)
                {
                    Log($"Couldn't (re)create conversion texture: {GetError()}");
                    return AppResult.Failure;
                }
                convertedTextureWidth = surface.Width;
                convertedTextureHeight = surface.Height;
            }

            // Turn each pixel into either black or white. This is a lousy technique but it works here.
            // In real life, something like Floyd-Steinberg dithering might work
            // better: https://en.wikipedia.org/wiki/Floyd%E2%80%93Steinberg_dithering*/
            int x, y;
            byte[] pixels = new byte[surface.Pitch * surface.Height];
            Marshal.Copy(surface.Pixels, pixels, 0, pixels.Length);
            for (y = 0; y < surface.Height; y++)
            {
                for (x = 0; x < surface.Width; x++)
                {
                    int offset = (y * surface.Pitch) + (x * 4);
                    int average = (pixels[offset + 1] + pixels[offset + 2] + pixels[offset + 3]) / 3;
                    if (average == 0)
                    {
                        // make pure black pixels red.
                        pixels[offset] = 0xFF;
                        pixels[offset + 1] = 0x00;
                        pixels[offset + 2] = 0x00;
                        pixels[offset + 3] = 0xFF;
                    }
                    else
                    {
                        // make everything else either black or white.
                        byte value = average > 50 ? (byte)0xFF : (byte)0x00;
                        pixels[offset + 1] = value;
                        pixels[offset + 2] = value;
                        pixels[offset + 3] = value;
                    }
                }
            }

            // upload the processed pixels back into a texture.
            UpdateTexture(convertedTexture, IntPtr.Zero, pixels, surface.Pitch);
            DestroySurface(surfacePtr);

            // draw the texture to the top-left of the screen.
            dstRect.X = dstRect.Y = 0.0f;
            dstRect.W = ((float)WindowWidth) / 4.0f;
            dstRect.H = ((float)WindowHeight) / 4.0f;
            RenderTexture(renderer, convertedTexture, IntPtr.Zero, in dstRect);
        }

        RenderPresent(renderer);  // put it all on the screen!

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        DestroyTexture(convertedTexture);
        DestroyTexture(texture);
        // SDL will clean up the window/renderer for us.
    }
}