using BomberBoy.src.MBC;
using BomberBoy.src.PPU;

namespace BomberBoy.src.MMU;

public class Mmu : Ram
{
    private readonly IMbc _mbc;
    private Ppu? _ppu;
    private Timer? _timer;
    private Joypad _joypad = null!;

    private bool _dmaActive = false;
    private int _dmaCyclesLeft = 0;
    private ushort _dmaSourceAddr = 0;

    private readonly bool _debugging;

    public Mmu(IMbc mbc, bool debuggingEnabled = false)
    {
        _mbc = mbc;
        _debugging = debuggingEnabled;
    }

    public void ConnectTimer(Timer timer)
    {
        _timer = timer;
    }

    public void ConnectPpu(Ppu ppu)
    {
        _ppu = ppu;
    }

    public void ConnectJoypad(Joypad joypad)
    {
        _joypad = joypad;
    }

    public byte ReadByte(ushort addr)
    {
        // Gameboy Doctor stub
        // if (addr == 0xFF44 && _debugging)
        // {
        //     return 0x90;
        // }

        if (addr == 0xFF00) // JOYPAD
        {
            return _joypad.P1;
        }

        // DMA LOCK: CPU can only access HRAM (0xFF80-0xFFFF) during DMA.
        if (_dmaActive && addr < 0xFF80)
        {
            return 0xFF;
        }

        switch (addr)
        {
            case <= 0x3FFF:
                return _mbc.ReadLoRom(addr);
            case <= 0x7FFF:
                return _mbc.ReadHiRom(addr);
            // VRAM is inaccessible during Mode 3 (Drawing)
            case <= 0x9FFF:
                if (_ppu?.CurrentMode == 3)
                {
                    if (_debugging) throw new InvalidOperationException($"[DEBUG] Illegal VRAM read access during PPU Mode 3 at address {addr:X4}.");
                    return 0xFF;
                }
                return ReadVram(addr);
            case <= 0xBFFF:
                return _mbc.ReadEram(addr);
            case <= 0xDFFF:
                return ReadWram(addr);
            case <= 0xFDFF:
                return ReadWram((ushort)(addr - 0x2000)); // Echo RAM
            // OAM is inaccessible during Mode 2 (OAM Scan) and Mode 3 (Drawing)
            case <= 0xFE9F:
                if (_ppu?.CurrentMode is 2 or 3)
                {
                    if (_debugging) throw new InvalidOperationException($"[DEBUG] Illegal OAM read access during PPU Mode {_ppu.CurrentMode} at address {addr:X4}.");
                    return 0xFF;
                }
                return ReadOam(addr);
            case <= 0xFEFF:
                return 0; // Unused
            case <= 0xFF7F:
                return ReadIo(addr);
            default: // <= 0xFFFF
                return ReadHram(addr);
        }
    }

    public void WriteByte(ushort addr, byte value)
    {
        if (addr == 0xFF00) // JOYPAD
        {
            _joypad.P1 = value;
            return;
        }

        // DMA LOCK: CPU writes are ignored for everything except HRAM during DMA.
        if (_dmaActive && addr < 0xFF80)
        {
            return;
        }

        switch (addr)
        {
            case <= 0x7FFF:
                _mbc.WriteRom(addr, value);
                break;
            // VRAM is inaccessible during Mode 3 (Drawing)
            case <= 0x9FFF:
                if (_ppu?.CurrentMode == 3)
                {
                    if (_debugging) throw new InvalidOperationException($"[DEBUG] Illegal VRAM write access during PPU Mode 3 at address {addr:X4}.");
                    break;
                }
                WriteVram(addr, value);
                break;
            case <= 0xBFFF:
                _mbc.WriteEram(addr, value);
                break;
            case <= 0xDFFF:
                WriteWram(addr, value);
                break;
            case <= 0xFDFF:
                WriteWram((ushort)(addr - 0x2000), value);
                break;
            // OAM is inaccessible during Mode 2 (OAM Scan) and Mode 3 (Drawing)
            case <= 0xFE9F:
                if (_ppu?.CurrentMode is 2 or 3)
                {
                    if (_debugging) throw new InvalidOperationException($"[DEBUG] Illegal OAM write access during PPU Mode {_ppu.CurrentMode} at address {addr:X4}.");
                    break;
                }
                WriteOam(addr, value);
                break;
            case <= 0xFEFF:
                // Unused
                break;
            case 0xFF04: // DIV register
                // Any write to DIV resets it and its internal counter.
                _timer?.ResetDivCounter();
                break;
            case 0xFF46: // DMA Transfer
                _dmaActive = true;
                _dmaCyclesLeft = 640; // 160 bytes * 4 cycles
                _dmaSourceAddr = (ushort)(value << 8); break;
            case <= 0xFF7F:
                WriteIo(addr, value);
                break;
            case <= 0xFFFF:
                WriteHram(addr, value);
                break;
        }
    }

