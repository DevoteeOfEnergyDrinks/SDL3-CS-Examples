/*
 * This example code reads frames from a camera and draws it to the screen.
 *
 * This is a very simple approach that is often Good Enough. You can get
 * fancier with this: multiple cameras, front/back facing cameras on phones,
 * color spaces, choosing formats and framerates...this just requests
 * _anything_ and goes with what it is handed.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */
using System.Runtime.InteropServices;

internal class Program
{
    #region Main
    // These delegates map our C# methods to the internal SDL3 lifecycle events.
    private static readonly AppInitFunc _init = new(AppInit);
    private static readonly AppIterateFunc _iterate = new(AppIterate);
    private static readonly AppEventFunc _event = new(AppEvent);
    private static readonly AppQuitFunc _quit = new(AppQuit);


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
    #endregion


    #region Fields
    // We use IntPtr (Integer Pointers) because SDL3 is a C library.
    // These variables hold the memory addresses of the window and the renderer.
    public static IntPtr window = IntPtr.Zero;
    public static IntPtr renderer = IntPtr.Zero;
    public static IntPtr camera = IntPtr.Zero;
    public static IntPtr texture = IntPtr.Zero;
    #endregion


    #region AppInit
    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        uint[] devices;
        int devcount = 0;

        SetAppMetadata("Example Camera Read and Draw", "1.0", "com.example.camera-read-and-draw");

        if (!Init(InitFlags.Video | InitFlags.Camera))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/camera/read-and-draw", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        devices = GetCameras(out devcount)!;
        if (devices.Length == 0)
        {
            Log($"Couldn't enumerate camera devices: {GetError()}");
            return AppResult.Failure;
        }
        else if (devcount == 0)
        {
            Log("Couldn't find any camera devices! Please connect a camera and try again.");
            return AppResult.Failure;
        }

        camera = OpenCamera(devices[0], IntPtr.Zero);  // just take the first thing we see in any format it wants.
        if (camera == IntPtr.Zero)
        {
            Log($"Couldn't open camera: {GetError()}");
            return AppResult.Failure;
        }

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppEvent
    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        if (evt.Type == (uint)EventType.Quit)
        {
            return AppResult.Success;   // end the program, reporting success to the OS.
        }
        else if (evt.Type == (uint)EventType.CameraDeviceApproved)
        {
            Log("Camera use approved by user!");
        }
        else if (evt.Type == (uint)EventType.CameraDeviceDenied)
        {
            Log("Camera use denied by user!");
            return AppResult.Failure;
        }
        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppIterate
    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        ulong timestampNS = 0;
        IntPtr framePtr = AcquireCameraFrame(camera, out timestampNS);

        if (framePtr != IntPtr.Zero)
        {
            Surface frame = Marshal.PtrToStructure<Surface>(framePtr);

            // Some platforms (like Emscripten) don't know _what_ the camera offers
            // until the user gives permission, so we build the texture and resize
            // the window when we get a first frame from the camera.
            if (texture == IntPtr.Zero)
            {
                SetWindowSize(window, frame.Width, frame.Height);  // Resize the window to match */
                SetRenderLogicalPresentation(renderer, frame.Width, frame.Height, RendererLogicalPresentation.Letterbox);
                texture = CreateTexture(renderer, frame.Format, TextureAccess.Streaming, frame.Width, frame.Height);
            }

            if (texture != IntPtr.Zero)
            {
                UpdateTexture(texture, IntPtr.Zero, frame.Pixels, frame.Pitch);
            }
            Marshal.StructureToPtr<Surface>(frame, framePtr, true);
            ReleaseCameraFrame(camera, framePtr);
        }

        SetRenderDrawColor(renderer, 0x99, 0x99, 0x99, byte.MaxValue);
        RenderClear(renderer);
        if (texture != IntPtr.Zero)
        {   // draw the latest camera frame, if available.
            RenderTexture(renderer, texture, IntPtr.Zero, IntPtr.Zero);
        }
        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppQuit
    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        CloseCamera(camera);
        DestroyTexture(texture);
        // SDL will clean up the window/renderer for us.
    }
    #endregion
}