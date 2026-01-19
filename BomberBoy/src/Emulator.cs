using BomberBoy.src.CPU;
using BomberBoy.src.PAK;
using BomberBoy.src.MMU;
using BomberBoy.src.PPU;
using System.IO;
using Raylib_cs;

namespace BomberBoy.src;

public class Emulator
{
    private readonly Pak _pak;
    private readonly Cpu _cpu;
    private readonly Registers _registers = new();
    private readonly Mmu _mmu;
    private readonly Interrupts _interrupt;
    private readonly Timer _timer;
    private readonly Ppu _ppu;
    private readonly Screen _screen;
    private readonly Joypad _joypad;

    private readonly string _romName;
    private readonly StreamWriter? _logStream;
    private readonly bool _debugging;

    public long total_t_cycles = 0;

    public Emulator(string romPath, bool debuggingEnabled = false, long consoleLogTarget = -1, uint consoleLogRadius = 5)
    {
        _debugging = debuggingEnabled;
        _romName = Path.GetFileNameWithoutExtension(romPath);

        try
        {
            _pak = Pak.CreateCartridge(romPath);
            if (_debugging)
            {
                _logStream = new StreamWriter("gameboy-doctor.log");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load ROM file: {romPath}");
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }

        long minConsoleLogLines = 0;
        long maxConsoleLogLines = long.MaxValue;

        if (_debugging && consoleLogTarget >= 0)
        {
            minConsoleLogLines = Math.Max(0, consoleLogTarget - consoleLogRadius);
            maxConsoleLogLines = consoleLogTarget + consoleLogRadius;
        }

        _mmu = new Mmu(_pak.mbc, _debugging);
        _interrupt = new Interrupts(this, _mmu, _registers, _debugging);
        _joypad = new Joypad(_interrupt);
        _mmu.ConnectJoypad(_joypad);
        _joypad.ConnectMmu(_mmu);
        _timer = new Timer(_interrupt, _mmu);
        _mmu.ConnectTimer(_timer);
        _ppu = new Ppu(_mmu, _interrupt, _debugging);
        _mmu.ConnectPpu(_ppu);
        _screen = new Screen(_ppu, _joypad);

        _cpu = new Cpu(this, _mmu, _registers, _interrupt, _logStream, _debugging, minConsoleLogLines, maxConsoleLogLines);
    }

    // Synchronizes components for a given number of M-cycles.
    public void MachineCycles(int m_cycles)
    {
        int n = m_cycles * 4; // Convert to T-cycles
        total_t_cycles += n;

        for (int i = 0; i < n; i++)
        {
            _timer.Tick();
            _ppu.Tick();
            _mmu.TickDma();
        }
    }

    public void Start()
    {
        const int CYCLES_PER_FRAME = 70224; // T-Cycles for one frame (4194304 / 59.7)

        try
        {
            while (!_screen.ShouldClose() && !_cpu.terminate)
            {
                long cyclesTarget = total_t_cycles + CYCLES_PER_FRAME;
                while (total_t_cycles < cyclesTarget)
                {
                    if (!_cpu.Step())
                    {
                        // _cpu.Step() returns false on termination, which also sets _cpu.terminate.
                        // The outer loop will catch this and exit.
                        break;
                    }
                }

                // We've run enough cycles for one frame.
                _screen.HandleEvents();
                HandleSaves();
                _screen.Update(); // This will draw the PPU's framebuffer to the window.
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred in the emulation loop: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            // Cleanup
            _screen.Terminate();
            _logStream?.Close();
        }
    }

    private void HandleSaves()
    {
        bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);

        // CTRL + T
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.T))
        {
            string savePath = Path.Combine("BomberBoy", "states", $"{_romName}.state");
            CreateSave(savePath);
            Console.WriteLine($"State saved to {savePath}.");
        }

        // CTRL + L
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.L))
        {
            string savePath = Path.Combine("BomberBoy", "states", $"{_romName}.state");
            if (LoadSave(savePath))
                Console.WriteLine("State loaded.");
        }
    }

    public void CreateSave(string path)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            writer.Write(total_t_cycles);

            _cpu.SaveState(writer);
            _mmu.SaveState(writer);
            _ppu.SaveState(writer);
            _timer.SaveState(writer);
            _interrupt.SaveState(writer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving state: {ex.Message}");
        }
    }

    public bool LoadSave(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Save file not found: {path}. CTRL + T to create save file.");
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open);
            using var reader = new BinaryReader(stream);

            total_t_cycles = reader.ReadInt64();

            _cpu.LoadState(reader);
            _mmu.LoadState(reader);
            _ppu.LoadState(reader);
            _timer.LoadState(reader);
            _interrupt.LoadState(reader);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading state: {ex.Message}");
            return false;
        }
    }
}