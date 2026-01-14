using System.Text;
using BomberBoy.src.MBC;

namespace BomberBoy.src.PAK;

public class Pak : Rom
{
    public byte[] data { get; }
    private readonly string _filename;
    public IMbc mbc { get; }

    public static Pak CreateCartridge(string path)
    {
        return new Pak(path);
    }

    private Pak(string romPath)
    {
        _filename = romPath;
        data = File.ReadAllBytes(romPath);

        ParseRomHeader();

        mbc = Mbc.CreateMbc(this, GetEramSize());
    }

    private void ParseRomHeader()
    {
        title = Encoding.ASCII.GetString(data, 0x134, 0x10).TrimEnd('\0');
        type = data[0x147];
        newLicense = BitConverter.ToUInt16(data, 0x144);
        sgbFlag = data[0x146];
        romSize = data[0x148];
        ramSize = data[0x149];
        destination = data[0x14A];
        license = data[0x14B];
        version = data[0x14C];
        checksum = data[0x14D];
        globalChecksum = BitConverter.ToUInt16(data, 0x14E);

        PrintRomInfo();
        PrintChecksum();
    }

    private void PrintRomInfo()
    {
        Console.WriteLine($"Rom Loaded: {_filename}");
        Console.WriteLine($"\tTitle:        {title}");

        string typeString = RomTypes.TryGetValue(type, out var T) ? T : "Unknown";
        Console.WriteLine($"\tType:         {type:X2} ({typeString})");

        Console.WriteLine($"\tROM Size:     {32 << romSize} KB");
        Console.WriteLine($"\tRAM Size:     {ramSize:X2}");

        string licenseString = LicenseCodes.TryGetValue(license, out var L) ? L : "Unknown";
        Console.WriteLine($"\tLicense Code: {license:X2} ({licenseString})");

        Console.WriteLine($"\tROM Version:  {version:X2}");
    }

    private void PrintChecksum()
    {
        int calculatedChecksum = 0;
        for (int i = 0x0134; i <= 0x014C; i++)
        {
            calculatedChecksum = calculatedChecksum - data[i] - 1;
        }
        calculatedChecksum &= 0xFF;
        Console.WriteLine($"\tCalculated checksum: {calculatedChecksum:X2} (Expected: {checksum:X2})");
        Console.WriteLine($"\tChecksum: {(calculatedChecksum == checksum ? "PASSED" : "FAILED")}");
    }

    private int GetEramSize()
    {
        switch (ramSize)
        {
            case 0x00:
                return 0;
            case 0x01:
                return 2 * 1024;
            case 0x02:
                return 8 * 1024;
            case 0x03:
                return 32 * 1024;
            case 0x04:
                return 128 * 1024;
            case 0x05:
                return 64 * 1024;
            default:
                throw new ArgumentOutOfRangeException(nameof(ramSize), "Invalid RAM size code");
        }
    }
}
