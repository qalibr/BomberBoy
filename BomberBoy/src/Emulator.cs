using BomberBoy.src.CPU;
using BomberBoy.src.PAK;
using BomberBoy.src.MMU;
using BomberBoy.src.PPU;
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
    public Ppu Ppu { get; }
    public Joypad Joypad { get; }

    private readonly string _romName;

    public long total_t_cycles = 0;

    public Emulator(string romPath)
    {
        _romName = Path.GetFileNameWithoutExtension(romPath);

        _pak = Pak.CreateCartridge(romPath);

        _mmu = new Mmu(_pak.mbc);
        _interrupt = new Interrupts(this, _mmu, _registers);
        Joypad = new Joypad(_interrupt);
        _mmu.ConnectJoypad(Joypad);
        Joypad.ConnectMmu(_mmu);
        _timer = new Timer(_interrupt, _mmu);
        _mmu.ConnectTimer(_timer);
        Ppu = new Ppu(_mmu, _interrupt);
        _mmu.ConnectPpu(Ppu);

        _cpu = new Cpu(this, _mmu, _registers, _interrupt);
    }

    // Synchronizes components for a given number of M-cycles.
    public void MachineCycles(int m_cycles)
    {
        int n = m_cycles * 4; // Convert to T-cycles
        total_t_cycles += n;

        for (int i = 0; i < n; i++)
        {
            _timer.Tick();
            Ppu.Tick();
            _mmu.TickDma();
        }
    }

    public void RunFrame()
    {
        const int CYCLES_PER_FRAME = 70224; // T-Cycles for one frame (4194304 / 59.7)

        if (_cpu.terminate)
        {
            return;
        }

        long cyclesTarget = total_t_cycles + CYCLES_PER_FRAME;
        while (total_t_cycles < cyclesTarget)
        {
            if (!_cpu.Step())
            {
                // _cpu.Step() returns false on termination, which also sets _cpu.terminate.
                break;
            }
        }

        Joypad.HandleInput();
        HandleSaves();
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
            Ppu.SaveState(writer);
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
            Ppu.LoadState(reader);
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