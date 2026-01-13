using BomberBoy.src.CPU;
using BomberBoy.src.PAK;
using BomberBoy.src.MMU;
using BomberBoy.src.PPU;

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

    private readonly StreamWriter? _logStream;
    private readonly bool _debugging;

    public long total_t_cycles = 0;

    public Emulator(string romPath, bool debuggingEnabled = false, long consoleLogTarget = -1, uint consoleLogRadius = 5)
    {
        _debugging = debuggingEnabled;

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
        _timer = new Timer(_interrupt, _mmu);
        _mmu.ConnectTimer(_timer);
        _ppu = new Ppu(_mmu, _interrupt, _debugging);
        _mmu.ConnectPpu(_ppu);
        _screen = new Screen(_ppu);

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
                        // The outer loop will catch this and exit gracefully.
                        break;
                    }
                }

                // We've run enough cycles for one frame.
                _screen.HandleEvents();
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
}