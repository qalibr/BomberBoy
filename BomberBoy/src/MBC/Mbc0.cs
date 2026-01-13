using BomberBoy.src.PAK;

namespace BomberBoy.src.MBC;

public class Mbc0 : IMbc
{
    private Pak _pak;

    public Mbc0(Pak pak)
    {
        _pak = pak;
    }

    public byte ReadLoRom(ushort addr)
    {
        return _pak.data[addr];
    }

    public byte ReadHiRom(ushort addr)
    {
        return _pak.data[addr];
    }

    public void WriteRom(ushort addr, byte val)
    {
        // Ignore writes on MBC0.
    }

    public byte ReadEram(ushort addr)
    {
        return 0xFF; // No ERAM.
    }

    public void WriteEram(ushort addr, byte val)
    {
        // Ignore writes on MBC0.
    }
}