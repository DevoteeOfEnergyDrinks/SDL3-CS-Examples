/*
 * This example code creates a simple audio stream for playing sound, and
 * loads a .wav file that is pushed through the stream in a loop.
 *
 * This code is public domain. Feel free to use it for any purpose!
 *
 * The .wav file is from this site https://mixkit.co/free-stock-music/
 *
 *      Song:   Tech House vibes
 *      by      Alejandro Magaña (A. M.)
 *      converted from mp3 to wav
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
    public static IntPtr stream = IntPtr.Zero;
    public static IntPtr wavData;
    public static uint wavDataLen = 0;

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
        AudioSpec spec;
        string wavPath;

        SetAppMetadata("Example Audio Load Wave", "1.0", "com.example.audio-load-wav");

        if (!Init(InitFlags.Video | InitFlags.Audio))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        // we don't _need_ a window for audio-only things but it's good policy to have one.
        if (!CreateWindowAndRenderer("examples/audio/load-wav", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        SetRenderLogicalPresentation(renderer, 640, 480, RendererLogicalPresentation.Letterbox);

        // Load the .wav file from wherever the app is being run from.
        wavPath = GetBasePath() + "Assets/sample.wav";  // build the string of the full file path

        if (!LoadWAV(wavPath, out spec, out wavData, out wavDataLen))
        {
            Log($"Couldn't load .wav file: {GetError()}");
            return AppResult.Failure;
        }

        // Create our audio stream in the same format as the .wav file. It'll convert to what the audio hardware wants.
        stream = OpenAudioDeviceStream(AudioDeviceDefaultPlayback, in spec, null, IntPtr.Zero);
        if (stream == IntPtr.Zero)
        {
            Log($"Couldn't create audio stream: {GetError()}");
            return AppResult.Failure;
        }

        // SDL_OpenAudioDeviceStream starts the device paused. You have to tell it to start!
        ResumeAudioStreamDevice(stream);

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
        // see if we need to feed the audio stream more data yet.
        // We're being lazy here, but if there's less than the entire wav file left to play,
        // just shove a whole copy of it into the queue, so we always have _tons_ of
        // data queued for playback.
        if (GetAudioStreamQueued(stream) < (int)wavDataLen)
        {
            // feed more data to the stream. It will queue at the end, and trickle out as the hardware needs more data.
            PutAudioStreamData(stream, wavData, (int)wavDataLen);
        }

        // we're not doing anything with the renderer, so just blank it out.
        RenderClear(renderer);
        RenderPresent(renderer);
        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        Free(wavData);  // strictly speaking, this isn't necessary because the process is ending, but it's good policy.
        // SDL will clean up the window/renderer for us.
    }
}