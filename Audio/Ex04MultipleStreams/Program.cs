/*
 * This example code loads two .wav files, puts them in audio streams and
 * binds them for playback, repeating both sounds on loop. This shows several
 * streams mixing into a single playback device.
 *
 * This code is public domain. Feel free to use it for any purpose!
 *
 * sample.wav from:
 * Main Theme (Overture) | The Grand Score by Alexander Nakarada | https://creatorchords.com/
 * Music promoted by https://www.chosic.com/free-music/all/
 * Attribution 4.0 International (CC BY 4.0)
 * https://creativecommons.org/licenses/by/4.0/
 *
 * sword.wav from:
 * https://kenney.nl/assets/rpg-audio
 */
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
    public static uint audioDevice = 0;

    public class Sound
    {
        public IntPtr WavData;
        public uint WavDataLen;
        public IntPtr Stream;
    }

    public static Sound[] sounds = new Sound[2];
    #endregion


    #region Methods
    static bool InitSound(string fname, Sound sound)
    {
        bool returnVal = false;
        AudioSpec spec;
        string wavPath;

        // Load the .wav files from wherever the app is being run from.
        wavPath = GetBasePath() + fname;
        if (!LoadWAV(wavPath, out spec, out sound.WavData, out sound.WavDataLen))
        {
            Log($"Couldn't load .wav file: {GetError()}");
            return false;
        }

        // Create an audio stream. Set the source format to the wav's format (what we'll input), 
        // leave the dest format NULL here (it'll change to what the device wants once we bind it).
        sound.Stream = CreateAudioStream(in spec, IntPtr.Zero);
        if (sound.Stream == IntPtr.Zero)
        {
            Log($"Couldn't create audio stream: {GetError()}");
        }
        else if (!BindAudioStream(audioDevice, sound.Stream))
        {   // once bound, it'll start playing when there is data available!
            Log($"Failed to bind '{fname}' stream to device: {GetError()}");
        }
        else
        {
            returnVal = true;  // success!
        }
        return returnVal;
    }
    #endregion


    #region AppInit
    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        SetAppMetadata("Example Audio Multiple Streams", "1.0", "com.example.audio-multiple-streams");

        if (!Init(InitFlags.Video | InitFlags.Audio))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        // we don't _need_ a window for audio-only things but it's good policy to have one.
        if (!CreateWindowAndRenderer("examples/audio/multiple-streams", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        SetRenderLogicalPresentation(renderer, 640, 480, RendererLogicalPresentation.Letterbox);

        // open the default audio device in whatever format it prefers; our audio streams will adjust to it.
        audioDevice = OpenAudioDevice(AudioDeviceDefaultPlayback, IntPtr.Zero);
        if (audioDevice == 0)
        {
            Log($"Couldn't open audio device: {GetError()}");
            return AppResult.Failure;
        }

        // Initialize each slot with a unique new object
        for (int i = 0; i < sounds.Length; i++)
        {
            sounds[i] = new Sound { WavData = IntPtr.Zero, WavDataLen = 0, Stream = IntPtr.Zero };
        }

        if (!InitSound("Assets/sample.wav", sounds[0]))
        {
            return AppResult.Failure;
        }
        else if (!InitSound("Assets/sword.wav", sounds[1]))
        {
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
        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppIterate
    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        int i;

        for (i = 0; i < sounds.Length; i++)
        {
            // If less than a full copy of the audio is queued for playback, put another copy in there.
            // This is overkill, but easy when lots of RAM is cheap. One could be more careful and
            // queue less at a time, as long as the stream doesn't run dry.
            if (GetAudioStreamQueued(sounds[i].Stream) < ((int)sounds[i].WavDataLen))
            {
                PutAudioStreamData(sounds[i].Stream, sounds[i].WavData, (int)sounds[i].WavDataLen);
            }
        }

        // just blank the screen.
        SetRenderDrawColor(renderer, 0, 0, 0, 255);
        RenderClear(renderer);
        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppQuit
    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        int i;

        CloseAudioDevice(audioDevice);

        for (i = 0; i < sounds.Length; i++)
        {
            if (sounds[i].Stream != IntPtr.Zero)
            {
                DestroyAudioStream(sounds[i].Stream);
            }
            Free(sounds[i].WavData);
        }
        // SDL will clean up the window/renderer for us.
    }
    #endregion
}