using System;
using System.Diagnostics;

namespace Abc.Zerio.Client
{
    internal static class Program
    {
        private static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            Console.WriteLine("CLIENT...");

            RunZerioClient();

            Console.WriteLine("Press enter to quit.");
            Console.ReadLine();
        }

        private static void RunZerioClient()
        {
        }
    }
}
