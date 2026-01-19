using BomberBoy.src.MMU;
using BomberBoy.src.PAK;
using BomberBoy.src.Util;

namespace BomberBoy.src.CPU;

public class Cpu
{
    private readonly Emulator _emulator;
    private readonly Mmu _mmu;
    private readonly Registers _register;
    private readonly Interrupts _interrupt;
    // private readonly GBDoctor? _gbDoctor;

    public ushort previousPc;
    public byte opcode;

    public bool halted = false;
    public bool IME = false;
    public bool haltBug = false;
    public int imeCountdown = 0;
    public bool terminate = false;

    // private readonly bool _debugging;

    public Cpu(Emulator emulator,
        Mmu mmu, Registers register,
        Interrupts interrupt,
        StreamWriter? logStream = null,
        bool debuggingEnabled = false,
        long minConsoleLogLines = 0,
        long maxConsoleLogLines = long.MaxValue)
    {
        _emulator = emulator;
        _mmu = mmu;
        _register = register;
        _interrupt = interrupt;
        // _debugging = debuggingEnabled;
        // if (_debugging && logStream != null)
        // {
        //     _gbDoctor = new GBDoctor(logStream, minConsoleLogLines, maxConsoleLogLines);
        // }

        register.AF = 0x01B0;
        register.BC = 0x0013;
        register.DE = 0x00D8;
        register.HL = 0x014D;
        register.PC = 0x0100;
        register.SP = 0xFFFE;
    }

    public void MachineCycles(int c)
    {
        _emulator.MachineCycles(c);
    }

    private void Fetch()
    {
        previousPc = _register.PC;
        opcode = _mmu.ReadByte(_register.PC++);

        if (haltBug)
        {
            _register.PC--;
            haltBug = false;
        }

        MachineCycles(1);
    }

    public bool Step()
    {
        // The EI instruction enables interrupts after the *next* instruction.
        // The EI bug: if an interrupt is pending when EI is executed, the instruction after EI is skipped,
        // and the ISR is executed instead. We model this by enabling IME one step early.
        if (imeCountdown == 2) // This is the cycle for the instruction immediately after EI.
        {
            if ((_mmu.IF & _mmu.IE & 0x1F) != 0) // Is there a pending interrupt?
            {
                // if (_debugging)
                // {
                //     Console.WriteLine("[DEBUG] EI bug triggered: enabling IME early due to pending interrupt.");
                // }
                IME = true;
                imeCountdown = 0; // Cancel normal countdown.
            }
        }

        if (imeCountdown > 0 && --imeCountdown == 0)
        {
            // if (_debugging)
            // {
            //     Console.WriteLine("[DEBUG] IME enabled via EI instruction countdown.");
            // }
            IME = true;
        }

        // Check for interrupts. If one is serviced, it consumes this entire step, preempting the normal fetch-execute cycle.
        if (_interrupt.HandleInterrupts(ref halted, ref IME))
        {
            // if (_debugging)
            // {
            //     Console.WriteLine("[DEBUG] Step consumed by interrupt service routine.");
            // }
            return true;
        }

        if (halted)
        {
            MachineCycles(1);
        }
        else
        {
            // _gbDoctor?.Log(_register, _mmu, ref terminate);
            if (terminate) return false;

            Fetch();

            // _gbDoctor?.Print(_mmu, ref previousPc, ref opcode);

            Execute();
        }

        return true;
    }

    public void SaveState(BinaryWriter writer)
    {
        writer.Write(IME);
        writer.Write(halted);
    }

    public void LoadState(BinaryReader reader)
    {
        IME = reader.ReadBoolean();
        halted = reader.ReadBoolean();
    }

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    /// <summary>
    /// 1M
    /// </summary>
    /// <returns></returns>
    private byte ReadImmediate8()
    {
        byte value = _mmu.ReadByte(_register.PC++);
        MachineCycles(1);
        return value;
    }

    /// <summary>
    /// 2M | 0x01, 0x11, 0x21, 0x31
    /// </summary>
    /// <returns></returns>
    private ushort ReadImmediate16()
    {
        // Little-endian read, so low byte comes first.
        byte low = ReadImmediate8();
        byte high = ReadImmediate8();
        return (ushort)((high << 8) | low);
    }

    /// <summary>
    /// 0x04, 0x14, 0x24, 0x34, 0x0C, 0x1C, 0x2C, 0x3C
    /// </summary>
    /// <param name="register"></param>
    /// <returns></returns>
    private byte Inc(byte register)
    {
        // hFlag is set if there's a carry from bit 3. This must be checked before the increment.
        _register.hFlag = (register & 0x0F) == 0x0F;
        register++;
        _register.zFlag = register == 0;
        _register.nFlag = false;
        return register;
    }

    /// <summary>
    /// 0x05, 0x15, 0x25, 0x35, 0x0D, 0x1D, 0x2D, 0x3D
    /// </summary>
    /// <param name="register"></param>
    /// <returns></returns>
    private byte Dec(byte register)
    {
        // hFlag is set if there's a borrow from bit 4. This happens when the lower nibble is 0.
        _register.hFlag = (register & 0x0F) == 0x00;
        register--;
        _register.zFlag = register == 0;
        _register.nFlag = true;
        return register;
    }

    /// <summary>
    /// 0x03, 0x13, 0x23, 0x33
    /// </summary>
    /// <param name="register"></param>
    /// <returns></returns>
    private ushort Inc16(ushort register)
    {
        MachineCycles(1);
        return ++register;
    }

    /// <summary>
    /// 0x0B, 0x1B, 0x2B, 0x3B
    /// </summary>
    /// <param name="register"></param>
    /// <returns></returns>
    private ushort Dec16(ushort register)
    {
        MachineCycles(1);
        return --register;
    }

    /// <summary>
    /// 0x09, 0x19, 0x29, 0x39
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private ushort AddHL(ushort value)
    {
        MachineCycles(1);

        int result = _register.HL + value;

        _register.nFlag = false; // Always reset for add ops.
        _register.hFlag = ((_register.HL & 0x0FFF) + (value & 0x0FFF)) > 0x0FFF; // Set if there is a carry from bit 11 to 12.
        _register.cFlag = result > 0xFFFF; // Set carry if there is a carry from bit 15.

        return (ushort)result;
    }

