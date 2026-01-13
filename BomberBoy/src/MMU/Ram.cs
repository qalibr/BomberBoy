namespace BomberBoy.src.MMU;

public class Ram
{
    protected byte[] Wram { get; set; } = new byte[0x2000];     // 0xC000 - 0xDFFF
    protected byte[] Io { get; set; } = new byte[128];          // 0xFF00 - 0xFF7F
    protected byte[] Hram { get; set; } = new byte[128];        // 0xFF80 - 0xFFFF
    protected byte[] Vram { get; set; } = new byte[0x2000];     // 0x8000 - 0x9FFF
    protected byte[] Oam { get; set; } = new byte[0xA0];        // 0xFE00 - 0xFE9F

    public byte ReadWram(ushort addr)
    {
        if (addr is >= 0xC000 and <= 0xDFFF)
        {
            return Wram[addr - 0xC000];
        }
        throw new IndexOutOfRangeException("Attempt to access WRAM failed: Out of range.");
    }

    public void WriteWram(ushort addr, byte value)
    {
        if (addr is >= 0xC000 and <= 0xDFFF)
        {
            Wram[addr - 0xC000] = value;
            return;
        }
        throw new IndexOutOfRangeException("Attempt to access WRAM failed: Out of range.");
    }

    public byte ReadIo(ushort addr)
    {
        if (addr is >= 0xFF00 and < 0xFF80)
        {
            return Io[addr - 0xFF00];
        }
        throw new IndexOutOfRangeException("Attempt to access I/O register failed: Out of range.");
    }

    public void WriteIo(ushort addr, byte value)
    {
        if (addr is >= 0xFF00 and < 0xFF80)
        {
            Io[addr - 0xFF00] = value;
            return;
        }
        throw new IndexOutOfRangeException("Attempt to access I/O register failed: Out of range.");
    }

    public byte ReadHram(ushort addr)
    {
        if (addr >= 0xFF80)
        {
            return Hram[addr - 0xFF80];
        }
        throw new IndexOutOfRangeException("Attempt to access HRAM failed: Out of range.");
    }

    public void WriteHram(ushort addr, byte value)
    {
        if (addr >= 0xFF80)
        {
            Hram[addr - 0xFF80] = value;
            return;
        }
        throw new IndexOutOfRangeException("Attempt to access HRAM failed: Out of range.");
    }

    public byte ReadVram(ushort addr)
    {
        if (addr is >= 0x8000 and <= 0x9FFF)
        {
            return Vram[addr - 0x8000];
        }
        throw new IndexOutOfRangeException("Attempt to access VRAM failed: Out of range.");
    }

    public void WriteVram(ushort addr, byte value)
    {
        if (addr is >= 0x8000 and <= 0x9FFF)
        {
            Vram[addr - 0x8000] = value;
            return;
        }
        throw new IndexOutOfRangeException("Attempt to access VRAM failed: Out of range.");
    }

    public byte ReadOam(ushort addr)
    {
        if (addr is >= 0xFE00 and <= 0xFE9F)
        {
            return Oam[addr - 0xFE00];
        }
        throw new IndexOutOfRangeException("Attempt to access OAM failed: Out of range.");
    }

    public void WriteOam(ushort addr, byte value)
    {
        if (addr is >= 0xFE00 and <= 0xFE9F)
        {
            Oam[addr - 0xFE00] = value;
            return;
        }
        throw new IndexOutOfRangeException("Attempt to access OAM failed: Out of range.");
    }
}