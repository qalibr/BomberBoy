using BomberBoy.src.PAK;

namespace BomberBoy.src.MBC;

public class Mbc1 : IMbc
{
    private readonly Pak _pak;
    private readonly byte[] _eram;

    private const int ROM_OFFSET = 0x4000;
    private const int ERAM_OFFSET = 0x2000;

    private bool _eramEnabled;

    private int _romBank = 1;
    private int _ramBank;
    private int _bankingMode;

    public Mbc1(Pak pak, int eramSize)
    {
        _pak = pak;
        _eram = new byte[eramSize];
    }

    public byte ReadLoRom(ushort addr)
    {
        return _pak.data[addr];
    }

    public byte ReadHiRom(ushort addr)
    {
        return _pak.data[(ROM_OFFSET * _romBank) + (addr & 0x3FFF)];
    }

    public void WriteRom(ushort addr, byte value)
    {
        switch (addr)
        {
            case < 0x2000:
                _eramEnabled = value == 0x0A;
                break;
            case < 0x4000:
                _romBank = value & 0x1F;
                if (_romBank == 0x00 || _romBank == 0x20 || _romBank == 0x40 || _romBank == 0x60)
                {
                    _romBank++;
                }
                break;
            case < 0x6000:
                if (_bankingMode == 0)
                {
                    _romBank |= value & 0x3;
                    if (_romBank == 0x00 || _romBank == 0x20 || _romBank == 0x40 || _romBank == 0x60)
                    {
                        _romBank++;
                    }
                }
                else
                {
                    _ramBank = value & 0x3;
                }
                break;
            case < 0x8000:
                _bankingMode = value & 0x1;
                break;
        }
    }

    public byte ReadEram(ushort addr)
    {
        return _eramEnabled ? _eram[(ERAM_OFFSET * _ramBank) + (addr & 0x1FFF)] : (byte)0xFF;
    }

    public void WriteEram(ushort addr, byte value)
    {
        if (_eramEnabled)
        {
            _eram[(ERAM_OFFSET * _ramBank) + (addr & 0x1FFF)] = value;
        }
    }

    public void SaveState(BinaryWriter writer)
    {
        if (_eram.Length > 0)
        {
            writer.Write(_eram);
        }
        writer.Write(_eramEnabled);
        writer.Write(_romBank);
        writer.Write(_bankingMode);
    }

    public void LoadState(BinaryReader reader)
    {
        if (_eram.Length > 0)
        {
            reader.BaseStream.ReadExactly(_eram);
        }
        _eramEnabled = reader.ReadBoolean();
        _romBank = reader.ReadInt32();
        _bankingMode = reader.ReadInt32();
    }
}