    private ushort Pop()
    {
        byte low = _mmu.ReadByte(_register.SP++);
        MachineCycles(1);
        byte high = _mmu.ReadByte(_register.SP++);
        MachineCycles(1);
        return (ushort)((high << 8) | low);
    }

    private void Push(ushort value)
    {
        MachineCycles(1);

        _mmu.WriteByte(--_register.SP, (byte)(value >> 8));
        MachineCycles(1);

        _mmu.WriteByte(--_register.SP, (byte)(value & 0xFF));
        MachineCycles(1);
    }

    private void Jr(bool condition)
    {
        sbyte offset = (sbyte)_mmu.ReadByte(_register.PC++);
        MachineCycles(1);

        if (condition)
        {
            _register.PC = (ushort)(_register.PC + offset);
            MachineCycles(1);
        }
    }

    private void Jp(bool condition)
    {
        ushort address = ReadImmediate16();
        if (condition)
        {
            _register.PC = address;
            MachineCycles(1);
        }
    }

    private void Call(bool condition)
    {
        ushort address = ReadImmediate16();
        if (condition)
        {
            Push(_register.PC);
            _register.PC = address;
        }
    }

    private void Ret(bool condition)
    {
        MachineCycles(1);
        if (condition)
        {
            _register.PC = Pop();
            MachineCycles(1);
        }
    }

    private void Rst()
    {
        Push(_register.PC);
        _register.PC = (ushort)(opcode & 0x38);
    }

    /// <summary>
    /// 0x80 - 0x87
    /// </summary>
    /// <param name="value"></param>
    private void Add(byte value)
    {
        int result = _register.A + value;
        _register.zFlag = (result & 0xFF) == 0;
        _register.nFlag = false;
        _register.hFlag = ((_register.A & 0x0F) + (value & 0x0F)) > 0x0F;
        _register.cFlag = result > 0xFF;
        _register.A = (byte)result;
    }

    /// <summary>
    /// 0x88 - 0x8F
    /// </summary>
    /// <param name="value"></param>
    private void Adc(byte value)
    {
        int carry = _register.cFlag ? 1 : 0;
        int result = _register.A + value + carry;
        _register.zFlag = (result & 0xFF) == 0;
        _register.nFlag = false;
        _register.hFlag = ((_register.A & 0x0F) + (value & 0x0F) + carry) > 0x0F;
        _register.cFlag = result > 0xFF;
        _register.A = (byte)result;
    }

    /// <summary>
    /// 0x90 - 0x97
    /// </summary>
    /// <param name="value"></param>
    private void Sub(byte value)
    {
        int result = _register.A - value;
        _register.zFlag = (result & 0xFF) == 0;
        _register.nFlag = true;
        _register.hFlag = (_register.A & 0x0F) < (value & 0x0F);
        _register.cFlag = result < 0;
        _register.A = (byte)result;
    }

    /// <summary>
    /// 0x98 - 0x9F
    /// </summary>
    /// <param name="value"></param>
    private void Sbc(byte value)
    {
        int carry = _register.cFlag ? 1 : 0;
        int result = _register.A - value - carry;
        _register.zFlag = (result & 0xFF) == 0;
        _register.nFlag = true;
        _register.hFlag = (_register.A & 0x0F) < ((value & 0x0F) + carry);
        _register.cFlag = result < 0;
        _register.A = (byte)result;
    }

    /// <summary>
    /// 0xA0 - 0xA7
    /// </summary>
    /// <param name="value"></param>
    private void And(byte value)
    {
        _register.A &= value;
        _register.zFlag = _register.A == 0;
        _register.nFlag = false;
        _register.hFlag = true;
        _register.cFlag = false;
    }

    /// <summary>
    /// 0xB0 - 0xB7
    /// </summary>
    /// <param name="value"></param>
    private void Or(byte value)
    {
        _register.A |= value;
        _register.zFlag = _register.A == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        _register.cFlag = false;
    }

    /// <summary>
    /// 0xA8 - AF
    /// </summary>
    /// <param name="value"></param>
    private void Xor(byte value)
    {
        _register.A ^= value;
        _register.zFlag = _register.A == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        _register.cFlag = false;
    }

    /// <summary>
    /// 0xB8 - BF
    /// </summary>
    /// <param name="value"></param>
    private void Cp(byte value)
    {
        int result = _register.A - value;
        _register.zFlag = (result & 0xFF) == 0;
        _register.nFlag = true;
        _register.hFlag = (_register.A & 0x0F) < (value & 0x0F);
        _register.cFlag = result < 0;
    }

