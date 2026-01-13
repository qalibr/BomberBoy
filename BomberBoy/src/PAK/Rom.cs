namespace BomberBoy.src.PAK;

public class Rom 
{
    protected byte[] entry;
    protected byte[] logo;
    protected string title;
    protected byte   type;
    protected ushort newLicense;
    protected byte   sgbFlag;
    protected byte   romSize;
    protected byte   ramSize;
    protected byte   destination;
    protected byte   license;
    protected byte   version;
    protected byte   checksum;
    protected ushort globalChecksum;

    protected readonly string[] romTypes     = new string[0x23];
    protected readonly string[] licenseCodes = new string[0xA5];

    protected Rom()
    {
        entry = new byte[0x4];
        logo = new byte[0x30];
        title = new string(new char[0x10]);
    }

    protected void RomTypes()
    {
        romTypes[0] = "ROM ONLY";
        romTypes[1] = "MBC1";
        romTypes[2] = "MBC1+RAM";
        romTypes[3] = "MBC1+RAM+BATTERY";
        romTypes[4] = "0x04 ???";
        romTypes[5] = "MBC2";
        romTypes[6] = "MBC2+BATTERY";
        romTypes[7] = "0x07 ???";
        romTypes[8] = "ROM+RAM 1";
        romTypes[9] = "ROM+RAM+BATTERY 1";
        romTypes[10] = "0x0A ???";
        romTypes[11] = "MMM01";
        romTypes[12] = "MMM01+RAM";
        romTypes[13] = "MMM01+RAM+BATTERY";
        romTypes[14] = "0x0E ???";
        romTypes[15] = "MBC3+TIMER+BATTERY";
        romTypes[16] = "MBC3+TIMER+RAM+BATTERY 2";
        romTypes[17] = "MBC3";
        romTypes[18] = "MBC3+RAM 2";
        romTypes[19] = "MBC3+RAM+BATTERY 2";
        romTypes[20] = "0x14 ???";
        romTypes[21] = "0x15 ???";
        romTypes[22] = "0x16 ???";
        romTypes[23] = "0x17 ???";
        romTypes[24] = "0x18 ???";
        romTypes[25] = "MBC5";
        romTypes[26] = "MBC5+RAM";
        romTypes[27] = "MBC5+RAM+BATTERY";
        romTypes[28] = "MBC5+RUMBLE";
        romTypes[29] = "MBC5+RUMBLE+RAM";
        romTypes[30] = "MBC5+RUMBLE+RAM+BATTERY";
        romTypes[31] = "0x1F ???";
        romTypes[32] = "MBC6";
        romTypes[33] = "0x21 ???";
        romTypes[34] = "MBC7+SENSOR+RUMBLE+RAM+BATTERY";
    }

    protected void LicenseCodes()
    {
        licenseCodes[0x00] = "None";
        licenseCodes[0x01] = "Nintendo R&D1";
        licenseCodes[0x08] = "Capcom";
        licenseCodes[0x13] = "Electronic Arts";
        licenseCodes[0x18] = "Hudson Soft";
        licenseCodes[0x19] = "b-ai";
        licenseCodes[0x20] = "kss";
        licenseCodes[0x22] = "pow";
        licenseCodes[0x24] = "PCM Complete";
        licenseCodes[0x25] = "san-x";
        licenseCodes[0x28] = "Kemco Japan";
        licenseCodes[0x29] = "seta";
        licenseCodes[0x30] = "Viacom";
        licenseCodes[0x31] = "Nintendo";
        licenseCodes[0x32] = "Bandai";
        licenseCodes[0x33] = "Ocean/Acclaim";
        licenseCodes[0x34] = "Konami";
        licenseCodes[0x35] = "Hector";
        licenseCodes[0x37] = "Taito";
        licenseCodes[0x38] = "Hudson";
        licenseCodes[0x39] = "Banpresto";
        licenseCodes[0x41] = "Ubi Soft";
        licenseCodes[0x42] = "Atlus";
        licenseCodes[0x44] = "Malibu";
        licenseCodes[0x46] = "angel";
        licenseCodes[0x47] = "Bullet-Proof";
        licenseCodes[0x49] = "irem";
        licenseCodes[0x50] = "Absolute";
        licenseCodes[0x51] = "Acclaim";
        licenseCodes[0x52] = "Activision";
        licenseCodes[0x53] = "American sammy";
        licenseCodes[0x54] = "Konami";
        licenseCodes[0x55] = "Hi tech entertainment";
        licenseCodes[0x56] = "LJN";
        licenseCodes[0x57] = "Matchbox";
        licenseCodes[0x58] = "Mattel";
        licenseCodes[0x59] = "Milton Bradley";
        licenseCodes[0x60] = "Titus";
        licenseCodes[0x61] = "Virgin";
        licenseCodes[0x64] = "LucasArts";
        licenseCodes[0x67] = "Ocean";
        licenseCodes[0x69] = "Electronic Arts";
        licenseCodes[0x70] = "Infogrames";
        licenseCodes[0x71] = "Interplay";
        licenseCodes[0x72] = "Broderbund";
        licenseCodes[0x73] = "sculptured";
        licenseCodes[0x75] = "sci";
        licenseCodes[0x78] = "THQ";
        licenseCodes[0x79] = "Accolade";
        licenseCodes[0x80] = "misawa";
        licenseCodes[0x83] = "lozc";
        licenseCodes[0x86] = "Tokuma Shoten Intermedia";
        licenseCodes[0x87] = "Tsukuda Original";
        licenseCodes[0x91] = "Chunsoft";
        licenseCodes[0x92] = "Video system";
        licenseCodes[0x93] = "Ocean/Acclaim";
        licenseCodes[0x95] = "Varie";
        licenseCodes[0x96] = "Yonezawa/s’pal";
        licenseCodes[0x97] = "Kaneko";
        licenseCodes[0x99] = "Pack in soft";
        licenseCodes[0xA4] = "Konami (Yu-Gi-Oh!)";
    }
}