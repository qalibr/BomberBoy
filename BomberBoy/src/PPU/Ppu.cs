using BomberBoy.src.CPU;
using BomberBoy.src.MMU;
using BomberBoy.src.Util;

namespace BomberBoy.src.PPU;

// https://gbdev.io/pandocs/Rendering.html
public class Ppu
{
    private readonly Mmu _mmu;
    private readonly Interrupts _interrupt;

    // Screen dimensions.
    public const int SCREEN_WIDTH = 160;
    public const int SCREEN_HEIGHT = 144;

    // Scanline timings in T-cycles.
    private const int OAM_SCAN_CYCLES = 80;
    private const int VRAM_READ_CYCLES = 172;
    private const int HBLANK_CYCLES = 204;
    private const int CYCLES_PER_SCANLINE = OAM_SCAN_CYCLES + VRAM_READ_CYCLES + HBLANK_CYCLES; // 456

    private const int VBLANK_SCANLINES = 10;
    private const int TOTAL_SCANLINES = SCREEN_HEIGHT + VBLANK_SCANLINES; // 154

    private int _scanlineCounter = 0;
    private int _windowLineCounter = 0;
    private int _mode3ExtraCycles = 0;
    private readonly List<Sprite> _spriteBuffer = new(10);
    private bool _lastStatSignal = false;
    public readonly int[] FrameBuffer = new int[SCREEN_WIDTH * SCREEN_HEIGHT];

    // These hex values are larger than a signed int can hold, so they are inferred as uint.
    // We must explicitly cast them to int to match the array type.
    // The bit pattern is what matters for the color, which is preserved by the cast.
    private readonly int[] _colors = unchecked([(int)0xFFFFFFFF, (int)0xFFD3D3D3, (int)0xFF888888, (int)0xFF000000]);

    public byte CurrentMode => GetCurrentMode();

    public Ppu(Mmu mmu, Interrupts interrupt)
    {
        _mmu = mmu;
        _interrupt = interrupt;
    }

    // Opting for a scanline based renderer instead of 'Pixel FIFO', the PPU state machine below
    // is driven by the scanline counter. Each scanline takes 456 T-cycles.
    // 
    // The PPU cycles through modes for each of the 144 (screen height) visible scanlines:
    // Mode 2 (OAM Scan, 80 cycles): Scans OAM (FE00-FE9F) for up to 10 sprites on the current line.
    // Mode 3 (Drawing, ~172-289 cycles): Reads VRAM to draw the pixels for the scanline. Duration varies.
    // Mode 0 (H-Blank, ~87-204 cycles): Horizontal blanking period. CPU can access VRAM and OAM.
    //
    // After the 144 visible scanlines, the PPU enters V-Blank for 10 scanlines (144 to 153):
    // Mode 1 (V-Blank, 4560 cycles): Vertical blanking period. CPU has full access to VRAM and OAM.
    // A V-Blank interrupt is requested at the start of this mode.
    public void Tick()
    {
        if (IsLcdEnabled())
        {
            _scanlineCounter++;

            switch (GetCurrentMode())
            {
                case 2: // Mode 2: OAM Scan
                    if (_scanlineCounter >= OAM_SCAN_CYCLES)
                    {
                        // During Mode 2, the PPU scans OAM for sprites on the current scanline.
                        // We find up to 10 sprites and store them for rendering in Mode 3.
                        PopulateSpriteBuffer();

                        // The duration of Mode 3 is not fixed. It depends on the number of sprites
                        // on the scanline, as fetching sprite data "steals" cycles from the drawing process.
                        // This is a simplified model of that penalty.
                        _mode3ExtraCycles = _spriteBuffer.Count * 6;

                        SetCurrentMode(3);
                    }
                    break;
                case 3: // Mode 3: Drawing
                    // The duration of Mode 3 also varies slightly based on the SCX (Scroll X) register,
                    // as it affects when the PPU starts fetching background pixels.
                    int totalMode3Time = VRAM_READ_CYCLES + (_mmu.SCX % 8) + _mode3ExtraCycles;
                    if (_scanlineCounter >= OAM_SCAN_CYCLES + totalMode3Time)
                    {
                        SetCurrentMode(0);
                        RenderScanline();
                    }
                    break;
                case 0: // Mode 0: H-Blank
                    if (_scanlineCounter >= CYCLES_PER_SCANLINE)
                    {
                        _scanlineCounter = 0;
                        _mmu.LY++;

                        if (_mmu.LY == SCREEN_HEIGHT)
                        {
                            // After the last visible scanline is drawn, we enter V-Blank (Mode 1).
                            SetCurrentMode(1);
                            _interrupt.RequestInterrupt(Interrupts.InterruptType.VBLANK);
                        }
                        else
                        {
                            SetCurrentMode(2);
                        }
                    }
                    break;
                case 1: // Mode 1: V-Blank
                    if (_scanlineCounter >= CYCLES_PER_SCANLINE)
                    {
                        _scanlineCounter = 0;
                        _mmu.LY++;

                        if (_mmu.LY == TOTAL_SCANLINES)
                        {
                            // After 10 lines of V-Blank, the frame is over. Reset LY and start a new frame.
                            _mmu.LY = 0;
                            _windowLineCounter = 0;
                            SetCurrentMode(2);
                        }
                    }
                    break;
            }
        }
        else
        {
            // When the LCD is disabled (LCDC bit 7 is 0), the PPU is effectively reset.
            // The scanline counter and LY register are cleared, and the mode is set to 0.
            _scanlineCounter = 0;
            _mmu.LY = 0;
            SetCurrentMode(0);
            _windowLineCounter = 0;
        }

        // Update flag first, then update the STAT interrupt.
        CheckLycCoincidence();
        UpdateStatInterrupt();
    }

