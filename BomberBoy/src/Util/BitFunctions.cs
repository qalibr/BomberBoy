namespace BomberBoy.src.Util;

public static class BitFunctions
{
    public static byte BitSet(byte bit, byte value)
    {
        return value |= (byte)(1 << bit);
    }

    public static byte BitClear(int bit, byte value)
    {
        return value &= (byte)~(1 << bit);
    }

    public static bool IsBit(int bit, int value)
    {
        return ((value >> bit) & 1) == 1;
    }
}