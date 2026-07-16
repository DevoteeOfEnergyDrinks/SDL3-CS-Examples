/*
 * Blending combines a source color 'src',
 * with the pixels already on the screen 'dst',
 * to produce transparency and other visual effects.
 *
 * formula: dst := (a * dst) op (b * src)
 *
 * where:
 *     dst: existed pixel on the screen.
 *     src: new pixel.
 *     a:   dst factor.
 *     b:   src factor.
 *     op:  blend operation (usually addition).
 *
 * In graphics programming, color and alpha are usually blended separately:
 *     dstRGB := (a * srcRGB) op (b * dstRGB)
 *     dstA   := (c * srcA)   op (d * dstA)
 *
 * This example uses SDL_SetTextureBlendMode() to apply blending to textures,
 * and uses SDL_ComposeCustomBlendMode() to create a custom blend mode.
 *
 * You can also use SDL_SetRenderDrawBlendMode() to apply blending to the
 * entire renderer, but it only affects filled rects and lines, not textures.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */
internal class Program
{
    // These delegates map our C# methods to the internal SDL3 lifecycle events.
    private static readonly AppInitFunc _init = new(AppInit);
    private static readonly AppIterateFunc _iterate = new(AppIterate);
    private static readonly AppEventFunc _event = new(AppEvent);
    private static readonly AppQuitFunc _quit = new(AppQuit);


    const int WindowWidth = 640;
    const int WindowHeight = 480;

    // UI Constants
    const int Rows = 2;
    const int Cols = 3;
    const float GridSize = ((WindowWidth - 1) / 18.0f);
    const float PanelSize = (GridSize * 4);
    const float RowOffset = ((WindowHeight - Rows * PanelSize) / 4);
    const float ColOffset = (GridSize * Cols);
    const float RectSize = 50.0f;
    const float RedOffset = (GridSize);
    const float GreenOffset = (RectSize / 3 + GridSize);
    const float BlueOffset = (RectSize * 2 / 3 + GridSize);

    // We use IntPtr (Integer Pointers) because SDL3 is a C library.
    // These variables hold the memory addresses of the window and the renderer.
    public static IntPtr window = IntPtr.Zero;
    public static IntPtr renderer = IntPtr.Zero;
    public static FRect[] panels = new FRect[Rows * Cols];
    public static IntPtr redRectTexture = IntPtr.Zero;
    public static IntPtr greenRectTexture = IntPtr.Zero;
    public static IntPtr blueRectTexture = IntPtr.Zero;
    public static byte alpha = 255;
    public static BlendMode[] blendModes =
    [
        /*The default no blending: dstRGB := srcRGB
                                    dstA  := srcA   */
        BlendMode.None,

        /* Alpha blending: dstRGB := srcA * srcRGB + (1 - srcA) * dstRGB
                           dstA   := srcA          + (1 - srcA) * dstA   */
        BlendMode.Blend,

        /* Additive blending: dstRGB := srcRGB + dstRGB
                              dstA   := srcA   + dstA   */
        BlendMode.Add,

        /* Modulate blending: dstRGB := srcRGB * dstRGB
                              dstA   := dstA            */
        BlendMode.Mod,

        /* Multiply blending: dstRGB := srcRGB * dstRGB + (1 - srcA) * dstRGB
                              dstA   := dstA                                  */
        BlendMode.Mul,

        /* Our custom blending 'Screen Blending': dstRGB := 1 - (1 - dstRGB) * (1 - srcRGB)
                                                  dstA   := dstA                            */
        0
    ];
    public static string[] blendModeNames =
    [
      "NONE",
      "BLEND",
      "ADD",
      "MOD",
      "MUL",
      "SCREEN \"COSTOM\""
    ];

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

        SetAppMetadata("Example Renderer Blending", "1.0", "com.example.renderer-blending");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/renderer/blending", WindowWidth, WindowHeight, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        SetRenderLogicalPresentation(renderer, WindowWidth, WindowHeight, RendererLogicalPresentation.Letterbox);

        int row = 0;
        int col = 0;
        for (row = 0; row < Rows; row++)
        {
            for (col = 0; col < Cols; col++)
            {
                panels[col + (row * Cols)] = new FRect
                {
                    X = col * PanelSize + col * ColOffset,
                    Y = row * PanelSize + (row + 1) * RowOffset,
                    H = PanelSize,
                    W = PanelSize
                };
            }
        }

        // Create 'screen blend' mode
        blendModes[Rows * Cols - 1] = ComposeCustomBlendMode
        (
            BlendFactor.OneMinusDstColor,    // srcRGB factor    := (1 - dstRGB)
            BlendFactor.One,                 // dstRGB factor    := 1           
            BlendOperation.Add,              // RGB    operation := +           
            BlendFactor.Zero,                // srcA   factor    := 0           
            BlendFactor.One,                 // dstA   factor    := dstA        
            BlendOperation.Add               // A      operation := +           
        );

        surfacePtr = CreateSurface((int)RectSize, (int)RectSize, PixelFormat.RGBA8888);
        if (surfacePtr == IntPtr.Zero)
        {
            Log($"Couldn't create surface: {GetError()}");
            return AppResult.Failure;
        }

