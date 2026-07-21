/*
 * This example code creates a simple audio stream for playing sound, and
 * generates a sine wave sound effect for it to play as time goes on. Unlike
 * the previous example, this uses a callback to generate sound.
 *
 * This might be the path of least resistance if you're moving an SDL2
 * program's audio code to SDL3.
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
    public static IntPtr stream = IntPtr.Zero;
    public static int currentSineSample = 0;

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


    // this function will be called (usually in a background thread) when the audio stream is consuming data.
    static void FeedTheAudioStreamMore(IntPtr userdata, IntPtr astream, int additionalAmount, int totalAmount)
    {
        // total_amount is how much data the audio stream is eating right now, additional_amount is how much more it needs
        // than what it currently has queued (which might be zero!). You can supply any amount of data here; it will take what
        // it needs and use the extra later. If you don't give it enough, it will take everything and then feed silence to the
        // hardware for the rest. Ideally, though, we always give it what it needs and no extra, so we aren't buffering more
        // than necessary. */
        additionalAmount /= sizeof(float);  // convert from bytes to samples
        while (additionalAmount > 0)
        {
            float[] samples = new float[128];  // this will feed 128 samples each iteration until we have enough. */
            int total = (int)MathF.Min(additionalAmount, samples.Length);
            int i;

            // generate a 440Hz pure tone
            for (i = 0; i < total; i++)
            {
                int freq = 440;
                float phase = currentSineSample * freq / 8000.0f;
                samples[i] = MathF.Sin(phase * 2 * MathF.PI);
                currentSineSample++;
            }

            // wrapping around to avoid floating-point errors */
            currentSineSample %= 8000;

            // feed the new data to the stream. It will queue at the end, and trickle out as the hardware needs more data.
            Span<byte> bytes = MemoryMarshal.AsBytes<float>(samples.AsSpan());
            PutAudioStreamData(astream, bytes, total * sizeof(float));
            additionalAmount -= total;  // subtract what we've just fed the stream. */
        }
    }


    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        AudioSpec spec;

        SetAppMetadata("Example Simple Audio Playback Callback", "1.0", "com.example.audio-simple-playback-callback");

        if (!Init(InitFlags.Video | InitFlags.Audio))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        // we don't _need_ a window for audio-only things but it's good policy to have one.
        if (!CreateWindowAndRenderer("examples/audio/simple-playback-callback", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        SetRenderLogicalPresentation(renderer, 640, 480, RendererLogicalPresentation.Letterbox);

        // We're just playing a single thing here, so we'll use the simplified option.
        // We are always going to feed audio in as mono, float32 data at 8000Hz.
        // The stream will convert it to whatever the hardware wants on the other side.
        spec.Channels = 1;
        // A Check for the endiness of our system
        spec.Format = BitConverter.IsLittleEndian ? AudioFormat.AudioF32LE : AudioFormat.AudioF32BE;
        spec.Freq = 8000;
        stream = OpenAudioDeviceStream(AudioDeviceDefaultPlayback, in spec, FeedTheAudioStreamMore, IntPtr.Zero);
        if (stream == IntPtr.Zero)
        {
            Log($"Couldn't create audio stream: {GetError()}");
            return AppResult.Failure;
        }

        // SDL_OpenAudioDeviceStream starts the device paused. You have to tell it to start!
        ResumeAudioStreamDevice(stream);
        

        // Inside your loop or method
        Console.WriteLine($"Thread ID: {Environment.ProcessId} | Managed ID: {Thread.CurrentThread.ManagedThreadId}");
        Console.WriteLine(GetCurrentThreadID());

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
        // we're not doing anything with the renderer, so just blank it out.
        RenderClear(renderer);
        RenderPresent(renderer);

        // all the work of feeding the audio stream is happening in a callback in a background thread.

        return AppResult.Continue;  // carry on with the program!
    }


    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
}