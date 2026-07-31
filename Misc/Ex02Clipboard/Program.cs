/*
 * This example code lets the user copy and paste with the system clipboard.
 *
 * This only handles text, but SDL supports other data types, too.
 *
 * This code is public domain. Feel free to use it for any purpose!
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
    public static string copyButtonString = "Click here to copy!";
    public static string pasteButtonString = "Click here to paste!";
    public static FRect currentTimeRect;
    public static FRect copyButtonRect;
    public static FRect pasteTextRect;
    public static FRect pasteButtonRect;
    public static bool copyPressed = false;
    public static bool pastePressed = false;
    public static string? currentTime;
    public static string? pastedString;
    #endregion


    #region Methods
    static void CalculateCurrentTimeString()
    {
        long ticks = 0;
        SDL3.SDL.DateTime dateTime;
        if (!GetCurrentTime(out ticks) || !TimeToDateTime(ticks, out dateTime, true))
        {
            currentTime = "(Don't know the current time, sorry.)";
        }
        else
        {
            string[] month = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
            string[] day = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

            currentTime = $"{day[dateTime.DayOfWeek]}, {month[dateTime.Month - 1]} {dateTime.Day}, {dateTime.Year}  {dateTime.Hour:00}:{dateTime.Minute:00}:{dateTime.Second:00}";
        }
    }

    static void RenderPastedText()
    {
        if (string.IsNullOrEmpty(pastedString))
            return;

        float x = pasteTextRect.X + 5;
        float y = pasteTextRect.Y + 5;
        float w = pasteTextRect.W - 10;
        float h = pasteTextRect.H;

        int maxCharacterPerLine = (int)(w / DebugTextFontCharacterSize);

        // Split on both Windows and Unix newlines.
        string[] lines = pastedString.Replace("\r\n", "\n").Split('\n');

        foreach (string line in lines)
        {
            // No room for another line?
            if ((h - y) < DebugTextFontCharacterSize)
                break;

            string text = line;

            // Match the C example by simply clipping the line.
            if (text.Length > maxCharacterPerLine)
                text = text[..maxCharacterPerLine];

            RenderDebugText(renderer, x, y, text);

            y += DebugTextFontCharacterSize + 2;
        }
    }
    #endregion


    #region AppInit
    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        SetAppMetadata("Example Misc Clipboard", "1.0", "com.example.misc-clipboard");

        if (!Init(InitFlags.Video))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/misc/clipboard", 640, 480, WindowFlags.Resizable, out window, out renderer))
        {
            Log($"Couldn't create window/renderer: {GetError()}");
            return AppResult.Failure;
        }
        SetRenderLogicalPresentation(renderer, 640, 480, RendererLogicalPresentation.Letterbox);

        CalculateCurrentTimeString();

        // set up the locations where we'll draw stuff.
        currentTimeRect.X = 30;
        currentTimeRect.Y = 10;
        currentTimeRect.W = 390;
        currentTimeRect.H = DebugTextFontCharacterSize + 10;

        copyButtonRect.X = currentTimeRect.X + currentTimeRect.W + 30;
        copyButtonRect.Y = currentTimeRect.Y;
        copyButtonRect.W = (float)((DebugTextFontCharacterSize * copyButtonString.Length) + 10);
        copyButtonRect.H = currentTimeRect.H;

        pasteTextRect.X = 10;
        pasteTextRect.Y = currentTimeRect.Y + currentTimeRect.H + 10;
        pasteTextRect.W = 620;
        pasteTextRect.H = ((480 - pasteTextRect.Y) - copyButtonRect.H) - 20;

        pasteButtonRect.W = (float)((DebugTextFontCharacterSize * (pasteButtonString.Length)) + 10);
        pasteButtonRect.X = (640 - pasteButtonRect.W) / 2.0f;
        pasteButtonRect.Y = pasteTextRect.Y + pasteTextRect.H + 10;
        pasteButtonRect.H = copyButtonRect.H;

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppEvent
    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        ConvertEventToRenderCoordinates(renderer, ref evt);
        if (evt.Type == (uint)EventType.Quit)
        {
            return AppResult.Success;   // end the program, reporting success to the OS.
        }
        else if (evt.Type == (uint)EventType.MouseButtonDown)
        {
            if (evt.Button.Button == ButtonLeft)
            {
                FPoint p = new() { X = evt.Button.X, Y = evt.Button.Y };
                copyPressed = PointInRectFloat(in p, in copyButtonRect);
                pastePressed = PointInRectFloat(in p, in pasteButtonRect);
            }
        }
        else if (evt.Type == (uint)EventType.MouseButtonUp)
        {
            if (evt.Button.Button == ButtonLeft)
            {
                FPoint p = new() { X = evt.Button.X, Y = evt.Button.Y };

                if (copyPressed && PointInRectFloat(in p, in copyButtonRect))
                {
                    SetClipboardText(currentTime!);
                }
                else if (pastePressed && PointInRectFloat(in p, in pasteButtonRect))
                {
                    pastedString = GetClipboardText();
                }
                copyPressed = pastePressed = false;
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
        float x, y;

        CalculateCurrentTimeString();

        SetRenderDrawColor(renderer, 0, 0, 0, 255);  // black
        RenderClear(renderer);

        // draw a frame around the current time.
        SetRenderDrawColor(renderer, 0, 0, 255, 255);
        RenderFillRect(renderer, in currentTimeRect);
        SetRenderDrawColor(renderer, 255, 255, 255, 255);
        RenderRect(renderer, in currentTimeRect);

        // draw the current time inside the frame.
        x = currentTimeRect.X + ((currentTimeRect.W - (DebugTextFontCharacterSize * currentTime!.Length)) / 2.0f);
        y = currentTimeRect.Y + 5;
        SetRenderDrawColor(renderer, 255, 255, 0, 255);
        RenderDebugText(renderer, x, y, currentTime);

        // draw a frame for the "copy the current time to the clipboard" button.
        if (copyPressed)
        {
            SetRenderDrawColor(renderer, 0, 255, 0, 255);
        }
        else
        {
            SetRenderDrawColor(renderer, 255, 0, 0, 255);
        }
        RenderFillRect(renderer, in copyButtonRect);
        SetRenderDrawColor(renderer, 255, 255, 255, 255);
        RenderRect(renderer, in copyButtonRect);

        // draw the "copy this text" button string.
        SetRenderDrawColor(renderer, 255, 255, 255, 255);
        RenderDebugText(renderer, copyButtonRect.X + 5, copyButtonRect.Y + 5, copyButtonString);

        // draw a frame for the pasted text area.
        SetRenderDrawColor(renderer, 0, 53, 25, 255);
        RenderFillRect(renderer, in pasteTextRect);
        SetRenderDrawColor(renderer, 255, 255, 255, 255);
        RenderRect(renderer, in pasteTextRect);

        // draw pasted text.
        SetRenderDrawColor(renderer, 0, 219, 107, 255);
        RenderPastedText();

        // draw a frame for the "paste from the clipboard" button.
        if (pastePressed)
        {
            SetRenderDrawColor(renderer, 0, 255, 0, 255);
        }
        else
        {
            SetRenderDrawColor(renderer, 255, 0, 0, 255);
        }
        RenderFillRect(renderer, in pasteButtonRect);
        SetRenderDrawColor(renderer, 255, 255, 255, 255);
        RenderRect(renderer, in pasteButtonRect);

        // draw the "paste some text" button string.
        SetRenderDrawColor(renderer, 255, 255, 255, 255);
        RenderDebugText(renderer, pasteButtonRect.X + 5, pasteButtonRect.Y + 5, pasteButtonString);

        // put the new rendering on the screen.
        RenderPresent(renderer);
        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppQuit
    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // SDL will clean up the window/renderer for us.
    }
    #endregion
}