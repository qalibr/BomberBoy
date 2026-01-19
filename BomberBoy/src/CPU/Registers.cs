namespace BomberBoy.src.CPU;

public class Registers
{
    public byte A { get; set; }
    public byte F { get; set; }
    public byte B { get; set; }
    public byte C { get; set; }
    public byte D { get; set; }
    public byte E { get; set; }
    public byte H { get; set; }
    public byte L { get; set; }
    public ushort SP { get; set; }
    public ushort PC { get; set; }

    public ushort AF
    {
        get => (ushort)((A << 8) | F);
        set
        {
            A = (byte)(value >> 8);
            F = (byte)(value & 0xF0);
        }
    }

    public ushort BC
    {
        get => (ushort)((B << 8) | C);
        set
        {
            B = (byte)(value >> 8);
            C = (byte)(value & 0xFF);
        }
    }

    public ushort DE
    {
        get => (ushort)((D << 8) | E);
        set
        {
            D = (byte)(value >> 8);
            E = (byte)(value & 0xFF);
        }
    }

    public ushort HL
    {
        get => (ushort)((H << 8) | L);
        set
        {
            H = (byte)(value >> 8);
            L = (byte)(value & 0xFF);
        }
    }

    public bool zFlag
    {
        get => (F & 0x80) == 0x80;
        set => F = (byte)(value ? F | 0x80 : F & ~0x80);
    }

    public bool nFlag
    {
        get => (F & 0x40) == 0x40;
        set => F = (byte)(value ? F | 0x40 : F & ~0x40);
    }

    public bool hFlag
    {
        get => (F & 0x20) == 0x20;
        set => F = (byte)(value ? F | 0x20 : F & ~0x20);
    }

    public bool cFlag
    {
        get => (F & 0x10) == 0x10;
        set => F = (byte)(value ? F | 0x10 : F & ~0x10);
    }

    public void SaveState(BinaryWriter writer)
    {
        writer.Write(A);
        writer.Write(F);
        writer.Write(B);
        writer.Write(C);
        writer.Write(D);
        writer.Write(E);
        writer.Write(H);
        writer.Write(L);
        writer.Write(SP);
        writer.Write(PC);
    }

    public void LoadState(BinaryReader reader)
    {
        A = reader.ReadByte();
        F = reader.ReadByte();
        B = reader.ReadByte();
        C = reader.ReadByte();
        D = reader.ReadByte();
        E = reader.ReadByte();
        H = reader.ReadByte();
        L = reader.ReadByte();
        SP = reader.ReadUInt16();
        PC = reader.ReadUInt16();
    }
}