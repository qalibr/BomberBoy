using BomberBoy.src.CPU;
using BomberBoy.src.MMU;

namespace BomberBoy.src;

public class Timer
{
    private readonly Interrupts _interrupt;
    private readonly Mmu _mmu;

    private ushort _internalCounter = 0;
    private int _timaCounter = 0;

    public Timer(Interrupts interrupt, Mmu mmu)
    {
        _interrupt = interrupt;
        _mmu = mmu;
    }

    public void Tick()
    {
        // The internal counter is always running at 4.19MHz, but DIV should only increment
        // at 16384 Hz (4.194 MHz / 256). Taking the upper 8 bits ensures this.
        //    _internalCounter   | DIV
        // 00000000 11111111 255 |  0
        // 00000001 00000000 256 |  1
        // 00000001 11111111 511 |  1
        // 00000010 00000000 512 |  2
        _internalCounter++;
        _mmu.DIV = (byte)(_internalCounter >> 8);

        if ((_mmu.TAC & 0x04) != 0) // Check if timer is enabled.
        {
            _timaCounter++;

            int threshold = GetClockThreshold(_mmu.TAC);
            if (_timaCounter >= threshold) // Check if we should reset.
            {
                _timaCounter = 0;

                _mmu.TIMA++;
                if (_mmu.TIMA == 0x00) // Check for overflow.
                {
                    // On overflow, TIMA is reset to the value in TMA.
                    _mmu.TIMA = _mmu.TMA;
                    _interrupt.RequestInterrupt(Interrupts.InterruptType.TIMER);
                }
            }
        }
    }

    // If a ROM writes to the DIV register, reset it and its internal counter.
    public void ResetDivCounter()
    {
        _internalCounter = 0;
        _mmu.DIV = 0;
    }

    private int GetClockThreshold(byte tac) => (tac & 0x03) switch
    {
        0x00 => 1024, // 4096 Hz   (4194304 / 4096)
        0x01 => 16,   // 262144 Hz (4194304 / 262144)
        0x02 => 64,   // 65536 Hz  (4194304 / 65536)
        0x03 => 256,  // 16384 Hz  (4194304 / 16384)
        _ => throw new ArgumentOutOfRangeException(nameof(tac), "Invalid TAC value")
    };
}