    private void RenderScanline()
    {
        // LCDC bit 0 is a master switch for the background and window. If it's off,
        // they are not drawn, effectively becoming transparent (color 0).
        bool bgWindowMasterSwitch = BitFunctions.IsBit(0, _mmu.LCDC);

        if (bgWindowMasterSwitch)
        {
            RenderBackgroundLine();
        }
        else // If master switch is off, BG and Window are blank.
        {
            int y = _mmu.LY;
            int canvasOffset = y * SCREEN_WIDTH;
            // "Blank" means filled with color 0 from the BGP palette.
            // Color 0 is not necessarily white, it's the first color in the BGP palette
            int paletteColorId = (_mmu.BGP >> 0) & 0x03;
            int color = _colors[paletteColorId];
            Array.Fill(FrameBuffer, color, canvasOffset, SCREEN_WIDTH);
        }

        // The window has its own enable bit (LCDC bit 5) and is drawn on top of the background.
        if (bgWindowMasterSwitch && IsWindow())
        {
            RenderWindowLine();
            _windowLineCounter++;
        }

        // Sprites (OBJ) also have their own enable bit (LCDC bit 1).
        // Render Sprites
        if (BitFunctions.IsBit(1, _mmu.LCDC)) // OBJ Display Enable
        {
            RenderSpriteLine();
        }
    }

    private void RenderBackgroundLine()
    {
        int y = _mmu.LY;

        int canvasOffset = y * SCREEN_WIDTH;

        for (int x = 0; x < SCREEN_WIDTH; x++)
        {
            int colorId = GetColorIdFromVram(x, y);

            FrameBuffer[canvasOffset + x] = _colors[colorId];
        }
    }

    private ushort GetTileDataAddress(byte tileIndex)
    {
        // There are two addressing modes for tiles:
        // 1. "8000 method": Tiles 0-255 are at 0x8000-0x8FFF.
        // 2. "8800 method": Tiles 0-127 are at 0x9000-0x97FF,
        //    but tiles 128-255 (treated as signed -128 to -1) are at 0x8800-0x8FFF.
        if (BitFunctions.IsBit(4, _mmu.LCDC))
        {
            return (ushort)(0x8000 + (tileIndex * 16));
        }
        else
        {
            // By casting to a signed sbyte, 128-255 become -128 to -1.
            // 0x9000 + (-128 * 16) = 0x8800.
            return (ushort)(0x9000 + ((sbyte)tileIndex * 16));
        }
    }

