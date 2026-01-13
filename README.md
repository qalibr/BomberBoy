# BomberBoy

Written in C#, this emulator passes dmg-acid2 and most Blargg tests.

<img src="./img/dmg-acid2.png" alt="drawing" style="width:400px;"/>

## Building and Running

This project is built using the .NET 9 SDK.

### 1. Clone the repository

```sh
git clone https://github.com/qalibr/BomberBoy.git
cd BomberBoy
```

### 2. Build the project

```sh
dotnet build
```

### 3. Run the emulator

Pass the path to a Game Boy ROM as a command-line argument.

```sh
dotnet run --project BomberBoy/BomberBoy.csproj -- path/to/your/rom.gb
```

## Test ROMs Status

| Test ROM                  | Status |
| ------------------------  | :----: |
| `cpu_instrs.gb`           |   ✅   |
| `instr_timing.gb`         |   ✅   |
| `mem_timing.gb`           |   ✅   |
| `dmg-acid2.gb`            |   ✅   |
| `oam_bug.gb`              |   ❌   |
| `interrupt_time.gb`       |  N/A   |
| `halt_bug.gb`             |  N/A   |

## Features

| Feature              | Status         | Notes                                                     |
| -------------------- | :------------: | -----------------------------------------------------     |
| CPU Core             |       ✅       | Passes Blargg's `cpu_instrs` and `instr_timing` tests.    |
| PPU (Video)          |       🟡       | Passes `dmg-acid2`, but fails `oam_bug`.                  |
| Timer                |       ✅       | Implemented with correct `DIV` and `TIMA` logic.          |
| Sound (APU)          |       ❌       | No sound hardware emulation yet.                          |
| Joypad Input         |       ❌       | No user input is handled.                                 |
| MBC1 Support         |       ✅       | Basic memory bank controller is functional.               |
| Additional MBCs      |       ❌       | Support for MBC2, MBC3, MBC5, etc. is not implemented.    |
| Save States          |       ❌       | Ability to save/load game progress not implemented.       |
| GUI / Frontend       |       🟡       | Basic window via Raylib-cs. No menus or options.          |
| Debugging Tools      |       🟡       | Basic logging to console/file is available.               |
| CGB Support          |       ❌       | Emulator is currently DMG-only.                           |

## Resoures

<https://gbdev.io/pandocs/>

<https://gbdev.io/gb-opcodes/optables/>

<https://github.com/gbdev/awesome-gbdev>

### Testing

<https://github.com/robert/gameboy-doctor>

<https://github.com/retrio/gb-test-roms>

<https://github.com/mattcurrie/dmg-acid2>

## Emulators

These emulator's have been crucial for my success thus far.

<https://github.com/rockytriton/LLD_gbemu>

<https://github.com/BluestormDNA/ProjectDMG>

<http://www.codeslinger.co.uk/pages/projects/gameboy/beginning.html>

## Libraries

<https://github.com/raylib-cs/raylib-cs>