        FillSurfaceRect(surfacePtr, IntPtr.Zero, 0xFF0000FF); // Red
        redRectTexture = CreateTextureFromSurface(renderer, surfacePtr);
        if (redRectTexture == IntPtr.Zero)
        {
            Log($"Couldn't create texture: {GetError()}");
            return AppResult.Failure;
        }

        FillSurfaceRect(surfacePtr, IntPtr.Zero, 0x00FF00FF); // Green
        greenRectTexture = CreateTextureFromSurface(renderer, surfacePtr);
        if (greenRectTexture == IntPtr.Zero)
        {
            Log($"Couldn't create texture: {GetError()}");
            return AppResult.Failure;
        }

        FillSurfaceRect(surfacePtr, IntPtr.Zero, 0x0000FFFF); // Blue
        blueRectTexture = CreateTextureFromSurface(renderer, surfacePtr);
        if (blueRectTexture == IntPtr.Zero)
        {
            Log($"Couldn't create texture: {GetError()}");
            return AppResult.Failure;
        }

        DestroySurface(surfacePtr);

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        if (evt.Type == (uint)EventType.Quit)
        {
            return AppResult.Success;   // end the program, reporting success to the OS.
        }
        if (evt.Type == (uint)EventType.KeyDown)
        {
            // UP arrow increase alpha
            if (evt.Key.Key == Keycode.Up && alpha <= 255 - 8)
                alpha += 8;
            // DOWN arrow decreases alpha
            if (evt.Key.Key == Keycode.Down && alpha >= 8)
                alpha -= 8;
        }
        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);
        RenderClear(renderer);

        int i = 0;
        float x;
        float y;
        // Render checkerboard panels
        for (i = 0; i < Rows * Cols; i++)
        {
            // Loop through the panel pixels
            for (y = panels[i].Y; y < PanelSize + panels[i].Y; y += GridSize)
            {
                for (x = panels[i].X; x < PanelSize + panels[i].X; x += GridSize)
                {
                    FRect grid = new() { X = x, Y = y, H = GridSize, W = GridSize };
                    bool dark = (int)(x / GridSize + y / GridSize) % 2 == 1 ? true : false;

                    if (dark) SetRenderDrawColor(renderer, 70, 70, 70, 255);    // Darker color
                    else SetRenderDrawColor(renderer, 110, 110, 110, 255); // Lighter color

                    RenderFillRect(renderer, in grid);
                }
            }

            // Label the blend mode
            SetRenderDrawColor(renderer, 255, 255, 255, byte.MaxValue);
            RenderDebugText(renderer, panels[i].X, panels[i].Y - 15, blendModeNames[i]);
        }

        // Render panels
        RenderRects(renderer, panels, Rows * Cols);

        // Render UI text
        RenderDebugText(renderer, (WindowWidth - 176) / 2, WindowHeight - 30, "UP/DOWN: CHANGE ALPHA");
        RenderDebugTextFormat(renderer, (WindowWidth - 80) / 2, WindowHeight - 20, $"ALPHA: {alpha}");

        // Update textures alpha mod
        SetTextureAlphaMod(redRectTexture, alpha);
        SetTextureAlphaMod(greenRectTexture, alpha);
        SetTextureAlphaMod(blueRectTexture, alpha);

        // Render panels
        for (i = 0; i < Rows * Cols; i++)
        {
            //+ Update rects destination
            FRect redDst = new() { X = panels[i].X + RedOffset, Y = panels[i].Y + RedOffset, W = RectSize, H = RectSize };
            FRect greenDst = new() { X = panels[i].X + GreenOffset, Y = panels[i].Y + GreenOffset, W = RectSize, H = RectSize };
            FRect blueDst = new() { X = panels[i].X + BlueOffset, Y = panels[i].Y + BlueOffset, W = RectSize, H = RectSize };

            // Apply the current blend mode
            bool supported = SetTextureBlendMode(redRectTexture, blendModes[i]);  // just make sure the renderer supports this blend mode
            SetTextureBlendMode(greenRectTexture, blendModes[i]);
            SetTextureBlendMode(blueRectTexture, blendModes[i]);

            // Render textures
            RenderTexture(renderer, redRectTexture, IntPtr.Zero, in redDst);
            RenderTexture(renderer, greenRectTexture, IntPtr.Zero, in greenDst);
            RenderTexture(renderer, blueRectTexture, IntPtr.Zero, in blueDst);

            // Not all renderers support all blend modes. The renderer will try to pick something close in this case,
            // but it should be noted that the results might be unexpected, so we add "[UNSUPPORTED]" to this panel.
            if (!supported)
            {
                float textwidth = 104.0f;
                FRect dst = new() { X = panels[i].X + ((panels[i].W - textwidth) / 2.0f), Y = panels[i].Y + (panels[i].H - 8), W = textwidth, H = 8 };
                SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);
                RenderFillRect(renderer, in dst);
                SetRenderDrawColor(renderer, 255, 255, 255, byte.MaxValue);
                RenderDebugText(renderer, dst.X, dst.Y, "[UNSUPPORTED]");
            }
        }

        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        DestroyTexture(redRectTexture);
        DestroyTexture(greenRectTexture);
        DestroyTexture(blueRectTexture);
        // SDL will clean up the window/renderer for us.
    }
}