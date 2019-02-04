using System;
using System.Runtime.InteropServices;

namespace OoTBitRaceRandomizer
{
    /// <summary>
    /// Contains information for a code to be applied.
    /// </summary>
    public class CodeInfo
    {
        public readonly int MemoryOffset;
        public readonly Type DataType;
        public readonly int DataSize;
        public readonly int MinimumPointDonation;
        public readonly object MinValue;
        public readonly object MaxValue;
        public readonly string Name;
        public readonly string CommandName;

        public CodeInfo(string Name, string CommandName, int MemoryOffset, int MinimumPointDonation, object MinValue, object MaxValue)
        {
            Type MinType = MinValue.GetType();
            Type MaxType = MinValue.GetType();

            if (MinType != MaxType) { throw new Exception("MinValue and MaxValue must be of the same type!"); }
            else {
                this.Name = Name;
                this.CommandName = CommandName;
                this.MemoryOffset = MemoryOffset;
                this.MinimumPointDonation = MinimumPointDonation;
                this.MinValue = MinValue;
                this.MaxValue = MaxValue;
                this.DataType = MinType;
                this.DataSize = Marshal.SizeOf(MinType);
            }
        }

    }
}