    // https://gbdev.io/gb-opcodes/optables/
    public void Execute()
    {
        switch (opcode)
        {
            case 0x00: break;
            case 0x01: _register.BC = ReadImmediate16(); break;
            case 0x02: _mmu.WriteByte(_register.BC, _register.A); MachineCycles(1); break;
            case 0x03: _register.BC = Inc16(_register.BC); break;
            case 0x04: _register.B = Inc(_register.B); break;
            case 0x05: _register.B = Dec(_register.B); break;
            case 0x06: _register.B = ReadImmediate8(); break;
            case 0x07: // RLCA
                _register.cFlag = (_register.A & 0x80) != 0; // Check if bit 7 is set, because if so it will be shifted out.
                _register.A = (byte)((_register.A << 1) | (_register.A >> 7));
                _register.zFlag = false;
                _register.nFlag = false;
                _register.hFlag = false;
                break;
            case 0x08: { ushort addr = ReadImmediate16(); _mmu.WriteWord(addr, _register.SP); MachineCycles(2); } break;
            case 0x09: _register.HL = AddHL(_register.BC); break;
            case 0x0A: _register.A = _mmu.ReadByte(_register.BC); MachineCycles(1); break;
            case 0x0B: _register.BC = Dec16(_register.BC); break;
            case 0x0C: _register.C = Inc(_register.C); break;
            case 0x0D: _register.C = Dec(_register.C); break;
            case 0x0E: _register.C = ReadImmediate8(); break;
            case 0x0F: // RRCA
                _register.cFlag = (_register.A & 0x01) != 0; // Check if bit 0 is set, because if so it will be shifted out.
                _register.A = (byte)((_register.A >> 1) | _register.A << 7);
                _register.zFlag = false;
                _register.nFlag = false;
                _register.hFlag = false;
                break;

            case 0x10: // STOP
                // TODO?
                // STOP is a 2-byte instruction used to enter a low-power mode.
                // For now, we'll just advance the PC past the following 0x00 byte.
                _register.PC++;
                break;
            case 0x11: _register.DE = ReadImmediate16(); break;
            case 0x12: _mmu.WriteByte(_register.DE, _register.A); MachineCycles(1); break;
            case 0x13: _register.DE = Inc16(_register.DE); break;
            case 0x14: _register.D = Inc(_register.D); break;
            case 0x15: _register.D = Dec(_register.D); break;
            case 0x16: _register.D = ReadImmediate8(); break;
            case 0x17: // RLA
                {
                    byte carry = (byte)(_register.cFlag ? 1 : 0);
                    _register.cFlag = (_register.A & 0x80) != 0;
                    _register.A = (byte)((_register.A << 1) | carry);
                    _register.zFlag = false;
                    _register.nFlag = false;
                    _register.hFlag = false;
                }
                break;
            case 0x18: Jr(true); break;
            case 0x19: _register.HL = AddHL(_register.DE); break;
            case 0x1A: _register.A = _mmu.ReadByte(_register.DE); MachineCycles(1); break;
            case 0x1B: _register.DE = Dec16(_register.DE); break;
            case 0x1C: _register.E = Inc(_register.E); break;
            case 0x1D: _register.E = Dec(_register.E); break;
            case 0x1E: _register.E = ReadImmediate8(); break;
            case 0x1F: // RRA
                {
                    byte carry = (byte)(_register.cFlag ? 0x80 : 0);
                    _register.cFlag = (_register.A & 0x01) != 0;
                    _register.A = (byte)(carry | (_register.A >> 1));
                    _register.zFlag = false;
                    _register.nFlag = false;
                    _register.hFlag = false;
                }
                break;

            case 0x20: Jr(!_register.zFlag); break;
            case 0x21: _register.HL = ReadImmediate16(); break;
            case 0x22: _mmu.WriteByte(_register.HL, _register.A); MachineCycles(1); _register.HL++; break;
            case 0x23: _register.HL = Inc16(_register.HL); break;
            case 0x24: _register.H = Inc(_register.H); break;
            case 0x25: _register.H = Dec(_register.H); break;
            case 0x26: _register.H = ReadImmediate8(); break;
            case 0x27: // DAA
                {
                    byte correction = 0;

                    if (_register.hFlag || (!_register.nFlag && (_register.A & 0x0F) > 9))
                    {
                        correction |= 0x06;
                    }

                    if (_register.cFlag || (!_register.nFlag && _register.A > 0x99))
                    {
                        correction |= 0x60;
                        _register.cFlag = true;
                    }

                    _register.A = (byte)(_register.A + (_register.nFlag ? -correction : correction)); ;
                    _register.zFlag = _register.A == 0;
                    _register.hFlag = false;
                }
                break;
            case 0x28: Jr(_register.zFlag); break;
            case 0x29: _register.HL = AddHL(_register.HL); break;
            case 0x2A: _register.A = _mmu.ReadByte(_register.HL++); MachineCycles(1); break;
            case 0x2B: _register.HL = Dec16(_register.HL); break;
            case 0x2C: _register.L = Inc(_register.L); break;
            case 0x2D: _register.L = Dec(_register.L); break;
            case 0x2E: _register.L = ReadImmediate8(); break;
            case 0x2F: // CPL
                {
                    _register.A = (byte)~_register.A;
                    _register.nFlag = true;
                    _register.hFlag = true;
                }
                break;

            case 0x30: Jr(!_register.cFlag); break;
            case 0x31: _register.SP = ReadImmediate16(); break;
            case 0x32: _mmu.WriteByte(_register.HL, _register.A); MachineCycles(1); _register.HL--; break;
            case 0x33: _register.SP = Inc16(_register.SP); break;
            case 0x34: // INC (HL)
                {
                    byte value = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    value = Inc(value);
                    _mmu.WriteByte(_register.HL, value);
                    MachineCycles(1);
                }
                break;
            case 0x35: // DEC (HL)
                {
                    byte value = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    value = Dec(value);
                    _mmu.WriteByte(_register.HL, value);
                    MachineCycles(1);
                }
                break;
            case 0x36: byte value_36 = ReadImmediate8(); _mmu.WriteByte(_register.HL, value_36); MachineCycles(1); break;
            case 0x37: { _register.nFlag = false; _register.hFlag = false; _register.cFlag = true; } break;
            case 0x38: Jr(_register.cFlag); break;
            case 0x39: _register.HL = AddHL(_register.SP); break;
            case 0x3A: _register.A = _mmu.ReadByte(_register.HL--); MachineCycles(1); break;
            case 0x3B: _register.SP = Dec16(_register.SP); break;
            case 0x3C: _register.A = Inc(_register.A); break;
            case 0x3D: _register.A = Dec(_register.A); break;
            case 0x3E: _register.A = ReadImmediate8(); break;
            case 0x3F: { _register.nFlag = false; _register.hFlag = false; _register.cFlag = !_register.cFlag; } break;

            case 0x40: break;
            case 0x41: _register.B = _register.C; break;
            case 0x42: _register.B = _register.D; break;
            case 0x43: _register.B = _register.E; break;
            case 0x44: _register.B = _register.H; break;
            case 0x45: _register.B = _register.L; break;
            case 0x46: _register.B = _mmu.ReadByte(_register.HL); MachineCycles(1); break;
            case 0x47: _register.B = _register.A; break;
            case 0x48: _register.C = _register.B; break;
            case 0x49: break;
            case 0x4A: _register.C = _register.D; break;
            case 0x4B: _register.C = _register.E; break;
            case 0x4C: _register.C = _register.H; break;
            case 0x4D: _register.C = _register.L; break;
            case 0x4E: _register.C = _mmu.ReadByte(_register.HL); MachineCycles(1); break;
            case 0x4F: _register.C = _register.A; break;

            case 0x50: _register.D = _register.B; break;
            case 0x51: _register.D = _register.C; break;
            case 0x52: break;
            case 0x53: _register.D = _register.E; break;
            case 0x54: _register.D = _register.H; break;
            case 0x55: _register.D = _register.L; break;
            case 0x56: _register.D = _mmu.ReadByte(_register.HL); MachineCycles(1); break;
            case 0x57: _register.D = _register.A; break;
            case 0x58: _register.E = _register.B; break;
            case 0x59: _register.E = _register.C; break;
            case 0x5A: _register.E = _register.D; break;
            case 0x5B: break;
            case 0x5C: _register.E = _register.H; break;
            case 0x5D: _register.E = _register.L; break;
            case 0x5E: _register.E = _mmu.ReadByte(_register.HL); MachineCycles(1); break;
            case 0x5F: _register.E = _register.A; break;

            case 0x60: _register.H = _register.B; break;
            case 0x61: _register.H = _register.C; break;
            case 0x62: _register.H = _register.D; break;
            case 0x63: _register.H = _register.E; break;
            case 0x64: break;
            case 0x65: _register.H = _register.L; break;
            case 0x66: _register.H = _mmu.ReadByte(_register.HL); MachineCycles(1); break;
            case 0x67: _register.H = _register.A; break;
            case 0x68: _register.L = _register.B; break;
            case 0x69: _register.L = _register.C; break;
            case 0x6A: _register.L = _register.D; break;
            case 0x6B: _register.L = _register.E; break;
            case 0x6C: _register.L = _register.H; break;
            case 0x6D: break;
            case 0x6E: _register.L = _mmu.ReadByte(_register.HL); MachineCycles(1); break;
            case 0x6F: _register.L = _register.A; break;

            case 0x70: _mmu.WriteByte(_register.HL, _register.B); MachineCycles(1); break;
            case 0x71: _mmu.WriteByte(_register.HL, _register.C); MachineCycles(1); break;
            case 0x72: _mmu.WriteByte(_register.HL, _register.D); MachineCycles(1); break;
            case 0x73: _mmu.WriteByte(_register.HL, _register.E); MachineCycles(1); break;
            case 0x74: _mmu.WriteByte(_register.HL, _register.H); MachineCycles(1); break;
            case 0x75: _mmu.WriteByte(_register.HL, _register.L); MachineCycles(1); break;
            case 0x76: // HALT
                {
                    byte pending = (byte)(_mmu.IF & _mmu.IE & 0x1F); // Pending interrupt?

                    if (!IME && pending != 0) // HALT BUG!
                    {
                        // The CPU does not halt.
                        // The PC halts for one cycle, effectively causing the next byte to be read twice.
                        // Trick the next Fetch() to reuse the current PC.
                        halted = false;
                        haltBug = true;
                        // if (_debugging)
                        // {
                        //     Console.WriteLine("[DEBUG] HALT bug triggered. CPU will not halt.");
                        // }
                    }
                    else
                    {
                        halted = true;
                        // if (_debugging)
                        // {
                        //     Console.WriteLine("[DEBUG] HALT instruction executed. CPU halted.");
                        // }
                    }
                }
                break;
            case 0x77: _mmu.WriteByte(_register.HL, _register.A); MachineCycles(1); break;
            case 0x78: _register.A = _register.B; break;
            case 0x79: _register.A = _register.C; break;
            case 0x7A: _register.A = _register.D; break;
            case 0x7B: _register.A = _register.E; break;
            case 0x7C: _register.A = _register.H; break;
            case 0x7D: _register.A = _register.L; break;
            case 0x7E: _register.A = _mmu.ReadByte(_register.HL); MachineCycles(1); break;
            case 0x7F: break;

            case 0x80: Add(_register.B); break;
            case 0x81: Add(_register.C); break;
            case 0x82: Add(_register.D); break;
            case 0x83: Add(_register.E); break;
            case 0x84: Add(_register.H); break;
            case 0x85: Add(_register.L); break;
            case 0x86: Add(_mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x87: Add(_register.A); break;
            case 0x88: Adc(_register.B); break;
            case 0x89: Adc(_register.C); break;
            case 0x8A: Adc(_register.D); break;
            case 0x8B: Adc(_register.E); break;
            case 0x8C: Adc(_register.H); break;
            case 0x8D: Adc(_register.L); break;
            case 0x8E: Adc(_mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x8F: Adc(_register.A); break;

            case 0x90: Sub(_register.B); break;
            case 0x91: Sub(_register.C); break;
            case 0x92: Sub(_register.D); break;
            case 0x93: Sub(_register.E); break;
            case 0x94: Sub(_register.H); break;
            case 0x95: Sub(_register.L); break;
            case 0x96: Sub(_mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x97: Sub(_register.A); break;
            case 0x98: Sbc(_register.B); break;
            case 0x99: Sbc(_register.C); break;
            case 0x9A: Sbc(_register.D); break;
            case 0x9B: Sbc(_register.E); break;
            case 0x9C: Sbc(_register.H); break;
            case 0x9D: Sbc(_register.L); break;
            case 0x9E: Sbc(_mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x9F: Sbc(_register.A); break;

            case 0xA0: And(_register.B); break;
            case 0xA1: And(_register.C); break;
            case 0xA2: And(_register.D); break;
            case 0xA3: And(_register.E); break;
            case 0xA4: And(_register.H); break;
            case 0xA5: And(_register.L); break;
            case 0xA6: And(_mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0xA7: And(_register.A); break;
            case 0xA8: Xor(_register.B); break;
            case 0xA9: Xor(_register.C); break;
            case 0xAA: Xor(_register.D); break;
            case 0xAB: Xor(_register.E); break;
            case 0xAC: Xor(_register.H); break;
            case 0xAD: Xor(_register.L); break;
            case 0xAE: Xor(_mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0xAF: Xor(_register.A); break;

            case 0xB0: Or(_register.B); break;
            case 0xB1: Or(_register.C); break;
            case 0xB2: Or(_register.D); break;
            case 0xB3: Or(_register.E); break;
            case 0xB4: Or(_register.H); break;
            case 0xB5: Or(_register.L); break;
            case 0xB6: Or(_mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0xB7: Or(_register.A); break;
            case 0xB8: Cp(_register.B); break;
            case 0xB9: Cp(_register.C); break;
            case 0xBA: Cp(_register.D); break;
            case 0xBB: Cp(_register.E); break;
            case 0xBC: Cp(_register.H); break;
            case 0xBD: Cp(_register.L); break;
            case 0xBE: Cp(_mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0xBF: Cp(_register.A); break;

            case 0xC0: Ret(!_register.zFlag); break;
            case 0xC1: _register.BC = Pop(); break;
            case 0xC2: Jp(!_register.zFlag); break;
            case 0xC3: Jp(true); break;
            case 0xC4: Call(!_register.zFlag); break;
            case 0xC5: Push(_register.BC); break;
            case 0xC6: Add(ReadImmediate8()); break;
            case 0xC7: Rst(); break;
            case 0xC8: Ret(_register.zFlag); break;
            case 0xC9: _register.PC = Pop(); MachineCycles(1); break;
            case 0xCA: Jp(_register.zFlag); break;
            case 0xCB: opcode = _mmu.ReadByte(_register.PC++); ExecuteCb(); break;
            case 0xCC: Call(_register.zFlag); break;
            case 0xCD: Call(true); break;
            case 0xCE: Adc(ReadImmediate8()); break;
            case 0xCF: Rst(); break;

            case 0xD0: Ret(!_register.cFlag); break;
            case 0xD1: _register.DE = Pop(); break;
            case 0xD2: Jp(!_register.cFlag); break;
            case 0xD3: break;
            case 0xD4: Call(!_register.cFlag); break;
            case 0xD5: Push(_register.DE); break;
            case 0xD6: Sub(ReadImmediate8()); break;
            case 0xD7: Rst(); break;
            case 0xD8: Ret(_register.cFlag); break;
            case 0xD9: // RETI
                _register.PC = Pop();
                MachineCycles(1);
                IME = true;
                // if (_debugging)
                // {
                //     Console.WriteLine($"[DEBUG] RETI executed. IME enabled. PC restored to {_register.PC:X4}.");
                // }
                break;
            case 0xDA: Jp(_register.cFlag); break;
            case 0xDB: break;
            case 0xDC: Call(_register.cFlag); break;
            case 0xDD: break;
            case 0xDE: Sbc(ReadImmediate8()); break;
            case 0xDF: Rst(); break;
            case 0xE0: { byte offset = ReadImmediate8(); ushort addr = (ushort)(0xFF00 + offset); _mmu.WriteByte(addr, _register.A); MachineCycles(1); } break;
            case 0xE1: _register.HL = Pop(); break;
            case 0xE2: _mmu.WriteByte((ushort)(0xFF00 + _register.C), _register.A); MachineCycles(1); break;
            case 0xE3: break;
            case 0xE4: break;
            case 0xE5: Push(_register.HL); break;
            case 0xE6: And(ReadImmediate8()); break;
            case 0xE7: Rst(); break;
            case 0xE8: // ADD SP, e8
                {
                    byte imm = ReadImmediate8();
                    sbyte offset = (sbyte)imm;

                    _register.zFlag = false;
                    _register.nFlag = false;
                    _register.hFlag = ((_register.SP & 0x0F) + (imm & 0x0F)) > 0x0F;
                    _register.cFlag = ((_register.SP & 0xFF) + imm) > 0xFF;

                    _register.SP = (ushort)(_register.SP + offset);
                    MachineCycles(2);
                }
                break;
            case 0xE9: _register.PC = _register.HL; break;
            case 0xEA: { ushort addr = ReadImmediate16(); _mmu.WriteByte(addr, _register.A); MachineCycles(1); } break;
            case 0xEB: break;
            case 0xEC: break;
            case 0xED: break;
            case 0xEE: Xor(ReadImmediate8()); break;
            case 0xEF: Rst(); break;

            case 0xF0:
                {
                    byte offset = ReadImmediate8();
                    ushort addr = (ushort)(0xFF00 + offset);
                    _register.A = _mmu.ReadByte(addr);
                    MachineCycles(1);
                }
                break;
            case 0xF1: _register.AF = Pop(); break;
            case 0xF2: _register.A = _mmu.ReadByte((ushort)(0xFF00 + _register.C)); MachineCycles(1); break;
            case 0xF3: // DI
                // if (_debugging)
                // {
                //     Console.WriteLine("[DEBUG] DI instruction executed. IME disabled.");
                // }
                IME = false;
                imeCountdown = 0;
                break;
            case 0xF4: break;
            case 0xF5: Push(_register.AF); break;
            case 0xF6: Or(ReadImmediate8()); break;
            case 0xF7: Rst(); break;
            case 0xF8: // LD HL, SP+e8
                {
                    byte imm = ReadImmediate8();
                    sbyte offset = (sbyte)imm;
                    _register.zFlag = false;
                    _register.nFlag = false;
                    _register.hFlag = ((_register.SP & 0x0F) + (imm & 0x0F)) > 0x0F;
                    _register.cFlag = ((_register.SP & 0xFF) + imm) > 0xFF;
                    _register.HL = (ushort)(_register.SP + offset);
                    MachineCycles(1);
                }
                break;
            case 0xF9: _register.SP = _register.HL; MachineCycles(1); break;
            case 0xFA: { ushort addr = ReadImmediate16(); _register.A = _mmu.ReadByte(addr); MachineCycles(1); } break;
            case 0xFB: // EI
                // if (_debugging)
                // {
                //     Console.WriteLine("[DEBUG] EI instruction executed. IME will be enabled after next instruction.");
                // }
                imeCountdown = 2;
                break;
            case 0xFC: break;
            case 0xFD: break;
            case 0xFE: Cp(ReadImmediate8()); break;
            case 0xFF: Rst(); break;
        }
    }

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    private byte Rlc(byte value)
    {
        _register.cFlag = (value & 0x80) != 0;
        value = (byte)((value << 1) | (value >> 7));
        _register.zFlag = value == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        return value;
    }

    private byte Rrc(byte value)
    {
        _register.cFlag = (value & 0x01) != 0;
        value = (byte)((value >> 1) | (value << 7));
        _register.zFlag = value == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        return value;
    }

    private byte Rl(byte value)
    {
        byte carry = (byte)(_register.cFlag ? 1 : 0);
        _register.cFlag = (value & 0x80) != 0;
        value = (byte)((value << 1) | carry);
        _register.zFlag = value == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        return value;
    }

    private byte Rr(byte value)
    {
        byte carry = (byte)(_register.cFlag ? 0x80 : 0);
        _register.cFlag = (value & 0x01) != 0;
        value = (byte)((value >> 1) | carry);
        _register.zFlag = value == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        return value;
    }

    private byte Sla(byte value)
    {
        _register.cFlag = (value & 0x80) != 0;
        value <<= 1;
        _register.zFlag = value == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        return value;
    }

    private byte Sra(byte value)
    {
        _register.cFlag = (value & 0x01) != 0;
        value = (byte)((value >> 1) | (value & 0x80));
        _register.zFlag = value == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        return value;
    }

    private byte Swap(byte value)
    {
        value = (byte)(((value & 0x0F) << 4) | ((value & 0xF0) >> 4));
        _register.zFlag = value == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        _register.cFlag = false;
        return value;
    }

    private byte Srl(byte value)
    {
        _register.cFlag = (value & 0x01) != 0;
        value >>= 1;
        _register.zFlag = value == 0;
        _register.nFlag = false;
        _register.hFlag = false;
        return value;
    }

    private void Bit(int bit, byte value)
    {
        _register.zFlag = (value & (1 << bit)) == 0;
        _register.nFlag = false;
        _register.hFlag = true;
    }

    private byte Res(int bit, byte value)
    {
        return (byte)(value & ~(1 << bit));
    }

    private byte Set(int bit, byte value)
    {
        return (byte)(value | (1 << bit));
    }

    public void ExecuteCb()
    {
        MachineCycles(1);

        switch (opcode)
        {
            case 0x00: _register.B = Rlc(_register.B); break;
            case 0x01: _register.C = Rlc(_register.C); break;
            case 0x02: _register.D = Rlc(_register.D); break;
            case 0x03: _register.E = Rlc(_register.E); break;
            case 0x04: _register.H = Rlc(_register.H); break;
            case 0x05: _register.L = Rlc(_register.L); break;
            case 0x06:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Rlc(v));
                    MachineCycles(1);
                }
                break;
            case 0x07: _register.A = Rlc(_register.A); break;
            case 0x08: _register.B = Rrc(_register.B); break;
            case 0x09: _register.C = Rrc(_register.C); break;
            case 0x0A: _register.D = Rrc(_register.D); break;
            case 0x0B: _register.E = Rrc(_register.E); break;
            case 0x0C: _register.H = Rrc(_register.H); break;
            case 0x0D: _register.L = Rrc(_register.L); break;
            case 0x0E:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Rrc(v));
                    MachineCycles(1);
                }
                break;
            case 0x0F: _register.A = Rrc(_register.A); break;

            case 0x10: _register.B = Rl(_register.B); break;
            case 0x11: _register.C = Rl(_register.C); break;
            case 0x12: _register.D = Rl(_register.D); break;
            case 0x13: _register.E = Rl(_register.E); break;
            case 0x14: _register.H = Rl(_register.H); break;
            case 0x15: _register.L = Rl(_register.L); break;
            case 0x16:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Rl(v));
                    MachineCycles(1);
                }
                break;
            case 0x17: _register.A = Rl(_register.A); break;
            case 0x18: _register.B = Rr(_register.B); break;
            case 0x19: _register.C = Rr(_register.C); break;
            case 0x1A: _register.D = Rr(_register.D); break;
            case 0x1B: _register.E = Rr(_register.E); break;
            case 0x1C: _register.H = Rr(_register.H); break;
            case 0x1D: _register.L = Rr(_register.L); break;
            case 0x1E:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Rr(v));
                    MachineCycles(1);
                }
                break;
            case 0x1F: _register.A = Rr(_register.A); break;

            case 0x20: _register.B = Sla(_register.B); break;
            case 0x21: _register.C = Sla(_register.C); break;
            case 0x22: _register.D = Sla(_register.D); break;
            case 0x23: _register.E = Sla(_register.E); break;
            case 0x24: _register.H = Sla(_register.H); break;
            case 0x25: _register.L = Sla(_register.L); break;
            case 0x26:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Sla(v));
                    MachineCycles(1);
                }
                break;
            case 0x27: _register.A = Sla(_register.A); break;
            case 0x28: _register.B = Sra(_register.B); break;
            case 0x29: _register.C = Sra(_register.C); break;
            case 0x2A: _register.D = Sra(_register.D); break;
            case 0x2B: _register.E = Sra(_register.E); break;
            case 0x2C: _register.H = Sra(_register.H); break;
            case 0x2D: _register.L = Sra(_register.L); break;
            case 0x2E:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Sra(v));
                    MachineCycles(1);
                }
                break;
            case 0x2F: _register.A = Sra(_register.A); break;

            case 0x30: _register.B = Swap(_register.B); break;
            case 0x31: _register.C = Swap(_register.C); break;
            case 0x32: _register.D = Swap(_register.D); break;
            case 0x33: _register.E = Swap(_register.E); break;
            case 0x34: _register.H = Swap(_register.H); break;
            case 0x35: _register.L = Swap(_register.L); break;
            case 0x36:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Swap(v));
                    MachineCycles(1);
                }
                break;
            case 0x37: _register.A = Swap(_register.A); break;
            case 0x38: _register.B = Srl(_register.B); break;
            case 0x39: _register.C = Srl(_register.C); break;
            case 0x3A: _register.D = Srl(_register.D); break;
            case 0x3B: _register.E = Srl(_register.E); break;
            case 0x3C: _register.H = Srl(_register.H); break;
            case 0x3D: _register.L = Srl(_register.L); break;
            case 0x3E:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Srl(v));
                    MachineCycles(1);
                }
                break;
            case 0x3F: _register.A = Srl(_register.A); break;

            case 0x40: Bit(0, _register.B); break;
            case 0x41: Bit(0, _register.C); break;
            case 0x42: Bit(0, _register.D); break;
            case 0x43: Bit(0, _register.E); break;
            case 0x44: Bit(0, _register.H); break;
            case 0x45: Bit(0, _register.L); break;
            case 0x46: Bit(0, _mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x47: Bit(0, _register.A); break;

            case 0x48: Bit(1, _register.B); break;
            case 0x49: Bit(1, _register.C); break;
            case 0x4A: Bit(1, _register.D); break;
            case 0x4B: Bit(1, _register.E); break;
            case 0x4C: Bit(1, _register.H); break;
            case 0x4D: Bit(1, _register.L); break;
            case 0x4E: Bit(1, _mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x4F: Bit(1, _register.A); break;

            case 0x50: Bit(2, _register.B); break;
            case 0x51: Bit(2, _register.C); break;
            case 0x52: Bit(2, _register.D); break;
            case 0x53: Bit(2, _register.E); break;
            case 0x54: Bit(2, _register.H); break;
            case 0x55: Bit(2, _register.L); break;
            case 0x56: Bit(2, _mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x57: Bit(2, _register.A); break;

            case 0x58: Bit(3, _register.B); break;
            case 0x59: Bit(3, _register.C); break;
            case 0x5A: Bit(3, _register.D); break;
            case 0x5B: Bit(3, _register.E); break;
            case 0x5C: Bit(3, _register.H); break;
            case 0x5D: Bit(3, _register.L); break;
            case 0x5E: Bit(3, _mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x5F: Bit(3, _register.A); break;

            case 0x60: Bit(4, _register.B); break;
            case 0x61: Bit(4, _register.C); break;
            case 0x62: Bit(4, _register.D); break;
            case 0x63: Bit(4, _register.E); break;
            case 0x64: Bit(4, _register.H); break;
            case 0x65: Bit(4, _register.L); break;
            case 0x66: Bit(4, _mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x67: Bit(4, _register.A); break;

            case 0x68: Bit(5, _register.B); break;
            case 0x69: Bit(5, _register.C); break;
            case 0x6A: Bit(5, _register.D); break;
            case 0x6B: Bit(5, _register.E); break;
            case 0x6C: Bit(5, _register.H); break;
            case 0x6D: Bit(5, _register.L); break;
            case 0x6E: Bit(5, _mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x6F: Bit(5, _register.A); break;

            case 0x70: Bit(6, _register.B); break;
            case 0x71: Bit(6, _register.C); break;
            case 0x72: Bit(6, _register.D); break;
            case 0x73: Bit(6, _register.E); break;
            case 0x74: Bit(6, _register.H); break;
            case 0x75: Bit(6, _register.L); break;
            case 0x76: Bit(6, _mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x77: Bit(6, _register.A); break;

            case 0x78: Bit(7, _register.B); break;
            case 0x79: Bit(7, _register.C); break;
            case 0x7A: Bit(7, _register.D); break;
            case 0x7B: Bit(7, _register.E); break;
            case 0x7C: Bit(7, _register.H); break;
            case 0x7D: Bit(7, _register.L); break;
            case 0x7E: Bit(7, _mmu.ReadByte(_register.HL)); MachineCycles(1); break;
            case 0x7F: Bit(7, _register.A); break;

            case 0x80: _register.B = Res(0, _register.B); break;
            case 0x81: _register.C = Res(0, _register.C); break;
            case 0x82: _register.D = Res(0, _register.D); break;
            case 0x83: _register.E = Res(0, _register.E); break;
            case 0x84: _register.H = Res(0, _register.H); break;
            case 0x85: _register.L = Res(0, _register.L); break;
            case 0x86:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Res(0, v));
                    MachineCycles(1);
                }
                break;
            case 0x87: _register.A = Res(0, _register.A); break;

            case 0x88: _register.B = Res(1, _register.B); break;
            case 0x89: _register.C = Res(1, _register.C); break;
            case 0x8A: _register.D = Res(1, _register.D); break;
            case 0x8B: _register.E = Res(1, _register.E); break;
            case 0x8C: _register.H = Res(1, _register.H); break;
            case 0x8D: _register.L = Res(1, _register.L); break;
            case 0x8E:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Res(1, v));
                    MachineCycles(1);
                }
                break;
            case 0x8F: _register.A = Res(1, _register.A); break;

            case 0x90: _register.B = Res(2, _register.B); break;
            case 0x91: _register.C = Res(2, _register.C); break;
            case 0x92: _register.D = Res(2, _register.D); break;
            case 0x93: _register.E = Res(2, _register.E); break;
            case 0x94: _register.H = Res(2, _register.H); break;
            case 0x95: _register.L = Res(2, _register.L); break;
            case 0x96:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Res(2, v));
                    MachineCycles(1);
                }
                break;
            case 0x97: _register.A = Res(2, _register.A); break;

            case 0x98: _register.B = Res(3, _register.B); break;
            case 0x99: _register.C = Res(3, _register.C); break;
            case 0x9A: _register.D = Res(3, _register.D); break;
            case 0x9B: _register.E = Res(3, _register.E); break;
            case 0x9C: _register.H = Res(3, _register.H); break;
            case 0x9D: _register.L = Res(3, _register.L); break;
            case 0x9E:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Res(3, v));
                    MachineCycles(1);
                }
                break;
            case 0x9F: _register.A = Res(3, _register.A); break;

            case 0xA0: _register.B = Res(4, _register.B); break;
            case 0xA1: _register.C = Res(4, _register.C); break;
            case 0xA2: _register.D = Res(4, _register.D); break;
            case 0xA3: _register.E = Res(4, _register.E); break;
            case 0xA4: _register.H = Res(4, _register.H); break;
            case 0xA5: _register.L = Res(4, _register.L); break;
            case 0xA6:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Res(4, v));
                    MachineCycles(1);
                }
                break;
            case 0xA7: _register.A = Res(4, _register.A); break;

            case 0xA8: _register.B = Res(5, _register.B); break;
            case 0xA9: _register.C = Res(5, _register.C); break;
            case 0xAA: _register.D = Res(5, _register.D); break;
            case 0xAB: _register.E = Res(5, _register.E); break;
            case 0xAC: _register.H = Res(5, _register.H); break;
            case 0xAD: _register.L = Res(5, _register.L); break;
            case 0xAE:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Res(5, v));
                    MachineCycles(1);
                }
                break;
            case 0xAF: _register.A = Res(5, _register.A); break;

            case 0xB0: _register.B = Res(6, _register.B); break;
            case 0xB1: _register.C = Res(6, _register.C); break;
            case 0xB2: _register.D = Res(6, _register.D); break;
            case 0xB3: _register.E = Res(6, _register.E); break;
            case 0xB4: _register.H = Res(6, _register.H); break;
            case 0xB5: _register.L = Res(6, _register.L); break;
            case 0xB6:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Res(6, v));
                    MachineCycles(1);
                }
                break;
            case 0xB7: _register.A = Res(6, _register.A); break;

            case 0xB8: _register.B = Res(7, _register.B); break;
            case 0xB9: _register.C = Res(7, _register.C); break;
            case 0xBA: _register.D = Res(7, _register.D); break;
            case 0xBB: _register.E = Res(7, _register.E); break;
            case 0xBC: _register.H = Res(7, _register.H); break;
            case 0xBD: _register.L = Res(7, _register.L); break;
            case 0xBE:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Res(7, v));
                    MachineCycles(1);
                }
                break;
            case 0xBF: _register.A = Res(7, _register.A); break;

            case 0xC0: _register.B = Set(0, _register.B); break;
            case 0xC1: _register.C = Set(0, _register.C); break;
            case 0xC2: _register.D = Set(0, _register.D); break;
            case 0xC3: _register.E = Set(0, _register.E); break;
            case 0xC4: _register.H = Set(0, _register.H); break;
            case 0xC5: _register.L = Set(0, _register.L); break;
            case 0xC6:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Set(0, v));
                    MachineCycles(1);
                }
                break;
            case 0xC7: _register.A = Set(0, _register.A); break;

            case 0xC8: _register.B = Set(1, _register.B); break;
            case 0xC9: _register.C = Set(1, _register.C); break;
            case 0xCA: _register.D = Set(1, _register.D); break;
            case 0xCB: _register.E = Set(1, _register.E); break;
            case 0xCC: _register.H = Set(1, _register.H); break;
            case 0xCD: _register.L = Set(1, _register.L); break;
            case 0xCE:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Set(1, v));
                    MachineCycles(1);
                }
                break;
            case 0xCF: _register.A = Set(1, _register.A); break;

            case 0xD0: _register.B = Set(2, _register.B); break;
            case 0xD1: _register.C = Set(2, _register.C); break;
            case 0xD2: _register.D = Set(2, _register.D); break;
            case 0xD3: _register.E = Set(2, _register.E); break;
            case 0xD4: _register.H = Set(2, _register.H); break;
            case 0xD5: _register.L = Set(2, _register.L); break;
            case 0xD6:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Set(2, v));
                    MachineCycles(1);
                }
                break;
            case 0xD7: _register.A = Set(2, _register.A); break;

            case 0xD8: _register.B = Set(3, _register.B); break;
            case 0xD9: _register.C = Set(3, _register.C); break;
            case 0xDA: _register.D = Set(3, _register.D); break;
            case 0xDB: _register.E = Set(3, _register.E); break;
            case 0xDC: _register.H = Set(3, _register.H); break;
            case 0xDD: _register.L = Set(3, _register.L); break;
            case 0xDE:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Set(3, v));
                    MachineCycles(1);
                }
                break;
            case 0xDF: _register.A = Set(3, _register.A); break;

            case 0xE0: _register.B = Set(4, _register.B); break;
            case 0xE1: _register.C = Set(4, _register.C); break;
            case 0xE2: _register.D = Set(4, _register.D); break;
            case 0xE3: _register.E = Set(4, _register.E); break;
            case 0xE4: _register.H = Set(4, _register.H); break;
            case 0xE5: _register.L = Set(4, _register.L); break;
            case 0xE6:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Set(4, v));
                    MachineCycles(1);
                }
                break;
            case 0xE7: _register.A = Set(4, _register.A); break;

            case 0xE8: _register.B = Set(5, _register.B); break;
            case 0xE9: _register.C = Set(5, _register.C); break;
            case 0xEA: _register.D = Set(5, _register.D); break;
            case 0xEB: _register.E = Set(5, _register.E); break;
            case 0xEC: _register.H = Set(5, _register.H); break;
            case 0xED: _register.L = Set(5, _register.L); break;
            case 0xEE:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Set(5, v));
                    MachineCycles(1);
                }
                break;
            case 0xEF: _register.A = Set(5, _register.A); break;

            case 0xF0: _register.B = Set(6, _register.B); break;
            case 0xF1: _register.C = Set(6, _register.C); break;
            case 0xF2: _register.D = Set(6, _register.D); break;
            case 0xF3: _register.E = Set(6, _register.E); break;
            case 0xF4: _register.H = Set(6, _register.H); break;
            case 0xF5: _register.L = Set(6, _register.L); break;
            case 0xF6:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Set(6, v));
                    MachineCycles(1);
                }
                break;
            case 0xF7: _register.A = Set(6, _register.A); break;

            case 0xF8: _register.B = Set(7, _register.B); break;
            case 0xF9: _register.C = Set(7, _register.C); break;
            case 0xFA: _register.D = Set(7, _register.D); break;
            case 0xFB: _register.E = Set(7, _register.E); break;
            case 0xFC: _register.H = Set(7, _register.H); break;
            case 0xFD: _register.L = Set(7, _register.L); break;
            case 0xFE:
                {
                    byte v = _mmu.ReadByte(_register.HL);
                    MachineCycles(1);
                    _mmu.WriteByte(_register.HL, Set(7, v));
                    MachineCycles(1);
                }
                break;
            case 0xFF: _register.A = Set(7, _register.A); break;
        }
    }
}