using Raylib_cs;
using BomberBoy.src.PPU;
using System.Numerics;

namespace BomberBoy.src;

public class Screen
{
    public const int SCREEN_WIDTH = 160;
    public const int SCREEN_HEIGHT = 144;
    private const int SCALE = 3;

    private readonly Ppu _ppu;
    private readonly Texture2D _texture; // Texture that can be updated from framebuffer data.
    private readonly Image _image;

    public Screen(Ppu ppu)
    {
        _ppu = ppu;
        Raylib.SetTraceLogLevel(TraceLogLevel.Error);
        Raylib.InitWindow(SCREEN_WIDTH * SCALE, SCREEN_HEIGHT * SCALE, "BomberBoy GB Emulator");
        Raylib.SetTargetFPS(60);

        _image = Raylib.GenImageColor(SCREEN_WIDTH, SCREEN_HEIGHT, Color.Black);
        _texture = Raylib.LoadTextureFromImage(_image);
    }

    public void Update()
    {
        unsafe
        {
            fixed (int* ptr = &_ppu.FrameBuffer[0])
            {
                Raylib.UpdateTexture(_texture, (void*)ptr);
            }
        }

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawTextureEx(_texture, new Vector2(0, 0), 0, SCALE, Color.White);
        Raylib.EndDrawing();
    }

    public void HandleEvents()
    {
        // TODO event key inputs
    }

    public bool ShouldClose()
    {
        return Raylib.WindowShouldClose();
    }

    public void Terminate()
    {
        Raylib.UnloadTexture(_texture);
        Raylib.UnloadImage(_image);
        Raylib.CloseWindow();
    }
}