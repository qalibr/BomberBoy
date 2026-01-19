using BomberBoy.src.CPU;
using BomberBoy.src.MMU;
using Raylib_cs;

/*
    https://gbdev.io/pandocs/Joypad_Input.html
    D-PAD:
    UP:             W
    DOWN:           S
    RIGHT:          D
    LEFT:           A

    A:              E
    B:              R

    START:          F
    SELECT:         Z

    SAVE STATE:     CTRL + T
    RESTORE STATE:  CTRL + L
*/
namespace BomberBoy.src;

public class Joypad
{
    private readonly Interrupts _interrupts;
    private Mmu _mmu = null!;

    // Button states (true = pressed)
    private bool _up, _down, _left, _right;
    private bool _a, _b, _start, _select;

    public Joypad(Interrupts interrupts)
    {
        _interrupts = interrupts;
    }

    public void ConnectMmu(Mmu mmu)
    {
        _mmu = mmu;
        // Initialize the P1 register state in the MMU.
        // Bits 4 and 5 high = no buttons selected.
        _mmu.Io[0] = 0b00110000;
    }

    // P1/JOYP register at 0xFF00.
    public byte P1
    {
        get
        {
            // Read the selection bits from the MMU's IO array.
            byte selection = (byte)(_mmu.Io[0] & 0b00110000);

            // Set the unused upper bits (6 and 7) and the button/direction bits (3, 2, 1, and 0)
            // to 1 (unpressed).
            byte result = (byte)(selection | 0b11001111);

            // Bit 5 selects Action buttons. 0 = selected.
            if ((selection & 0b00100000) == 0)
            {
                if (_a) result &= 0b11111110;       // Bit 0
                if (_b) result &= 0b11111101;       // Bit 1
                if (_select) result &= 0b11111011;  // Bit 2
                if (_start) result &= 0b11110111;   // Bit 3
            }

            // Bit 4 selects Direction buttons. 0 = selected.
            if ((selection & 0b00010000) == 0)
            {
                if (_right) result &= 0b11111110; // Bit 0
                if (_left) result &= 0b11111101;  // Bit 1
                if (_up) result &= 0b11111011;    // Bit 2
                if (_down) result &= 0b11110111;  // Bit 3
            }

            return result;
        }
        set
        {
            // Game writes to P1 to select button group. Only bits 4 and 5 are writable.
            // We update the selection bits in the MMU's IO array.
            _mmu.Io[0] = (byte)((_mmu.Io[0] & 0xCF) | (value & 0x30));
        }
    }

    /// <summary>
    /// Polls keyboard for input and updates button states.
    /// Requests a Joypad interrupt if a button is pressed.
    /// </summary>
    public void HandleInput()
    {
        // Store previous state to detect changes
        bool prev_up = _up, prev_down = _down, prev_left = _left, prev_right = _right;
        bool prev_a = _a, prev_b = _b, prev_start = _start, prev_select = _select;

        // D-PAD
        _up = Raylib.IsKeyDown(KeyboardKey.W);
        _down = Raylib.IsKeyDown(KeyboardKey.S);
        _left = Raylib.IsKeyDown(KeyboardKey.A);
        _right = Raylib.IsKeyDown(KeyboardKey.D);

        // Action buttons
        _a = Raylib.IsKeyDown(KeyboardKey.E);
        _b = Raylib.IsKeyDown(KeyboardKey.R);

        // Start/Select
        _start = Raylib.IsKeyDown(KeyboardKey.F);
        _select = Raylib.IsKeyDown(KeyboardKey.Z);

        // A joypad interrupt is requested when any of the P10-P13 bits change from 1 to 0 (button pressed).
        bool buttonPressed =
            (_up && !prev_up) ||
            (_down && !prev_down) ||
            (_left && !prev_left) ||
            (_right && !prev_right) ||
            (_a && !prev_a) ||
            (_b && !prev_b) ||
            (_start && !prev_start) ||
            (_select && !prev_select);

        if (buttonPressed)
        {
            _interrupts.RequestInterrupt(Interrupts.InterruptType.JOYPAD);
        }
    }
}