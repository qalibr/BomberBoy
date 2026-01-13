using BomberBoy.src.MMU;
using BomberBoy.src.CPU;

namespace BomberBoy.src.Util
{
    public class GBDoctor
    {
        private readonly Debug _debug;

        private readonly List<string> _logBuffer = new();
        private long _logLinesWritten = 0;
        private long _instructionsProcessed = 0;
        private const int LogBufferSize = 1;
        private readonly long _minConsoleLogLines;
        private readonly long _maxConsoleLogLines;
        private readonly StreamWriter? _logStream;

        public long LogLinesWritten => _logLinesWritten;
        public long InstructionsProcessed => _instructionsProcessed;

        public GBDoctor(StreamWriter? logStream, long minConsoleLogLines = 0, long maxConsoleLogLines = long.MaxValue)
        {
            _logStream = logStream;
            _debug = new();
            _minConsoleLogLines = minConsoleLogLines;
            _maxConsoleLogLines = maxConsoleLogLines;
        }

        public void Log(Registers regs, Mmu mmu, ref bool terminate)
        {
            _instructionsProcessed++;

            // Termination logic, controlled from Program.cs.
            if (_instructionsProcessed > _maxConsoleLogLines)
            {
                if (!terminate && _maxConsoleLogLines != long.MaxValue) // Only show termination message if not logging indefinitely
                {
                    Console.WriteLine($"Reached maximum instructions count '{_maxConsoleLogLines}', terminating.");
                    terminate = true;
                }
            }

            // --- File Logging ---
            // Ignores slice parameters controlled from Program.cs.
            if (_logStream == null)
            {
                return;
            }

            string registersState = $"A:{regs.A:X2} F:{regs.F:X2} B:{regs.B:X2} C:{regs.C:X2} D:{regs.D:X2} E:{regs.E:X2} H:{regs.H:X2} L:{regs.L:X2} SP:{regs.SP:X4} PC:{regs.PC:X4}";

            byte m0 = mmu.ReadByte(regs.PC);
            byte m1 = mmu.ReadByte((ushort)(regs.PC + 1));
            byte m2 = mmu.ReadByte((ushort)(regs.PC + 2));
            byte m3 = mmu.ReadByte((ushort)(regs.PC + 3));
            string pcmem = $"PCMEM:{m0:X2},{m1:X2},{m2:X2},{m3:X2}";

            _logBuffer.Add($"{registersState} {pcmem}");

            if (_logBuffer.Count >= LogBufferSize)
            {
                FlushLogBuffer();
            }
        }

        public void FlushLogBuffer()
        {
            if (_logStream == null || _logBuffer.Count == 0)
            {
                return;
            }

            foreach (var line in _logBuffer)
            {
                _logStream.WriteLine(line);
            }
            _logStream.Flush();
            _logLinesWritten += _logBuffer.Count;
            _logBuffer.Clear();
        }

        public void Print(Mmu mmu, ref ushort previousPC, ref byte opcode)
        {
            // Print debug info only when we are within the specified logging slice.
            if (InstructionsProcessed >= _minConsoleLogLines && InstructionsProcessed <= _maxConsoleLogLines)
            {
                // CURRENT INSTRUCTIONS
                var instructionName = _debug.InstructionName(opcode);
                int instructionLength = _debug.GetInstructionLength(opcode);

                // If the current instruction is a CB prefix, we can be more specific
                if (opcode == 0xCB)
                {
                    byte cbOpcode = mmu.ReadByte((ushort)(previousPC + 1));
                    instructionName = $"PREFIX CB ({cbOpcode:X2})";
                }
                string currentInstructionStr = $"Executing: {instructionName}";

                // NEXT INSTRUCTION
                ushort nextInstructionAddress = (ushort)(previousPC + instructionLength);
                byte nextOpcode = mmu.ReadByte(nextInstructionAddress);
                var nextInstructionName = _debug.InstructionName(nextOpcode);
                int nextInstructionLength = _debug.GetInstructionLength(nextOpcode);

                // Get raw bytes for the next instruction.
                List<string> nextInstructionHex = new();
                for (int i = 0; i < nextInstructionLength; i++)
                {
                    nextInstructionHex.Add(mmu.ReadByte((ushort)(nextInstructionAddress + i)).ToString("X2"));
                }
                string nextOpcodesStr = string.Join(", ", nextInstructionHex);

                // If the next instruction is a CB prefix, we can be more specific.
                if (nextOpcode == 0xCB)
                {
                    byte nextCbOpcode = mmu.ReadByte((ushort)(nextInstructionAddress + 1));
                    nextInstructionName = $"PREFIX CB ({nextCbOpcode:X2})";
                }

                // Next instruction and next opcodes (including next instruction)
                var opcodesDisplay = $"[{nextOpcodesStr}]";
                string nextInstructionStr = $"Next opcode(s): {opcodesDisplay.PadRight(14)} Next instruction: {nextInstructionName}";

                // --- Formatting and Output ---
                Console.WriteLine($"[DEBUG] PC: {previousPC:X4} | Log line: {InstructionsProcessed} | {currentInstructionStr.PadRight(23)} | {nextInstructionStr}");
            }
        }
    }
}