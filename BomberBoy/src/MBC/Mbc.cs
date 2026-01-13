using BomberBoy.src.PAK;

namespace BomberBoy.src.MBC;

public static class Mbc
{
    public static IMbc CreateMbc(Pak pak, int eramSize)
    {
        switch (pak.data[0x147])
        {
            case 0x00:
                return new Mbc0(pak);
            case 0x01:
            case 0x02:
            case 0x03:
                return new Mbc1(pak, eramSize);
            default:
                throw new NotSupportedException($"MBC type {pak.data[0x147]:X2} is not supported.");
        }
    }
}