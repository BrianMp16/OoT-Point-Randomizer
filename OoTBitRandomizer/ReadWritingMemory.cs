using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OoTBitRaceRandomizer
{
    static class ReadWritingMemory
    {
        public const int ConstVal1 = 0x354AFFFF;
        public const int ConstVal2 = 0x3C01A460;

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int OpenProcess(int dwDesiredAccess, int bInheritHandle, int dwProcessId);

        [DllImport("kernel32", EntryPoint = "WriteProcessMemory", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int WriteProcessMemory1(int hProcess, int lpBaseAddress, ref int lpBuffer, int nSize, ref int lpNumberOfBytesWritten);

        [DllImport("kernel32", EntryPoint = "ReadProcessMemory", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int ReadProcessMemory1(int hProcess, int lpBaseAddress, ref int lpBuffer, int nSize, ref int lpNumberOfBytesRead);

        public static void WriteXBytes(string ProcessName, int Address, byte[] Values)
        {
            if (ProcessName.EndsWith(".exe")) { ProcessName = ProcessName.Replace(".exe", ""); }

            Process[] MyP = Process.GetProcessesByName(ProcessName);
            if (MyP.Length == 0) { MessageBox.Show(ProcessName + " isn't open!"); return; }

            int hProcess = OpenProcess(0x1f0ff, 0, MyP[0].Id);
            if (hProcess == 0) { MessageBox.Show("Failed to open " + ProcessName + "!"); return; }

            int V = 0;
            int reference = 0;

            for (int i = 0; i < Values.Length; i++)
            { V = Values[i]; WriteProcessMemory1(hProcess, Address + i, ref V, 1, ref reference); }
        }

        public static int GetBaseAddress(string ProcessName, int startOffset = 0, int scanStep = 0x1000, int nsize = 4)
        {
            if (ProcessName.EndsWith(".exe")) { ProcessName = ProcessName.Replace(".exe", ""); }

            Process[] MyP = Process.GetProcessesByName(ProcessName);
            if (MyP.Length == 0) { MessageBox.Show(ProcessName + " isn't open!"); return 0; }

            int hProcess = OpenProcess(0x1f0ff, 0, MyP[0].Id);
            if (hProcess == 0) { MessageBox.Show("Failed to open " + ProcessName + "!"); return 0; }

            int vBuffer = 0;

            for (int x = startOffset; x <= 0x72D00000; x += scanStep)
            { int reference = 0;

                ReadProcessMemory1(hProcess, x, ref vBuffer, nsize, ref reference);
                if (vBuffer == ConstVal1) // this appears to be constant?
                { reference = 0;
                    ReadProcessMemory1(hProcess, x + 4, ref vBuffer, nsize, ref reference);
                    if (vBuffer == ConstVal2) // this may be constant too. they're definitely better than the ones at address 0x80000000
                    { int RAMAddress = x - 0x10;
                        Console.WriteLine("RAM Base Address: 0x" + RAMAddress.ToString("X8"));
                        return RAMAddress; } }  }

            return 0;
        }

    }
}
