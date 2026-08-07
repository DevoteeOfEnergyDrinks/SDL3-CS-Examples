/*
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
    public const int MapBoxScale = 16;
    public const int MapBoxEdgesLength = (12 + MapBoxScale * 2);
    public const int MaxPlayerCount = 4;
    public const int CircleDrawSide = 32;
    public const int CircleDrawSidesLength = (CircleDrawSide + 1);

    public static ExtendedMetadata[] extendedMetadata =
    [
        new() { Key = Props.AppMetadataURLString,         Value = "https://examples.libsdl.org/SDL3/demo/02-woodeneye-008/" },
        new() { Key = Props.AppMetadataCreatorString,     Value = "SDL team" },
        new() { Key = Props.AppMetadataCopyrightString,   Value = "Placed in the public domain" },
        new() { Key = Props.AppMetadataTypeString,        Value = "game" }
    ];

    public static string DebugString = "";
    #endregion


    #region Structs/Classes
    public class AppState
    {
        public IntPtr Window;
        public IntPtr Renderer;
        public int PlayerCount;
        public Player[] Players = new Player[MaxPlayerCount];
        public float[,] Edges = new float[MapBoxEdgesLength, 6];

        public ulong FrameCount = 0;
        public ulong LastFpsUpdateTime = 0;
        public ulong PreviousFrameTime = 0;

        public static AppState GetAppState(IntPtr appstate)
        {
            return (AppState)GCHandle.FromIntPtr(appstate).Target!;
        }
    }


    public struct Player
    {
        public Player() { }

        public byte[] Color = new byte[3];

        public double[] Position = new double[3];
        public double[] Velocity = new double[3];

        public uint Mouse;
        public uint Keyboard;

        public uint Yaw;
        public int Pitch;
        public float Radius;
        public float Height;

        public byte Wasd;
    }


    public struct ExtendedMetadata
    {
        public string Key;
        public string Value;
    }
    #endregion


    #region Methods
    public static int WhoseMouse(uint mouse, Player[] players, int playersLength)
    {
        int i;
        for (i = 0; i < playersLength; i++)
        {
            if (players[i].Mouse == mouse)
            {
                return i;
            }
        }
        return -1;
    }


    public static int WhoseKeyboard(uint keyboard, Player[] players, int playersLength)
    {
        int i;
        for (i = 0; i < playersLength; i++)
        {
            if (players[i].Keyboard == keyboard)
            {
                return i;
            }
        }
        return -1;
    }


    public static void Shoot(int shooterIndex, Player[] players, int playersLength)
    {
        int targetIndex;
        int sphereIndex;
        double shooterPositionX = players[shooterIndex].Position[0];
        double shooterPositionY = players[shooterIndex].Position[1];
        double shooterPositionZ = players[shooterIndex].Position[2];

        double binaryAngleToRadians = Math.PI / 2147483648.0;
        double yawRadians = binaryAngleToRadians * players[shooterIndex].Yaw;
        double pitchRadians = binaryAngleToRadians * players[shooterIndex].Pitch;

        double cosYaw = Math.Cos(yawRadians);
        double sinYaw = Math.Sin(yawRadians);

        double cosPitch = Math.Cos(pitchRadians);
        double sinPitch = Math.Sin(pitchRadians);

        double shotDirectionX = -sinYaw * cosPitch;
        double shotDirectionY = sinPitch;
        double shotDirectionZ = -cosYaw * cosPitch;

        for (targetIndex = 0; targetIndex < playersLength; targetIndex++)
        {
            if (targetIndex == shooterIndex)
            {
                continue;
            }
            ref Player target = ref players[targetIndex];
            int hitCount = 0;
            for (sphereIndex = 0; sphereIndex < 2; sphereIndex++)
            {
                double targetRadius = target.Radius;
                double targetHeight = target.Height;
                double deltaX = target.Position[0] - shooterPositionX;
                double deltaY = target.Position[1] - shooterPositionY + (sphereIndex == 0 ? 0 : targetRadius - targetHeight);
                double deltaZ = target.Position[2] - shooterPositionZ;
                double rayProjection = shotDirectionX * deltaX + shotDirectionY * deltaY + shotDirectionZ * deltaZ;
                double targetDistanceSquared = deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ;
                double shotDirectionLengthSquared = shotDirectionX * shotDirectionX + shotDirectionY * shotDirectionY + shotDirectionZ * shotDirectionZ;
                double targetRadiusSquared = targetRadius * targetRadius;
                if (rayProjection < 0)
                {
                    continue;
                }
                if (rayProjection * rayProjection >= shotDirectionLengthSquared * (targetDistanceSquared - targetRadiusSquared))
                {
                    hitCount += 1;
                }
            }
            if (hitCount > 0)
            {
                target.Position[0] = (double)(MapBoxScale * (Rand(256) - 128)) / 256;
                target.Position[1] = (double)(MapBoxScale * (Rand(256) - 128)) / 256;
                target.Position[2] = (double)(MapBoxScale * (Rand(256) - 128)) / 256;
            }
        }
    }


    public static void Update(Player[] players, int playersLength, ulong deltaTimeNS)
    {
        int i;
        for (i = 0; i < playersLength; i++)
        {
            ref Player player = ref players[i];

            double velocityDampingRate = 6.0;
            double deltaTimeS = (double)deltaTimeNS * 1e-9;
            double dragFactor = Math.Exp(-deltaTimeS * velocityDampingRate);
            double dampingAmount = 1.0 - dragFactor;
            double movementAcceleration = 60.0;
            double gravity = 25.0;
            double yawAngleUnits = (double)player.Yaw;
            double yawRadians = yawAngleUnits * Math.PI / 2147483648.0;
            double cosYaw = Math.Cos(yawRadians);
            double sinYaw = Math.Sin(yawRadians);
            byte wasd = player.Wasd;

            double inputX = ((wasd & 8) != 0 ? 1.0 : 0.0) - ((wasd & 2) != 0 ? 1.0 : 0.0);
            double inputZ = ((wasd & 4) != 0 ? 1.0 : 0.0) - ((wasd & 1) != 0 ? 1.0 : 0.0);

            double movementLengthSquared = inputX * inputX + inputZ * inputZ;

            double accelerationX =
                movementAcceleration * (movementLengthSquared == 0 ? 0 : (cosYaw * inputX + sinYaw * inputZ) / Math.Sqrt(movementLengthSquared));

            double accelerationZ =
                movementAcceleration * (movementLengthSquared == 0 ? 0 : (-sinYaw * inputX + cosYaw * inputZ) / Math.Sqrt(movementLengthSquared));

            double previousVelocityX = player.Velocity[0];
            double previousVelocityY = player.Velocity[1];
            double previousVelocityZ = player.Velocity[2];

            player.Velocity[0] -= previousVelocityX * dampingAmount;
            player.Velocity[1] -= gravity * deltaTimeS;
            player.Velocity[2] -= previousVelocityZ * dampingAmount;
            player.Velocity[0] += dampingAmount * accelerationX / velocityDampingRate;
            player.Velocity[2] += dampingAmount * accelerationZ / velocityDampingRate;

            player.Position[0] +=
                (deltaTimeS - dampingAmount / velocityDampingRate) * accelerationX / velocityDampingRate + dampingAmount * previousVelocityX / velocityDampingRate;

            player.Position[1] +=
                -0.5 * gravity * deltaTimeS * deltaTimeS + previousVelocityY * deltaTimeS;

            player.Position[2] +=
                (deltaTimeS - dampingAmount / velocityDampingRate) * accelerationZ / velocityDampingRate + dampingAmount * previousVelocityZ / velocityDampingRate;

            double worldHalfSize = (double)MapBoxScale;
            double movementBoundary = worldHalfSize - player.Radius;
            double clampedX = Math.Max(Math.Min(movementBoundary, player.Position[0]), -movementBoundary);
            double clampedY = Math.Max(Math.Min(movementBoundary, player.Position[1]), player.Height - worldHalfSize);
            double clampedZ = Math.Max(Math.Min(movementBoundary, player.Position[2]), -movementBoundary);

            if (player.Position[0] != clampedX) player.Velocity[0] = 0;
            if (player.Position[1] != clampedY) player.Velocity[1] = (wasd & 16) != 0 ? 8.4375 : 0;
            if (player.Position[2] != clampedZ) player.Velocity[2] = 0;

            player.Position[0] = clampedX;
            player.Position[1] = clampedY;
            player.Position[2] = clampedZ;
        }
    }


    public static void DrawCircle(IntPtr renderer, float r, float x, float y)
    {
        float angle;
        FPoint[] points = new FPoint[CircleDrawSidesLength];
        int i;
        for (i = 0; i < CircleDrawSidesLength; i++)
        {
            angle = 2.0f * MathF.PI * (float)i / (float)CircleDrawSide;
            points[i].X = x + r * MathF.Cos(angle);
            points[i].Y = y + r * MathF.Sin(angle);
        }
        RenderLines(renderer, points, CircleDrawSidesLength);

    }


    public static void DrawClippedSegment(
        IntPtr renderer,
        float edgeStartX, float edgeStartY, float edgeStartZ,
        float edgeEndX, float edgeEndY, float edgeEndZ,
        float viewportCenterX, float viewportCenterY, float projectionDistance, float nearClipPlane)
    {
        if (edgeStartZ >= -nearClipPlane && edgeEndZ >= -nearClipPlane) return;
        float deltaX = edgeStartX - edgeEndX;
        float deltaY = edgeStartY - edgeEndY;
        if (edgeStartZ > -nearClipPlane)
        {
            float clipFactor = (-nearClipPlane - edgeEndZ) / (edgeStartZ - edgeEndZ);
            edgeStartX = edgeEndX + deltaX * clipFactor;
            edgeStartY = edgeEndY + deltaY * clipFactor;
            edgeStartZ = -nearClipPlane;
        }
        else if (edgeEndZ > -nearClipPlane)
        {
            float t = (-nearClipPlane - edgeStartZ) / (edgeEndZ - edgeStartZ);
            edgeEndX = edgeStartX - deltaX * t;
            edgeEndY = edgeStartY - deltaY * t;
            edgeEndZ = -nearClipPlane;
        }
        edgeStartX = -projectionDistance * edgeStartX / edgeStartZ;
        edgeStartY = -projectionDistance * edgeStartY / edgeStartZ;
        edgeEndX = -projectionDistance * edgeEndX / edgeEndZ;
        edgeEndY = -projectionDistance * edgeEndY / edgeEndZ;
        RenderLine(renderer, viewportCenterX + edgeStartX, viewportCenterY - edgeStartY, viewportCenterX + edgeEndX, viewportCenterY - edgeEndY);
    }


    public static void Draw(IntPtr renderer, float[,] edges, Player[] players, int playersLength)
    {
        int i, j, k;
        int renderWidth, renderHeight;

        if (!GetRenderOutputSize(renderer, out renderWidth, out renderHeight))
        {
            return;
        }
        SetRenderDrawColor(renderer, 0, 0, 0, byte.MaxValue);
        RenderClear(renderer);
        if (playersLength > 0)
        {
            float renderWidthF = (float)renderWidth;
            float renderHeightF = (float)renderHeight;
            int viewportColumns = playersLength > 2 ? 2 : 1;
            int viewportRows = playersLength > 1 ? 2 : 1;
            float viewportWidth = renderWidthF / ((float)viewportColumns);
            float viewportHeight = renderHeightF / ((float)viewportRows);
            for (i = 0; i < playersLength; i++)
            {
                float columnIndex = (float)(i % viewportColumns);
                float rowIndex = (float)(i / viewportColumns);
                float viewportCenterX = (columnIndex + 0.5f) * viewportWidth;
                float viewportCenterY = (rowIndex + 0.5f) * viewportHeight;
                float focalLength = (float)(0.5 * Math.Sqrt(viewportWidth * viewportWidth + viewportHeight * viewportHeight));
                float viewportX = columnIndex * viewportWidth;
                float viewportY = rowIndex * viewportHeight;
                Rect viewportRect;
                viewportRect.X = (int)viewportX;
                viewportRect.Y = (int)viewportY;
                viewportRect.W = (int)viewportWidth;
                viewportRect.H = (int)viewportHeight;
                SetRenderClipRect(renderer, in viewportRect);
                double cameraX = players[i].Position[0];
                double cameraY = players[i].Position[1];
                double cameraZ = players[i].Position[2];
                double binaryAngleToRadians = Math.PI / 2147483648.0;
                double yawRadians = binaryAngleToRadians * players[i].Yaw;
                double pitchRadians = binaryAngleToRadians * players[i].Pitch;
                double cosYaw = Math.Cos(yawRadians);
                double sinYaw = Math.Sin(yawRadians);
                double cosPitch = Math.Cos(pitchRadians);
                double sinPitch = Math.Sin(pitchRadians);
                double[] viewMatrix =
                [
                    cosYaw           ,          0, -sinYaw           ,
                    sinYaw * sinPitch,   cosPitch,  cosYaw * sinPitch,
                    sinYaw * cosPitch,  -sinPitch,  cosYaw * cosPitch
                ];
                SetRenderDrawColor(renderer, 64, 64, 64, byte.MaxValue);
                for (k = 0; k < MapBoxEdgesLength; k++)
                {
                    float edgeStartX = (float)(viewMatrix[0] * (edges[k, 0] - cameraX) + viewMatrix[1] * (edges[k, 1] - cameraY) + viewMatrix[2] * (edges[k, 2] - cameraZ));
                    float edgeStartY = (float)(viewMatrix[3] * (edges[k, 0] - cameraX) + viewMatrix[4] * (edges[k, 1] - cameraY) + viewMatrix[5] * (edges[k, 2] - cameraZ));
                    float edgeStartZ = (float)(viewMatrix[6] * (edges[k, 0] - cameraX) + viewMatrix[7] * (edges[k, 1] - cameraY) + viewMatrix[8] * (edges[k, 2] - cameraZ));
                    float edgeEndX = (float)(viewMatrix[0] * (edges[k, 3] - cameraX) + viewMatrix[1] * (edges[k, 4] - cameraY) + viewMatrix[2] * (edges[k, 5] - cameraZ));
                    float edgeEndY = (float)(viewMatrix[3] * (edges[k, 3] - cameraX) + viewMatrix[4] * (edges[k, 4] - cameraY) + viewMatrix[5] * (edges[k, 5] - cameraZ));
                    float edgeEndZ = (float)(viewMatrix[6] * (edges[k, 3] - cameraX) + viewMatrix[7] * (edges[k, 4] - cameraY) + viewMatrix[8] * (edges[k, 5] - cameraZ));
                    DrawClippedSegment(renderer, edgeStartX, edgeStartY, edgeStartZ, edgeEndX, edgeEndY, edgeEndZ, viewportCenterX, viewportCenterY, focalLength, 1);
                }
                for (j = 0; j < playersLength; j++)
                {
                    if (i == j) continue;
                    SetRenderDrawColor(renderer, players[j].Color[0], players[j].Color[1], players[j].Color[2], byte.MaxValue);
                    for (k = 0; k < 2; k++)
                    {
                        double relativeX = players[j].Position[0] - players[i].Position[0];
                        double relativeY = players[j].Position[1] - players[i].Position[1] + (players[j].Radius - players[j].Height) * (float)k;
                        double relativeZ = players[j].Position[2] - players[i].Position[2];
                        double targetX = viewMatrix[0] * relativeX + viewMatrix[1] * relativeY + viewMatrix[2] * relativeZ;
                        double targetY = viewMatrix[3] * relativeX + viewMatrix[4] * relativeY + viewMatrix[5] * relativeZ;
                        double targetZ = viewMatrix[6] * relativeX + viewMatrix[7] * relativeY + viewMatrix[8] * relativeZ;
                        double projectedRadius = players[j].Radius * focalLength / targetZ;
                        if (!(targetZ < 0)) continue;
                        DrawCircle(renderer, (float)(projectedRadius), (float)(viewportCenterX - focalLength * targetX / targetZ), (float)(viewportCenterY + focalLength * targetY / targetZ));
                    }
                }
                SetRenderDrawColor(renderer, 255, 255, 255, 255);
                RenderLine(renderer, viewportCenterX, viewportCenterY - 10, viewportCenterX, viewportCenterY + 10);
                RenderLine(renderer, viewportCenterX - 10, viewportCenterY, viewportCenterX + 10, viewportCenterY);
            }
        }
        SetRenderClipRect(renderer, IntPtr.Zero);
        SetRenderDrawColor(renderer, 255, 255, 255, 255);
        RenderDebugText(renderer, 0, 0, DebugString);
        RenderPresent(renderer);
    }


    public static void InitPlayers(Player[] players, int length)
    {
        int i;
        for (i = 0; i < length; i++)
        {
            players[i] = new Player();
            players[i].Position[0] = 8.0 * ((i & 1) != 0 ? -1.0 : 1.0);
            players[i].Position[1] = 0;
            players[i].Position[2] = 8.0 * ((i & 1) != 0 ? -1.0 : 1.0) * ((i & 2) != 0 ? -1.0 : 1.0);
            players[i].Velocity[0] = 0;
            players[i].Velocity[1] = 0;
            players[i].Velocity[2] = 0;
            players[i].Yaw = (uint)(0x20000000 + ((i & 1) != 0 ? 0x80000000 : 0) + ((i & 2) != 0 ? 0x40000000 : 0));
            players[i].Pitch = -0x08000000;
            players[i].Radius = 0.5f;
            players[i].Height = 1.5f;
            players[i].Wasd = 0;
            players[i].Mouse = 0;
            players[i].Keyboard = 0;
            players[i].Color[0] = (byte)(((1 << (i / 2)) & 2) != 0 ? 0 : 0xff);
            players[i].Color[1] = (byte)(((1 << (i / 2)) & 1) != 0 ? 0 : 0xff);
            players[i].Color[2] = (byte)(((1 << (i / 2)) & 4) != 0 ? 0 : 0xff);
            players[i].Color[0] = (byte)((i & 1) != 0 ? players[i].Color[0] : ~players[i].Color[0]);
            players[i].Color[1] = (byte)((i & 1) != 0 ? players[i].Color[1] : ~players[i].Color[1]);
            players[i].Color[2] = (byte)((i & 1) != 0 ? players[i].Color[2] : ~players[i].Color[2]);
        }
    }


    public static void InitEdges(int scale, float[,] edges, int edgesLength)
    {
        int i, j;
        float r = (float)scale;
        int[] map =
        [
            0,1 , 1,3 , 3,2 , 2,0 ,
            7,6 , 6,4 , 4,5 , 5,7 ,
            6,2 , 3,7 , 0,4 , 5,1
        ];
        for (i = 0; i < 12; i++)
        {
            for (j = 0; j < 3; j++)
            {
                edges[i, j + 0] = ((map[i * 2 + 0] & (1 << j)) != 0 ? r : -r);
                edges[i, j + 3] = ((map[i * 2 + 1] & (1 << j)) != 0 ? r : -r);
            }
        }
        for (i = 0; i < scale; i++)
        {
            float d = (float)(i * 2);
            for (j = 0; j < 2; j++)
            {
                edges[i + 12, 3 * j + 0] = j > 0 ? r : -r;
                edges[i + 12, 3 * j + 1] = -r;
                edges[i + 12, 3 * j + 2] = d - r;
                edges[i + 12 + scale, 3 * j + 0] = d - r;
                edges[i + 12 + scale, 3 * j + 1] = -r;
                edges[i + 12 + scale, 3 * j + 2] = j > 0 ? r : -r;
            }
        }
    }
    #endregion


    #region AppInit
    // This function runs once at startup.
    static AppResult AppInit(ref nint appstate, int argc, string[]? argv)
    {
        AppState state = new();
        appstate = GCHandle.ToIntPtr(GCHandle.Alloc(state));

        if (!SetAppMetadata("Example splitscreen shooter game", "1.0", "com.example.woodeneye-008"))
        {
            return AppResult.Failure;
        }
        int i;
        for (i = 0; i < extendedMetadata.Length; i++)
        {
            if (!SetAppMetadataProperty(extendedMetadata[i].Key, extendedMetadata[i].Value))
            {
                return AppResult.Failure;
            }
        }

        if (!Init(InitFlags.Video))
        {
            return AppResult.Failure;
        }
        if (!CreateWindowAndRenderer("examples/demo/woodeneye-008", 640, 480, WindowFlags.Resizable, out state.Window, out state.Renderer))
        {
            return AppResult.Failure;
        }

        state.PlayerCount = 1;

        InitPlayers(state.Players, MaxPlayerCount);
        InitEdges(MapBoxScale, state.Edges, MapBoxEdgesLength);
        DebugString = "";

        SetRenderVSync(state.Renderer, 0);
        SetWindowRelativeMouseMode(state.Window, true);
        SetHintWithPriority(Hints.WindowsRawKeyboard, "1", HintPriority.Override);

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppEvent
    // This function runs when a new event (mouse input, keypresses, etc) occurs.
    static AppResult AppEvent(nint appstate, ref Event evt)
    {
        AppState state = AppState.GetAppState(appstate);
        Player[] players = state.Players;
        int playerCount = state.PlayerCount;
        int i;
        switch (evt.Type)
        {
            case (uint)EventType.Quit:
                return AppResult.Success;
            case (uint)EventType.MouseRemoved:
                for (i = 0; i < playerCount; i++)
                {
                    if (players[i].Mouse == evt.MDevice.Which)
                    {
                        players[i].Mouse = 0;
                    }
                }
                break;
            case (uint)EventType.KeyboardRemoved:
                for (i = 0; i < playerCount; i++)
                {
                    if (players[i].Keyboard == evt.KDevice.Which)
                    {
                        players[i].Keyboard = 0;
                    }
                }
                break;
            case (uint)EventType.MouseMotion:
                {
                    uint id = evt.Motion.Which;
                    int index = WhoseMouse(id, players, playerCount);
                    if (index >= 0)
                    {
                        players[index].Yaw -= (uint)(((int)evt.Motion.XRel) * 0x00080000);
                        players[index].Pitch = (int)MathF.Max(-0x40000000, MathF.Min(0x40000000, players[index].Pitch - ((int)evt.Motion.YRel) * 0x00080000));
                    }
                    else if (id != 0)
                    {
                        for (i = 0; i < MaxPlayerCount; i++)
                        {
                            if (players[i].Mouse == 0)
                            {
                                players[i].Mouse = id;
                                state.PlayerCount = (int)MathF.Max(state.PlayerCount, i + 1);
                                break;
                            }
                        }
                    }
                    break;
                }
            case (uint)EventType.MouseButtonDown:
                {
                    uint id = evt.Button.Which;
                    int index = WhoseMouse(id, players, playerCount);
                    if (index >= 0)
                    {
                        Shoot(index, players, playerCount);
                    }
                    break;
                }
            case (uint)EventType.KeyDown:
                {
                    Keycode sym = evt.Key.Key;
                    uint id = evt.Key.Which;
                    int index = WhoseKeyboard(id, players, playerCount);
                    if (index >= 0)
                    {
                        if (sym == Keycode.W) players[index].Wasd |= 1;
                        if (sym == Keycode.A) players[index].Wasd |= 2;
                        if (sym == Keycode.S) players[index].Wasd |= 4;
                        if (sym == Keycode.D) players[index].Wasd |= 8;
                        if (sym == Keycode.Space) players[index].Wasd |= 16;
                    }
                    else if (id != 0)
                    {
                        for (i = 0; i < MaxPlayerCount; i++)
                        {
                            if (players[i].Keyboard == 0)
                            {
                                players[i].Keyboard = id;
                                state.PlayerCount = (int)MathF.Max(state.PlayerCount, i + 1);
                                break;
                            }
                        }
                    }
                    break;
                }
            case (uint)EventType.KeyUp:
                {
                    Keycode sym = evt.Key.Key;
                    uint id = evt.Key.Which;
                    if (sym == Keycode.Escape)
                    {
                        return AppResult.Success;
                    }
                    int index = WhoseKeyboard(id, players, playerCount);
                    if (index >= 0)
                    {
                        if (sym == Keycode.W) players[index].Wasd &= 30;
                        if (sym == Keycode.A) players[index].Wasd &= 29;
                        if (sym == Keycode.S) players[index].Wasd &= 27;
                        if (sym == Keycode.D) players[index].Wasd &= 23;
                        if (sym == Keycode.Space) players[index].Wasd &= 15;
                    }
                    break;
                }
        }
        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppIterate
    // This function runs once per frame, and is the heart of the program.
    static AppResult AppIterate(nint appstate)
    {
        AppState state = AppState.GetAppState(appstate);

        ulong currentTime = GetTicksNS();
        ulong deltaTimeNS = currentTime - state.PreviousFrameTime;
        Update(state.Players, state.PlayerCount, deltaTimeNS);
        Draw(state.Renderer, state.Edges, state.Players, state.PlayerCount);
        if (currentTime - state.LastFpsUpdateTime > 999999999)
        {
            state.LastFpsUpdateTime = currentTime;
            DebugString = $"{state.FrameCount} fps";
            state.FrameCount = 0;
        }
        state.PreviousFrameTime = currentTime;
        state.FrameCount += 1;
        ulong elapsed = GetTicksNS() - currentTime;
        if (elapsed < 999999)
        {
            DelayNS(999999 - elapsed);
        }

        return AppResult.Continue;  // carry on with the program!
    }
    #endregion


    #region AppQuit
    // This function runs once at shutdown.
    static void AppQuit(nint appstate, AppResult result)
    {
        // just free the memory, SDL will clean up the window/renderer for us.
        if (appstate != IntPtr.Zero)
        {
            GCHandle handle = GCHandle.FromIntPtr(appstate);
            AppState gameState = (AppState)handle.Target!;

            DestroyRenderer(gameState.Renderer);
            DestroyWindow(gameState.Window);

            handle.Free();
        }
    }
    #endregion
}