    // For a given (x, y) pixel on the screen, this function finds the corresponding
    // color from the background map.
    private int GetColorIdFromVram(int x, int y)
    {
        // 1. Determine which tile map to use (0x9800 or 0x9C00) from LCDC bit 3.
        ushort tileMapBase = BitFunctions.IsBit(3, _mmu.LCDC) ? (ushort)0x9C00 : (ushort)0x9800;

        // 2. Find the coordinates on the 256x256 background map, accounting for scroll registers.
        byte bgX = (byte)((x + _mmu.SCX) & 0xFF);
        byte bgY = (byte)((y + _mmu.SCY) & 0xFF);

        // 3. Calculate which tile in the 32x32 tile map corresponds to the bg coordinates.
        ushort tileMapX = (ushort)(bgX / 8);
        ushort tileMapY = (ushort)(bgY / 8);
        ushort tileMapOffset = (ushort)(tileMapY * 32 + tileMapX);
        ushort tileIndexAddress = (ushort)(tileMapBase + tileMapOffset);

        // 4. Get the tile index from the map. This index identifies which tile pattern to use.
        byte tileIndex = PpuReadByte(tileIndexAddress);

        // 5. Find where the tile's graphical data is stored using the addressing mode from LCDC bit 4.
        ushort tileAddress = GetTileDataAddress(tileIndex);

        // 6. Within the 8x8 tile, find which vertical line of pixels we need.
        //    Each line is 2 bytes.
        byte yInTile = (byte)(bgY % 8);
        ushort tileLineAddress = (ushort)(tileAddress + (yInTile * 2));
        byte byte1 = PpuReadByte(tileLineAddress);
        byte byte2 = PpuReadByte((ushort)(tileLineAddress + 1));

        // 7. Within the tile line, find which horizontal pixel we need.
        //    The two bytes combine to form the color ID for each of the 8 pixels.
        //    byte2 = msb, byte1 = lsb
        //    Pixel 0: Bit 7 of byte2 and byte1
        //    Pixel 1: Bit 6 of byte2 and byte1
        //    ...
        //    Pixel 7: Bit 0 of byte2 and byte1
        byte xInTile = (byte)(bgX % 8);
        int bitIndex = 7 - xInTile; // Bits are stored high-to-low (pixel 0 is bit 7).
        int lsb = (byte1 >> bitIndex) & 1;
        int msb = (byte2 >> bitIndex) & 1;
        int colorId = (msb << 1) | lsb; // A 2-bit value (0-3).

        // 8. Map the 2-bit color ID to a 2-bit palette index using the BGP register.
        //    BGP layout: [Color3][Color2][Color1][Color0]
        //    e.g., if colorId is 2, we want bits 5-4 of BGP.
        int paletteColorId = (_mmu.BGP >> (colorId * 2)) & 0x03;

        return paletteColorId;
    }

    private void RenderWindowLine()
    {
        // The WX register specifies the X position of the window plus 7.
        // To get the actual starting X coordinate, we subtract 7.
        int wx = _mmu.WX - 7; // WX is X-coord + 7
        int y = _mmu.LY; // WY is Y-coord.

        // The window has its own tile map selection bit, separate from the background's.
        // LCDC bit 6 selects the window tile map.
        ushort tileMapBase = BitFunctions.IsBit(6, _mmu.LCDC) ? (ushort)0x9C00 : (ushort)0x9800;

        int windowY = _windowLineCounter; // The vertical line inside the window's 256x256 pixel map.

        for (int x = 0; x < SCREEN_WIDTH; x++)
        {
            if (x < wx) continue; // Is the current screen pixel part of the window?

            int windowX = x - wx; // The horizontal pixel inside the window.

            ushort tileMapX = (ushort)(windowX / 8);
            ushort tileMapY = (ushort)(windowY / 8);
            ushort tileMapOffset = (ushort)(tileMapY * 32 + tileMapX);
            ushort tileIndexAddress = (ushort)(tileMapBase + tileMapOffset); // Find which tile in the 32x32 map we are in.

            byte tileIndex = PpuReadByte(tileIndexAddress);

            ushort tileAddress = GetTileDataAddress(tileIndex);

            // Find which vertical line of pixels in the tile we need
            byte yInTile = (byte)(windowY % 8);
            ushort tileLineAddress = (ushort)(tileAddress + (yInTile * 2));
            byte byte1 = PpuReadByte(tileLineAddress);
            byte byte2 = PpuReadByte((ushort)(tileLineAddress + 1));

            // Find which horizontal pixel in the tile we need
            byte xInTile = (byte)(windowX % 8);
            int bitIndex = 7 - xInTile; // Stored right-to-left
            int lsb = (byte1 >> bitIndex) & 1;
            int msb = (byte2 >> bitIndex) & 1;
            int colorId = (msb << 1) | lsb;

            // Map the color ID through the background palette register (BGP)
            int paletteColorId = (_mmu.BGP >> (colorId * 2)) & 0x03;

            int canvasOffset = y * SCREEN_WIDTH;
            FrameBuffer[canvasOffset + x] = _colors[paletteColorId];
        }
    }

    private struct Sprite
    {
        public byte Y;
        public byte X;
        public byte TileIndex;
        public byte Attributes;
        public byte OamIndex;
    }

