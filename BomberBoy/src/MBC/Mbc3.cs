using BomberBoy.src.PAK;
using System;

namespace BomberBoy.src.MBC;

public class Mbc3 : IMbc
{
    private readonly Pak _pak;
    private readonly byte[] _eram;

    private const int ROM_OFFSET = 0x4000;
    private const int ERAM_OFFSET = 0x2000;

    private bool _eramEnabled;
    private int _romBank = 1;
    private int _ramBankOrRtcReg; // Can be 0x00-0x03 for RAM, or 0x08-0x0C for RTC

    // RTC Registers
    private byte _rtc_S;  // Seconds (0-59)
    private byte _rtc_M;  // Minutes (0-59)
    private byte _rtc_H;  // Hours (0-23)
    private byte _rtc_DL; // Lower 8 bits of Day Counter
    private byte _rtc_DH; // Upper 1 bit of Day Counter, Halt flag, and Carry flag

    // Latched RTC Registers
    private byte _latched_S;
    private byte _latched_M;
    private byte _latched_H;
    private byte _latched_DL;
    private byte _latched_DH;

    private byte _latchSequenceState = 0xFF;
    private DateTime _lastRtcUpdate;

    public Mbc3(Pak pak, int eramSize)
    {
        _pak = pak;
        _eram = new byte[eramSize];
        // TODO: Load RTC state from a save file. For now, we start from zero.
        _lastRtcUpdate = DateTime.Now;
    }

    public byte ReadLoRom(ushort addr)
    {
        return _pak.data[addr];
    }

    public byte ReadHiRom(ushort addr)
    {
        int currentRomBank = _romBank == 0 ? 1 : _romBank;
        return _pak.data[(ROM_OFFSET * currentRomBank) + (addr & 0x3FFF)];
    }

    public void WriteRom(ushort addr, byte value)
    {
        switch (addr)
        {
            case < 0x2000:
                _eramEnabled = (value & 0x0F) == 0x0A;
                break;
            case < 0x4000:
                _romBank = value & 0x7F;
                break;
            case < 0x6000:
                _ramBankOrRtcReg = value;
                break;
            // 6000-7FFF: Latch Clock Data
            case < 0x8000:
                // A sequence of 0x00 followed by 0x01 latches the clock data.
                // This way we can have a stable read of copied RTC values in ERAM 
                // while the main RTC continues to tick.
                if (_latchSequenceState == 0x00 && value == 0x01)
                {
                    LatchRtc();
                }
                _latchSequenceState = value;
                break;
        }
    }

    public byte ReadEram(ushort addr)
    {
        if (!_eramEnabled)
        {
            return 0xFF;
        }

        switch (_ramBankOrRtcReg)
        {
            case 0x00:
            case 0x01:
            case 0x02:
            case 0x03:
                if (_eram.Length > 0)
                {
                    return _eram[(ERAM_OFFSET * _ramBankOrRtcReg) + (addr & 0x1FFF)];
                }
                return 0xFF;

            case 0x08: return _latched_S;
            case 0x09: return _latched_M;
            case 0x0A: return _latched_H;
            case 0x0B: return _latched_DL;
            case 0x0C: return _latched_DH;

            default:
                return 0xFF;
        }
    }

    public void WriteEram(ushort addr, byte value)
    {
        if (!_eramEnabled)
        {
            return;
        }

        switch (_ramBankOrRtcReg)
        {
            case 0x00:
            case 0x01:
            case 0x02:
            case 0x03:
                if (_eram.Length > 0)
                {
                    _eram[(ERAM_OFFSET * _ramBankOrRtcReg) + (addr & 0x1FFF)] = value;
                }
                break;

            case 0x08: _rtc_S = value; break;
            case 0x09: _rtc_M = value; break;
            case 0x0A: _rtc_H = value; break;
            case 0x0B: _rtc_DL = value; break;
            case 0x0C: _rtc_DH = value; break;
        }
    }

    private void LatchRtc()
    {
        UpdateRtc();
        _latched_S = _rtc_S;
        _latched_M = _rtc_M;
        _latched_H = _rtc_H;
        _latched_DL = _rtc_DL;
        _latched_DH = _rtc_DH;
    }

    private void UpdateRtc()
    {
        // Check if timer is halted
        if ((_rtc_DH & 0x40) != 0)
        {
            return;
        }

        TimeSpan elapsed = DateTime.Now - _lastRtcUpdate;
        if (elapsed.TotalSeconds < 1)
        {
            return;
        }

        long totalSeconds = (long)elapsed.TotalSeconds;

        // Update last update time to now, but keep the remainder of the second for precision.
        _lastRtcUpdate = _lastRtcUpdate.AddSeconds(totalSeconds);

        // Add elapsed seconds to RTC registers
        totalSeconds += _rtc_S;
        _rtc_S = (byte)(totalSeconds % 60);
        long totalMinutes = totalSeconds / 60;

        totalMinutes += _rtc_M;
        _rtc_M = (byte)(totalMinutes % 60);
        long totalHours = totalMinutes / 60;

        totalHours += _rtc_H;
        _rtc_H = (byte)(totalHours % 24);
        long totalDays = totalHours / 24;

        int dayCounter = _rtc_DL | ((_rtc_DH & 0x01) << 8);
        dayCounter += (int)totalDays;

        _rtc_DL = (byte)(dayCounter & 0xFF);
        _rtc_DH = (byte)((_rtc_DH & 0xFE) | ((dayCounter >> 8) & 0x01));

        // Handle day counter overflow
        if (dayCounter > 511)
        {
            _rtc_DH |= 0x80; // Set carry bit
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
        writer.Write(_ramBankOrRtcReg);
        writer.Write(_latchSequenceState);

        writer.Write(_rtc_S);
        writer.Write(_rtc_M);
        writer.Write(_rtc_H);
        writer.Write(_rtc_DL);
        writer.Write(_rtc_DH);

        writer.Write(_latched_S);
        writer.Write(_latched_M);
        writer.Write(_latched_H);
        writer.Write(_latched_DL);
        writer.Write(_latched_DH);

        // Save the base time for RTC calculation
        writer.Write(_lastRtcUpdate.ToBinary());
    }

    public void LoadState(BinaryReader reader)
    {
        if (_eram.Length > 0)
        {
            reader.BaseStream.ReadExactly(_eram);
        }
        _eramEnabled = reader.ReadBoolean();
        _romBank = reader.ReadInt32();
        _ramBankOrRtcReg = reader.ReadInt32();
        _latchSequenceState = reader.ReadByte();

        _rtc_S = reader.ReadByte();
        _rtc_M = reader.ReadByte();
        _rtc_H = reader.ReadByte();
        _rtc_DL = reader.ReadByte();
        _rtc_DH = reader.ReadByte();

        _latched_S = reader.ReadByte();
        _latched_M = reader.ReadByte();
        _latched_H = reader.ReadByte();
        _latched_DL = reader.ReadByte();
        _latched_DH = reader.ReadByte();

        // Restore the base time for RTC calculation
        _lastRtcUpdate = DateTime.FromBinary(reader.ReadInt64());
    }
}