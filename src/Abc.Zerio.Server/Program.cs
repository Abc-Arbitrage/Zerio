using System;
using System.Diagnostics;

namespace Abc.Zerio.Server
{
    internal static class Program
    {
        private static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            Console.WriteLine("SERVER...");

            RunZerioServer();

            Console.WriteLine("Press enter to quit.");
            Console.ReadLine();
        }

        private static void RunZerioServer()
        {
        }
    }
}