    private void RenderSpriteLine()
    {
        if (_spriteBuffer.Count == 0) return;

        int ySize = SpriteSize();
        int scanline = _mmu.LY;

        // Sprites are rendered back-to-front to handle priority correctly.
        // The buffer is pre-sorted by X-coordinate, then OAM index. By iterating
        // backwards, we draw lower-priority sprites first, allowing higher-priority
        // sprites (those with smaller X or OAM index) to be drawn over them.
        for (int i = _spriteBuffer.Count - 1; i >= 0; i--)
        {
            var sprite = _spriteBuffer[i];
            byte attributes = sprite.Attributes;
            bool yFlip = IsYFlipped(attributes);
            bool xFlip = IsXFlipped(attributes);
            bool bgPriority = BitFunctions.IsBit(7, attributes);

            // Calculate which line of the sprite's tile to render.
            int line = scanline - sprite.Y;
            if (yFlip)
            {
                line = ySize - 1 - line;
            }

            byte tileIndex = sprite.TileIndex;

            // In 8x16 sprite mode, the sprite is composed of two adjacent tiles.
            // The least significant bit of the tile index is ignored, and the hardware
            // selects tile N (for the top 8 pixels) and tile N+1 (for the bottom 8 pixels).
            if (ySize == 16)
            {
                tileIndex &= 0xFE;
                if (line >= 8)
                {
                    tileIndex |= 0x01;
                    line -= 8;
                }
            }

            // Sprite tile data is always fetched from the 0x8000-0x8FFF region.
            ushort tileLineAddress = (ushort)(0x8000 + (tileIndex * 16) + (line * 2));
            byte data1 = _mmu.PpuRead(tileLineAddress);
            byte data2 = _mmu.PpuRead((ushort)(tileLineAddress + 1));

            for (int tilePixel = 0; tilePixel < 8; tilePixel++)
            {
                int screenX = sprite.X + tilePixel;
                if (screenX < 0 || screenX >= SCREEN_WIDTH) continue;

                int colorBit = xFlip ? tilePixel : 7 - tilePixel;
                int lsb = (data1 >> colorBit) & 1;
                int msb = (data2 >> colorBit) & 1;
                int colorId = (msb << 1) | lsb;

                if (IsTransparent(colorId)) continue;

                int canvasOffset = scanline * SCREEN_WIDTH;

                // BG-to-OBJ Priority (Bit 7 of attributes)
                // If this bit is 1, the sprite pixel is only drawn if the background
                // pixel at this location is color 0 (the transparent color for the BG).
                if (bgPriority)
                {
                    // Get the actual color value for the background's color 0 from BGP.
                    int bgPaletteColor0 = (_mmu.BGP >> 0) & 0x03;
                    int bgColor0 = _colors[bgPaletteColor0];
                    // If the framebuffer pixel is not BG color 0, the BG "wins" and we skip drawing the sprite pixel.
                    if (FrameBuffer[canvasOffset + screenX] != bgColor0) continue;
                }

                byte paletteRegister = BitFunctions.IsBit(4, attributes) ? _mmu.OBP1 : _mmu.OBP0;
                int paletteColorId = (paletteRegister >> (colorId * 2)) & 0x03;

                FrameBuffer[canvasOffset + screenX] = _colors[paletteColorId];
            }
        }
    }

    private void PopulateSpriteBuffer()
    {
        _spriteBuffer.Clear();
        int ySize = SpriteSize();
        int scanline = _mmu.LY;

        // The PPU can only process a maximum of 10 sprites per scanline.
        // It scans OAM (0xFE00-0xFE9F) and picks the first 10 it finds that
        // are visible on the current line.
        for (int i = 0; i < 40 && _spriteBuffer.Count < 10; i++)
        {
            ushort oamAddr = (ushort)(0xFE00 + (i * 4));

            // Sprite Y position in OAM is the actual Y coordinate + 16.
            // This is to allow sprites to be positioned partially off-screen at the top.
            byte spriteY = (byte)(_mmu.PpuRead(oamAddr) - 16);

            if (scanline >= spriteY && scanline < (spriteY + ySize))
            {
                _spriteBuffer.Add(new Sprite
                {
                    Y = spriteY,
                    // Sprite X position in OAM is the actual X coordinate + 8.
                    X = (byte)(_mmu.PpuRead((ushort)(oamAddr + 1)) - 8),
                    TileIndex = _mmu.PpuRead((ushort)(oamAddr + 2)),
                    Attributes = _mmu.PpuRead((ushort)(oamAddr + 3)),
                    OamIndex = (byte)i
                });
            }
        }

        // Sprite priority is determined first by the X-coordinate (smaller X has higher priority).
        // If X-coordinates are equal, the sprite that appears earlier in OAM (lower index) 
        // has higher priority.
        _spriteBuffer.Sort((s1, s2) =>
        {
            int xCompare = s1.X.CompareTo(s2.X);
            return xCompare != 0 ? xCompare : s1.OamIndex.CompareTo(s2.OamIndex);
        });
    }

