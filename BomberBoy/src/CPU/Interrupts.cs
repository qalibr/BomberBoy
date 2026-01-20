using BomberBoy.src.MMU;

namespace BomberBoy.src.CPU;

public class Interrupts(Emulator emulator, Mmu mmu, Registers regs)
{
    private readonly Emulator _emulator = emulator;
    private readonly Mmu _mmu = mmu;
    private readonly Registers _regs = regs;

    public bool delayInterruptCheck = false;

    public enum InterruptType : byte
    {
        VBLANK = 0b00001,
        LCD_STAT = 0b00010,
        TIMER = 0b00100,
        SERIAL = 0b01000,
        JOYPAD = 0b10000,
    }

    private void Push(ushort value)
    {
        // The execution is: internal delay, write high, write low.
        MachineCycles(1);

        // The stack grows downwards. High byte is pushed first.
        _mmu.WriteByte(--_regs.SP, (byte)(value >> 8));
        MachineCycles(1);

        // Then the low byte.
        _mmu.WriteByte(--_regs.SP, (byte)(value & 0xFF));
        MachineCycles(1);
    }

    public void MachineCycles(int c)
    {
        _emulator.MachineCycles(c);
    }

    public void RequestInterrupt(InterruptType interrupt)
    {
        byte currentIF = _mmu.ReadByte(0xFF0F);
        _mmu.WriteByte(0xFF0F, (byte)(currentIF | (byte)interrupt));
    }

    private void ServiceInterrupt(InterruptType interrupt, ref bool IME)
    {
        // An interrupt service routine takes 5 machine cycles.
        // 2 wait cycles, then 3 for the push.
        MachineCycles(2);

        // Reset IME - Clear the corresponding IF flag bit - Push - Jump

        IME = false;

        byte currentIF = _mmu.ReadByte(0xFF0F);
        _mmu.WriteByte(0xFF0F, (byte)(currentIF & ~(byte)interrupt));

        Push(_regs.PC);

        switch (interrupt)
        {
            case InterruptType.VBLANK: _regs.PC = 0x0040; break;
            case InterruptType.LCD_STAT: _regs.PC = 0x0048; break;
            case InterruptType.TIMER: _regs.PC = 0x0050; break;
            case InterruptType.SERIAL: _regs.PC = 0x0058; break;
            case InterruptType.JOYPAD: _regs.PC = 0x0060; break;
        }
    }

    public bool HandleInterrupts(ref bool halted, ref bool IME)
    {
        if (delayInterruptCheck)
        {
            delayInterruptCheck = false;
            return false;
        }

        // Mask for interrupt bits.
        byte pending = (byte)(_mmu.IF & _mmu.IE & 0x1F);

        if (pending == 0) return false;

        // If CPU is halted, an interrupt will wake it up.
        if (halted)
        {
            halted = false;

            if (!IME)
            {
                return false;
            }
        }

        if (IME)
        {
            if ((pending & (byte)InterruptType.VBLANK) != 0)
            {
                ServiceInterrupt(InterruptType.VBLANK, ref IME);
                return true;
            }
            else if ((pending & (byte)InterruptType.LCD_STAT) != 0)
            {
                ServiceInterrupt(InterruptType.LCD_STAT, ref IME);
                return true;
            }
            else if ((pending & (byte)InterruptType.TIMER) != 0)
            {
                ServiceInterrupt(InterruptType.TIMER, ref IME);
                return true;
            }
            else if ((pending & (byte)InterruptType.SERIAL) != 0)
            {
                ServiceInterrupt(InterruptType.SERIAL, ref IME);
                return true;
            }
            else if ((pending & (byte)InterruptType.JOYPAD) != 0)
            {
                ServiceInterrupt(InterruptType.JOYPAD, ref IME);
                return true;
            }
        }

        return false;
    }

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    public void SaveState(BinaryWriter writer)
    {
        writer.Write(delayInterruptCheck);
    }

    public void LoadState(BinaryReader reader)
    {
        delayInterruptCheck = reader.ReadBoolean();
    }
}