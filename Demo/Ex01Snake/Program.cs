/*
 * Logic implementation of the Snake game. It is designed to efficiently
 * represent the state of the game in memory.
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
    public const int StepRateInMilliSeconds = 125;
    public const int SnakeBlockSizeInPixels = 24;
    public const int WindowWidth = (int)(SnakeBlockSizeInPixels * SnakeGameWidth);
    public const int WindowHeight = (int)(SnakeBlockSizeInPixels * SnakeGameHeight);

    public const uint SnakeGameWidth = 24U;
    public const uint SnakeGameHeight = 18U;
    public const int SnakeMatrixSize = (int)(SnakeGameWidth * SnakeGameHeight);

    public const int SnakeCellMaxBits = 3; // floor(log2(SNAKE_CELL_FOOD)) + 1
    public const int SnakeCellSetBits = (1 << SnakeCellMaxBits) - 1;

    public static IntPtr joystick = IntPtr.Zero;

    public enum SnakeCell
    {
        Nothing = 0,
        SRight = 1,
        SUp = 2,
        SLeft = 3,
        SDown = 4,
        Food = 5
    }

    public enum SnakeDirection
    {
        Right,
        Up,
        Left,
        Down,
    }

    public static ExtendedMetadata[] extendedMetadata =
    [
        new() { Key = Props.AppMetadataURLString,         Value = "https://examples.libsdl.org/SDL3/demo/01-snake/" },
        new() { Key = Props.AppMetadataCreatorString,     Value = "SDL team" },
        new() { Key = Props.AppMetadataCopyrightString,   Value = "Placed in the public domain" },
        new() { Key = Props.AppMetadataTypeString,        Value = "game" }
    ];
    #endregion


    #region Structs/Classes
    public struct SnakeContext
    {
        public SnakeContext() { }
        public byte[] Cells = new byte[(SnakeMatrixSize * SnakeCellMaxBits) / 8U];
        public sbyte HeadXPos;
        public sbyte HeadYPos;
        public sbyte TailXPos;
        public sbyte TailYPos;
        public sbyte NextDir;
        public sbyte InhibitTailStep;
        public uint OccupiedCells;
    }


    public class AppState
    {
        public IntPtr Window;
        public IntPtr Renderer;
        public SnakeContext SnakeContext;
        public ulong LastStep;

        public static AppState GetAppState(IntPtr appstate)
        {
            return (AppState)GCHandle.FromIntPtr(appstate).Target!;
        }
    }


    public struct ExtendedMetadata
    {
        public string Key;
        public string Value;
    }
    #endregion


    #region Methods
    public static int Shift(int x, int y)
    {
        return (int)((x + (y * SnakeGameWidth)) * SnakeCellMaxBits);
    }


    public static SnakeCell SnakeCellAt(ref SnakeContext context, byte x, byte y)
    {
        int shift = Shift(x, y);
        int index = shift / 8;
        int offset = shift % 8;

        // Read up to two bytes (little-endian)
        ushort range = context.Cells[index];

        if (index + 1 < context.Cells.Length)
        {
            range |= (ushort)(context.Cells[index + 1] << 8);
        }

        return (SnakeCell)((range >> offset) & SnakeCellSetBits);
    }


    public static void SetRectXY(ref FRect rect, short x, short y)
    {
        rect.X = (float)(x * SnakeBlockSizeInPixels);
        rect.Y = (float)(y * SnakeBlockSizeInPixels);
    }


    public static void PutCellAt(ref SnakeContext context, byte x, byte y, SnakeCell cell)
    {
        int shift = Shift(x, y);
        int index = shift / 8;
        int offset = shift % 8;

        // Read up to two bytes (little-endian)
        ushort range = context.Cells[index];

        if (index + 1 < context.Cells.Length)
        {
            range |= (ushort)(context.Cells[index + 1] << 8);
        }

        // Clear the existing 3-bit value
        range &= (ushort)~(SnakeCellSetBits << offset);

        // Insert the new value
        range |= (ushort)(((int)cell & SnakeCellSetBits) << offset);

        // Write the low byte back
        context.Cells[index] = (byte)(range & 0xFF);

        // Write the high byte back if it exists
        if (index + 1 < context.Cells.Length)
        {
            context.Cells[index + 1] = (byte)(range >> 8);
        }
    }


    static bool AreCellsFull(SnakeContext context)
    {
        return context.OccupiedCells == (SnakeGameWidth * SnakeGameHeight);
    }


    public static void NewFoodPos(ref SnakeContext context)
    {
        while (true)
        {
            byte x = (byte)Rand((int)SnakeGameWidth);
            byte y = (byte)Rand((int)SnakeGameHeight);
            if (SnakeCellAt(ref context, x, y) == SnakeCell.Nothing)
            {
                PutCellAt(ref context, x, y, SnakeCell.Food);
                break;
            }
        }
    }


    public static void SnakeInitialize(ref SnakeContext context)
    {
        int i;
        Array.Clear(context.Cells);

        context.HeadXPos = context.TailXPos = (byte)SnakeGameWidth / 2;
        context.HeadYPos = context.TailYPos = (byte)SnakeGameHeight / 2;
        context.NextDir = (sbyte)SnakeDirection.Right;
        context.InhibitTailStep = 4;
        context.OccupiedCells = 4;
        --context.OccupiedCells;
        PutCellAt(ref context, (byte)context.TailXPos, (byte)context.TailYPos, SnakeCell.SRight);
        for (i = 0; i < 4; i++)
        {
            NewFoodPos(ref context);
            context.OccupiedCells++;
        }
    }


    public static void SnakeRedir(ref SnakeContext context, SnakeDirection direction)
    {
        SnakeCell ct = SnakeCellAt(ref context, (byte)context.HeadXPos, (byte)context.HeadYPos);

        if ((direction == SnakeDirection.Right && ct != SnakeCell.SLeft) ||
            (direction == SnakeDirection.Up && ct != SnakeCell.SDown) ||
            (direction == SnakeDirection.Left && ct != SnakeCell.SRight) ||
            (direction == SnakeDirection.Down && ct != SnakeCell.SUp))
        {
            context.NextDir = (sbyte)direction;
        }
    }


    public static void WrapAround(ref sbyte value, sbyte max)
    {
        if (value < 0)
        {
            value = (sbyte)(max - 1);
        }
        else if (value > max - 1)
        {
            value = 0;
        }
    }


    public static void SnakeStep(ref SnakeContext context)
    {
        SnakeCell DirectionAsCell = (SnakeCell)(context.NextDir + 1);
        SnakeCell ct;
        byte previousXPos;
        byte previousYPos;
        // Move tail forward
        if (--context.InhibitTailStep == 0)
        {
            ++context.InhibitTailStep;
            ct = SnakeCellAt(ref context, (byte)context.TailXPos, (byte)context.TailYPos);
            PutCellAt(ref context, (byte)context.TailXPos, (byte)context.TailYPos, SnakeCell.Nothing);

            switch (ct)
            {
                case SnakeCell.SRight:
                    context.TailXPos++;
                    break;
                case SnakeCell.SUp:
                    context.TailYPos--;
                    break;
                case SnakeCell.SLeft:
                    context.TailXPos--;
                    break;
                case SnakeCell.SDown:
                    context.TailYPos++;
                    break;
                default:
                    break;
            }
            WrapAround(ref context.TailXPos, (sbyte)SnakeGameWidth);
            WrapAround(ref context.TailYPos, (sbyte)SnakeGameHeight);
        }
        // Move head forward
        previousXPos = (byte)context.HeadXPos;
        previousYPos = (byte)context.HeadYPos;
        switch (context.NextDir)
        {
            case (sbyte)SnakeDirection.Right:
                ++context.HeadXPos;
                break;
            case (sbyte)SnakeDirection.Up:
                --context.HeadYPos;
                break;
            case (sbyte)SnakeDirection.Left:
                --context.HeadXPos;
                break;
            case (sbyte)SnakeDirection.Down:
                ++context.HeadYPos;
                break;
            default:
                break;
        }
        WrapAround(ref context.HeadXPos, (sbyte)SnakeGameWidth);
        WrapAround(ref context.HeadYPos, (sbyte)SnakeGameHeight);
        // Collisions
        ct = SnakeCellAt(ref context, (byte)context.HeadXPos, (byte)context.HeadYPos);
        if (ct != SnakeCell.Nothing && ct != SnakeCell.Food)
        {
            SnakeInitialize(ref context);
            return;
        }
        PutCellAt(ref context, previousXPos, previousYPos, DirectionAsCell);
        PutCellAt(ref context, (byte)context.HeadXPos, (byte)context.HeadYPos, DirectionAsCell);
        if (ct == SnakeCell.Food)
        {
            if (AreCellsFull(context))
            {
                SnakeInitialize(ref context);
                return;
            }
            NewFoodPos(ref context);
            ++context.InhibitTailStep;
            ++context.OccupiedCells;
        }
    }


    static AppResult HandleKeyEvent(ref SnakeContext context, Scancode keyCode)
    {
        switch (keyCode)
        {
            // Quit.
            case Scancode.Escape:
            case Scancode.Q:
                return AppResult.Success;
            // Restart the game as if the program was launched.
            case Scancode.R:
                SnakeInitialize(ref context);
                break;
            // Decide new direction of the snake.
            case Scancode.Right:
                SnakeRedir(ref context, SnakeDirection.Right);
                break;
            case Scancode.Up:
                SnakeRedir(ref context, SnakeDirection.Up);
                break;
            case Scancode.Left:
                SnakeRedir(ref context, SnakeDirection.Left);
                break;
            case Scancode.Down:
                SnakeRedir(ref context, SnakeDirection.Down);
                break;
            default:
                break;
        }
        return AppResult.Continue;
    }


    static AppResult HandleHatEvent(ref SnakeContext context, byte hat)
    {
        switch (hat)
        {
            case (byte)JoystickHat.Right:
                SnakeRedir(ref context, SnakeDirection.Right);
                break;
            case (byte)JoystickHat.Up:
                SnakeRedir(ref context, SnakeDirection.Up);
                break;
            case (byte)JoystickHat.Left:
                SnakeRedir(ref context, SnakeDirection.Left);
                break;
            case (byte)JoystickHat.Down:
                SnakeRedir(ref context, SnakeDirection.Down);
                break;
            default:
                break;
        }
        return AppResult.Continue;
    }
    #endregion


    #region AppInit
    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        AppState state = new();
        appstate = GCHandle.ToIntPtr(GCHandle.Alloc(state));

        uint i;

        if (!SetAppMetadata("Example Snake game", "1.0", "com.example.Snake"))
        {
            return AppResult.Failure;
        }

        for (i = 0; i < extendedMetadata.Length; i++)
        {
            if (!SetAppMetadataProperty(extendedMetadata[i].Key, extendedMetadata[i].Value))
            {
                return AppResult.Failure;
            }
        }

        if (!Init(InitFlags.Video | InitFlags.Joystick))
        {
            Log($"Couldn't initialize SDL: {GetError()}");
            return AppResult.Failure;
        }

        if (!CreateWindowAndRenderer("examples/demo/snake", WindowWidth, WindowHeight, WindowFlags.Resizable, out state.Window, out state.Renderer))
        {
            return AppResult.Failure;
        }
        SetRenderLogicalPresentation(state.Renderer, WindowWidth, WindowHeight, RendererLogicalPresentation.Letterbox);

        state.SnakeContext = new();

        SnakeInitialize(ref state.SnakeContext);

        state.LastStep = GetTicks();

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppEvent
    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        AppState state = AppState.GetAppState(appstate);
        switch (evt.Type)
        {
            case (uint)EventType.Quit:
                return AppResult.Success;
            case (uint)EventType.JoystickAdded:
                if (joystick == IntPtr.Zero)
                {
                    joystick = OpenJoystick(evt.JDevice.Which);
                    if (joystick == IntPtr.Zero)
                    {
                        Log($"Failed to open joystick ID {evt.JDevice.Which}: {GetError()}");
                    }
                }
                break;
            case (uint)EventType.JoystickRemoved:
                if (joystick != IntPtr.Zero && (GetJoystickID(joystick) == evt.JDevice.Which))
                {
                    CloseJoystick(joystick);
                    joystick = IntPtr.Zero;
                }
                break;
            case (uint)EventType.JoystickHatMotion:
                return HandleHatEvent(ref state.SnakeContext, evt.JHat.Value);
            case (uint)EventType.KeyDown:
                return HandleKeyEvent(ref state.SnakeContext, evt.Key.Scancode);
            default:
                break;
        }
        return AppResult.Continue;
    }
    #endregion


    #region AppIterate
    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        AppState state = AppState.GetAppState(appstate);

        Delay(6);

        ulong now = GetTicks();
        FRect rect = new();
        int i;
        int j;
        int cell;

        // run game logic if we're at or past the time to run it.
        // if we're _really_ behind the time to run it, run it several times.
        while ((now - state.LastStep) >= StepRateInMilliSeconds)
        {
            SnakeStep(ref state.SnakeContext);
            state.LastStep += StepRateInMilliSeconds;
        }

        rect.W = rect.H = SnakeBlockSizeInPixels;
        SetRenderDrawColor(state.Renderer, 0, 0, 0, byte.MaxValue);
        RenderClear(state.Renderer);
        for (i = 0; i < SnakeGameWidth; i++)
        {
            for (j = 0; j < SnakeGameHeight; j++)
            {
                cell = (int)SnakeCellAt(ref state.SnakeContext, (byte)i, (byte)j);
                if (cell == (int)SnakeCell.Nothing)
                {
                    continue;
                }
                SetRectXY(ref rect, (short)i, (short)j);
                if (cell == (int)SnakeCell.Food)
                {
                    SetRenderDrawColor(state.Renderer, 80, 80, 255, byte.MaxValue);
                }
                else // body
                {
                    SetRenderDrawColor(state.Renderer, 0, 128, 0, byte.MaxValue);
                }
                RenderFillRect(state.Renderer, in rect);
            }
        }
        SetRenderDrawColor(state.Renderer, 255, 255, 0, byte.MaxValue); //head
        SetRectXY(ref rect, state.SnakeContext.HeadXPos, state.SnakeContext.HeadYPos);
        RenderFillRect(state.Renderer, in rect);
        RenderPresent(state.Renderer);
        return AppResult.Continue;
    }
    #endregion


    #region AppQuit
    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        if (appstate != IntPtr.Zero)
        {
            GCHandle handle = GCHandle.FromIntPtr(appstate);
            AppState gameState = (AppState)handle.Target!;

            DestroyRenderer(gameState.Renderer);
            DestroyWindow(gameState.Window);

            handle.Free();
        }
        if (joystick != IntPtr.Zero)
        {
            CloseJoystick(joystick);
        }
    }
    #endregion
}