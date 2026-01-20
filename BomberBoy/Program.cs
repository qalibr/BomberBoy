using BomberBoy.src;
using Raylib_cs;
using System.Numerics;
using System.Runtime.InteropServices;

namespace BomberBoy
{
    internal class Program
    {
        const int SCREEN_WIDTH = 160;
        const int SCREEN_HEIGHT = 144;
        const int SCALE = 3;

        static void Main(string[] args)
        {
            Raylib.SetTraceLogLevel(TraceLogLevel.Error);
            Raylib.InitWindow(SCREEN_WIDTH * SCALE, SCREEN_HEIGHT * SCALE, "BomberBoy GB Emulator");
            Raylib.SetTargetFPS(60);

            var image = Raylib.GenImageColor(SCREEN_WIDTH, SCREEN_HEIGHT, Color.Black);
            var texture = Raylib.LoadTextureFromImage(image);
            Raylib.UnloadImage(image);

            Emulator? emulator = null;

            if (args.Length > 0)
            {
                try
                {
                    emulator = new Emulator(args[0]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to initialize: {ex.Message}");
                }
            }

            while (!Raylib.WindowShouldClose())
            {
                if (Raylib.IsFileDropped())
                {
                    var droppedFiles = Raylib.LoadDroppedFiles();
                    var files = new string[droppedFiles.Count];

#pragma warning disable CS8601
                    unsafe
                    {
                        if (droppedFiles.Paths != null)
                        {
                            for (int i = 0; i < droppedFiles.Count; i++)
                            {
                                files[i] = Marshal.PtrToStringAnsi((IntPtr)droppedFiles.Paths[i]);
                            }
                        }
                    }
#pragma warning restore CS8601

                    if (files.Length > 0
                        && !string.IsNullOrEmpty(files[0])
                        && (files[0].EndsWith(".gb")
                        || files[0].EndsWith(".gbc")))
                    {
                        try
                        {
                            emulator = new Emulator(files[0]!);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to load dropped ROM: {ex.Message}");
                            emulator = null;
                        }
                    }
                    Raylib.UnloadDroppedFiles(droppedFiles);
                }

                emulator?.RunFrame();

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);

                if (emulator != null)
                {
                    unsafe
                    {
                        fixed (int* ptr = &emulator.Ppu.FrameBuffer[0])
                        {
                            Raylib.UpdateTexture(texture, (void*)ptr);
                        }
                    }
                    Raylib.DrawTextureEx(texture, new Vector2(0, 0), 0, SCALE, Color.White);
                }
                else
                {
                    string message = "Drag & Drop a ROM file to play";
                    var textSize = Raylib.MeasureText(message, 20);
                    Raylib.DrawText(message, (SCREEN_WIDTH * SCALE - textSize) / 2, (SCREEN_HEIGHT * SCALE - 20) / 2, 20, Color.LightGray);
                }

                Raylib.EndDrawing();
            }

            Raylib.UnloadTexture(texture);
            Raylib.CloseWindow();
        }
    }
}