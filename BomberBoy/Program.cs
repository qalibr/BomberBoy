using BomberBoy.src;

namespace BomberBoy
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine($"Arguments received: {args.Length}");
                return;
            }

            try
            {
                var emulator = new Emulator(args[0],
                    debuggingEnabled: false,
                    consoleLogTarget: 1513470,          // The instruction number to center the console log on.
                    consoleLogRadius: 500000000);       // The number of instructions to show before and after the target.
                emulator.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to initialize:");
                Console.WriteLine(ex.Message);
                Environment.Exit(1);
            }
        }
    }
}