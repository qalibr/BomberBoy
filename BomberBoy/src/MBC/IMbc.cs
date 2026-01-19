namespace BomberBoy.src.MBC;

public interface IMbc
{
    public byte ReadLoRom(ushort addr);
    public byte ReadHiRom(ushort addr);

    public void WriteRom(ushort addr, byte val);

    public byte ReadEram(ushort addr);
    public void WriteEram(ushort addr, byte val);

    public void SaveState(BinaryWriter writer);
    public void LoadState(BinaryReader reader);
}