    private byte PpuReadByte(ushort addr)
    {
        byte data = _mmu.ReadByte(addr);
        return data;
    }

    private int SpriteSize()
    {
        return BitFunctions.IsBit(2, _mmu.LCDC) ? 16 : 8;
    }

    private bool IsXFlipped(int attr)
    {
        return BitFunctions.IsBit(5, attr);
    }

    private bool IsYFlipped(int attr)
    {
        return BitFunctions.IsBit(6, attr);
    }

    private bool IsTransparent(int value)
    {
        return value == 0;
    }

    private bool IsWindow()
    {
        // The window is only considered "active" on a scanline if its master switch is on,
        // its Y position is at or above the current scanline, and its X position is not completely off-screen to the right.
        return BitFunctions.IsBit(5, _mmu.LCDC) && _mmu.WY <= _mmu.LY && _mmu.WX < 167;
    }

    private bool IsLcdEnabled()
    {
        return BitFunctions.IsBit(7, _mmu.LCDC);
    }

    private byte GetCurrentMode() => (byte)(_mmu.STAT & 0x3);

    private void SetCurrentMode(byte mode) => _mmu.STAT = (byte)((_mmu.STAT & 0b11111100) | mode);

    private void CheckLycCoincidence()
    {
        // Simply update the bit in the STAT register.
        // The UpdateStatInterrupt method will see this and trigger if bit 6 is set.
        if (_mmu.LY == _mmu.LYC)
        {
            _mmu.STAT = BitFunctions.BitSet(2, _mmu.STAT);
        }
        else
        {
            _mmu.STAT = BitFunctions.BitClear(2, _mmu.STAT);
        }
    }

    [Flags]
    private enum StatInterruptSource : byte
    {
        HBLANK = 1 << 3, // Mode 0
        VBLANK = 1 << 4, // Mode 1
        OAM = 1 << 5,    // Mode 2
        LYC = 1 << 6     // LY=LYC
    }

    private void UpdateStatInterrupt()
    {
        // The STAT Interrupt line is a single 'OR' gate connected 
        // to all enabled sources. If the line is already "High" because of an 
        // H-Blank, and an LYC coincidence happens, the line stays "High."
        // Because the CPU only triggers on a "Rising Edge" (0 to 1), the 
        // second interrupt would be missed. This is the "STAT Blocking" behavior.

        // 1. Check which interrupt sources are enabled in the STAT register.
        bool lycInterruptEnabled = BitFunctions.IsBit(6, _mmu.STAT);   // Bit 6: LYC=LY Interrupt Enable
        bool mode2InterruptEnabled = BitFunctions.IsBit(5, _mmu.STAT); // Bit 5: Mode 2 OAM Interrupt Enable
        bool mode1InterruptEnabled = BitFunctions.IsBit(4, _mmu.STAT); // Bit 4: Mode 1 V-Blank Interrupt Enable
        bool mode0InterruptEnabled = BitFunctions.IsBit(3, _mmu.STAT); // Bit 3: Mode 0 H-Blank Interrupt Enable

        // 2. Check if the conditions for any of those sources are currently met.
        bool lycCondition = lycInterruptEnabled && BitFunctions.IsBit(2, _mmu.STAT);
        bool mode2Condition = mode2InterruptEnabled && (GetCurrentMode() == 2);
        bool mode1Condition = mode1InterruptEnabled && (GetCurrentMode() == 1);
        bool mode0Condition = mode0InterruptEnabled && (GetCurrentMode() == 0);

        // 3. Combine them to get the current state of the STAT interrupt signal.
        bool currentSignal = lycCondition || mode2Condition || mode1Condition || mode0Condition;

        // 4. A STAT interrupt is requested on the rising edge of this signal.
        //    If the signal is high now, but was low on the last PPU tick, request the interrupt.
        if (currentSignal && !_lastStatSignal)
        {
            _interrupt.RequestInterrupt(Interrupts.InterruptType.LCD_STAT);
        }

        // 5. Remember the current signal state for the next tick's rising-edge check.
        _lastStatSignal = currentSignal;
    }

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------

    public void SaveState(BinaryWriter writer)
    {
        writer.Write(_scanlineCounter);
        writer.Write(_windowLineCounter);
        writer.Write(_mode3ExtraCycles);
        writer.Write(_lastStatSignal);
    }

    public void LoadState(BinaryReader reader)
    {
        _scanlineCounter = reader.ReadInt32();
        _windowLineCounter = reader.ReadInt32();
        _mode3ExtraCycles = reader.ReadInt32();
        _lastStatSignal = reader.ReadBoolean();
    }
}