    public void WriteWord(ushort addr, ushort val)
    {
        WriteByte(addr, (byte)(val & 0xFF));
        WriteByte((ushort)(addr + 1), (byte)((val >> 8) & 0xFF));
    }

    public byte PpuRead(ushort addr)
    {
        return addr switch
        {
            // PPU needs to read Tile Data and Tile Maps from VRAM
            >= 0x8000 and <= 0x9FFF => ReadVram(addr),

            // PPU needs to read Sprite Attributes from OAM
            >= 0xFE00 and <= 0xFE9F => ReadOam(addr),

            _ => 0xFF
        };
    }

    public void TickDma()
    {
        if (!_dmaActive) return;

        _dmaCyclesLeft--;

        if (_dmaCyclesLeft <= 0)
        {
            _dmaActive = false;
            _dmaCyclesLeft = 0;

            // Bulk transfer 160 bytes.
            for (int i = 0; i < 0xA0; i++)
            {
                ushort addr = (ushort)(_dmaSourceAddr + i);

                byte data = addr switch
                {
                    <= 0x3FFF => _mbc.ReadLoRom(addr),
                    <= 0x7FFF => _mbc.ReadHiRom(addr),
                    <= 0x9FFF => ReadVram(addr),
                    <= 0xBFFF => _mbc.ReadEram(addr),
                    <= 0xDFFF => ReadWram(addr),
                    _ => 0xFF
                };

                Oam[i] = data;
            }
        }
    }

    // https://gbdev.io/pandocs/Hardware_Reg_List.html
    public byte IE
    {
        get => Hram[127]; // 0xFFFF - 0xFF80 = 127
        set => Hram[127] = value;
    }

    public byte IF
    {
        get => Io[15]; // 0xFF0F - 0xFF00 = 15
        set => Io[15] = value;
    }

    public byte DIV
    {
        get => Io[4]; // 0xFF04 - 0xFF00 = 4
        set => Io[4] = value;
    }

    public byte TIMA
    {
        get => Io[5]; // 0xFF05 - 0xFF00
        set => Io[5] = value;
    }

    public byte TMA
    {
        get => Io[6]; // 0xFF06 - 0xFF00
        set => Io[6] = value;
    }

    public byte TAC
    {
        get => Io[7]; // 0xFF07 - 0xFF00
        set => Io[7] = value;
    }

    public byte LCDC
    {
        get => Io[64]; // 0xFF40 - 0xFF00 = 64
        set => Io[64] = value;
    }

    public byte STAT
    {
        get => Io[65]; // 0xFF41 - 0xFF00
        set => Io[65] = value;
    }

    public byte SCY
    {
        get => Io[66]; // 0xFF42 - 0xFF00
        set => Io[66] = value;
    }

    public byte SCX
    {
        get => Io[67]; // 0xFF43 - 0xFF00
        set => Io[67] = value;
    }

    public byte LY
    {
        get => Io[68]; // 0xFF44 - 0xFF00
        set => Io[68] = value;
    }

    public byte LYC
    {
        get => Io[69]; // 0xFF45 - 0xFF00
        set => Io[69] = value;
    }

    public byte BGP
    {
        get => Io[71]; // 0xFF47 - 0xFF00
        set => Io[71] = value;
    }

    public byte OBP0
    {
        get => Io[72]; // 0xFF48 - 0xFF00
        set => Io[72] = value;
    }

    public byte OBP1
    {
        get => Io[73]; // 0xFF49 - 0xFF00
        set => Io[73] = value;
    }

    public byte WY
    {
        get => Io[74]; // 0xFF4A - 0xFF00
        set => Io[74] = value;
    }

    public byte WX
    {
        get => Io[75]; // 0xFF4B - 0xFF00
        set => Io[75] = value;
    }

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    public void SaveState(BinaryWriter writer)
    {
        writer.Write(Wram);
        writer.Write(Io);
        writer.Write(Hram);
        writer.Write(Vram);
        writer.Write(Oam);
    }

    public void LoadState(BinaryReader reader)
    {
        reader.BaseStream.ReadExactly(Wram);
        reader.BaseStream.ReadExactly(Io);
        reader.BaseStream.ReadExactly(Hram);
        reader.BaseStream.ReadExactly(Vram);
        reader.BaseStream.ReadExactly(Oam);
    }
}