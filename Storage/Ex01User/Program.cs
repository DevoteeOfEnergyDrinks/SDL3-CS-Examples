/*
 * This example code creates an SDL window and renderer, and waits for the user
 * to click on the window. By default, the window color is blue; When storage
 * succeeds, the window turns green, if it fails the window turns red and the
 * error message is logged. Left clicking will save a game, all other clicks
 * will load a game.
 *
 * The primary goal is to show how to handle save data without blocking the main
 * thread, while also making sure to keep the storage handle open for as little
 * as possible; many platforms do not allow keeping user storage open for long
 * periods of time so you _must_ be sure to only have user storage open when you
 * are absolutely 100% ready to interact with the storage handle.
 *
 * This code is public domain. Feel free to use it for any purpose!
 */
using System.Diagnostics;
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

    // This is the list of steps that will occur as part of saving or loading a game
    enum SaveState
    {
        Unstarted, // blue
        ProcessingGameWorld, // yellow
        PreparingStorage, // cyan
        ProcessingStorageFile, // magenta
        FinalCheck, // green if succes, red if failed
    }

    public static AtomicInt currentSaveState;
    // During the final check, this indicates success or failure
    public static int saveResult = -1;

    // This is the thread that handles the majority of save operations
    public static IntPtr saveThread = IntPtr.Zero;

    // Opening storage is itself an async operation, so the thread will have some waiting to do
    public static IntPtr storageReady = IntPtr.Zero;

    // This is the handle for the user's filesystem
    public static IntPtr saveStorage = IntPtr.Zero;

    public const string SaveFileName = "save.sav";
    #endregion


    #region Methods
    // This function pretends to serialize a fictional game world, then starts
    // opening the filesystem to write the serialized data
    static int WriteSaveData(IntPtr data)
    {
        ulong gameWorld; // to keep things simple, let's just pretend that an entire game fits in 64-bits
        bool writeResult;

        SetAtomicInt(ref currentSaveState, (int)SaveState.ProcessingGameWorld);

        // again, let's just pretend that an entire game fits in 64-bits
        gameWorld = GetPerformanceCounter();

        // now that save data is ready to go, we can start opening the filesystem
        saveStorage = OpenUserStorage("libsdl", "User Storage Example", 0);
        if (saveStorage == IntPtr.Zero)
        {
            SetAtomicInt(ref currentSaveState, (int)SaveState.FinalCheck);
            return -1;
        }
        SetAtomicInt(ref currentSaveState, (int)SaveState.PreparingStorage);

        // the main thread will eventually signal to us that storage is ready
        WaitSemaphore(storageReady);

        // the save data can now be written to the storage device
        IntPtr gameWorldPtr = Marshal.AllocHGlobal(sizeof(ulong));
        Marshal.WriteInt64(gameWorldPtr, (long)gameWorld);

        writeResult = WriteStorageFile(saveStorage, SaveFileName, gameWorldPtr, sizeof(ulong));

        Marshal.FreeHGlobal(gameWorldPtr);

        // regardless of what happened above, we've reached the end of the routine
        CloseStorage(saveStorage);

        SetAtomicInt(ref currentSaveState, (int)SaveState.FinalCheck);

        if (!writeResult)
        {
            return -1;
        }
        return 0;
    }


    // This function opens the filesystem to read a save file, then deserializes the
    // data into fictional game world data
    static int ReadSaveData(IntPtr data)
    {
        ulong gameWorld = new(); // to keep things simple, let's just pretend that an entire game fits in 64-bits
        ulong saveLen;
        bool readResult;

        // start by preparing the filesystem for reading
        saveStorage = OpenUserStorage("libsdl", "User Storage Example", 0);
        if (saveStorage == IntPtr.Zero)
        {
            SetAtomicInt(ref currentSaveState, (int)SaveState.FinalCheck);
            return -1;
        }
        SetAtomicInt(ref currentSaveState, (int)SaveState.PreparingStorage);

        // the main thread will eventually signal to us that storage is ready
        WaitSemaphore(storageReady);

        readResult = GetStorageFileSize(saveStorage, SaveFileName, out saveLen);
        if (!readResult)
        {
            CloseStorage(saveStorage);
            SetAtomicInt(ref currentSaveState, (int)SaveState.FinalCheck);
            Log("Save data was not found");
            return -1;
        }
        else if (saveLen != sizeof(ulong))
        {
            CloseStorage(saveStorage);
            SetAtomicInt(ref currentSaveState, (int)SaveState.FinalCheck);
            Log("Save data size is incorrect, was the file corrupted?");
            return -1;
        }

        // once we've read the file in, the storage handle is no longer needed
        IntPtr gameWorldPtr = Marshal.AllocHGlobal(sizeof(ulong));
        Marshal.WriteInt64(gameWorldPtr, (long)gameWorld);
        readResult = ReadStorageFile(saveStorage, SaveFileName, gameWorldPtr, sizeof(ulong));

        CloseStorage(saveStorage);

        SetAtomicInt(ref currentSaveState, (int)SaveState.ProcessingGameWorld);

        if (readResult)
        {
            // again, let's just pretend that an entire game fits in 64-bits
            Log($"Game World loaded, value was {(ulong)Marshal.ReadInt64(gameWorldPtr)}");
        }
        Marshal.FreeHGlobal(gameWorldPtr);
        // regardless of what happened above, we've reached the end of the routine
        SetAtomicInt(ref currentSaveState, (int)SaveState.FinalCheck);

        if (!readResult)
        {
            return -1;
        }
        return 0;
    }
    #endregion


    #region AppInit
    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        SetAppMetadata("User Storage Example", "1.0", "com.example.storage-user");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/storage/user", 640, 480, 0, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }

        // initialize the default save state
        SetAtomicInt(ref currentSaveState, (int)SaveState.Unstarted);
        storageReady = CreateSemaphore(0);

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
        else if (evt.Type == (uint)EventType.MouseButtonDown)
        {
            if (saveThread != IntPtr.Zero)
            {
                Log("Ignoring interaction, save/load is in progress");
            }
            else
            {
                // once the thread starts, it will update this to the first "real" state
                SetAtomicInt(ref currentSaveState, (int)SaveState.Unstarted);
                if (evt.Button.Button == 1)
                {
                    saveThread = CreateThread(WriteSaveData, "Save Write Thread", IntPtr.Zero);
                }
                else
                {
                    saveThread = CreateThread(ReadSaveData, "Save Read Thread", IntPtr.Zero);
                }
            }
        }
        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppIterate
    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        Delay(6);
        float red, green, blue;
        int saveState = GetAtomicInt(ref currentSaveState);

        // the main thread does not have to do much other than help the thread wait
        // for storage to be ready and read the result when the thread is finished
        if (saveState == (int)SaveState.PreparingStorage)
        {
            if (StorageReady(saveStorage))
            {
                SetAtomicInt(ref currentSaveState, (int)SaveState.ProcessingStorageFile);
                SignalSemaphore(storageReady);
            }
        }
        else if (saveState == (int)SaveState.FinalCheck)
        {
            if (saveThread != IntPtr.Zero)
            {
                WaitThread(saveThread, out saveResult);
                saveThread = IntPtr.Zero;
                if (saveResult == 0)
                {
                    Log("Save/Load complete!");
                }
                else
                {
                    Log($"Save/Load failed: {GetError()}");
                }
            }
        }

        // set the draw color based on the state of the save system
        switch (saveState)
        {
            case (int)SaveState.Unstarted:
                red = 0.0f;
                green = 0.0f;
                blue = 1.0f;
                break;
            case (int)SaveState.ProcessingGameWorld:
                red = 1.0f;
                green = 1.0f;
                blue = 0.0f;
                break;
            case (int)SaveState.PreparingStorage:
                red = 0.0f;
                green = 1.0f;
                blue = 1.0f;
                break;
            case (int)SaveState.ProcessingStorageFile:
                red = 1.0f;
                green = 0.0f;
                blue = 1.0f;
                break;
            case (int)SaveState.FinalCheck:
                if (saveResult == 0)
                {
                    red = 0.0f;
                    green = 1.0f;
                }
                else
                {
                    red = 1.0f;
                    green = 0.0f;
                }
                blue = 0.0f;
                break;
            default:
                red = 0.0f;
                green = 0.0f;
                blue = 0.0f;
                Debug.Fail("Unrecognized save state");
                break;
        }
        SetRenderDrawColorFloat(renderer, red, green, blue, byte.MaxValue);  // new color, full alpha.

        // clear the window to the draw color.
        RenderClear(renderer);

        // put the newly-cleared rendering on the screen.
        RenderPresent(renderer);

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppQuit
    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // If saving/loading is still in progress, force the thread not to wait */
        SignalSemaphore(storageReady);
        WaitThread(saveThread, out int value);
        DestroySemaphore(storageReady);
        // SDL will clean up the window/renderer for us.
    }
    #endregion
}