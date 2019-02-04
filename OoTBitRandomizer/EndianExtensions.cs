namespace System
{
    public static class UInt16Extension
    {
        public static UInt16 Reverse(this UInt16 Value) { return (ushort)((Value << 8) | (Value >> 8)); }
        public static UInt16 ToN64(this UInt16 Value) { return Value.Reverse(); }
    }

    public static class Int16Extension
    {
        public static Int16 Reverse(this Int16 Value) { return (short)((Value << 8) | (Value >> 8)); }
        public static Int16 ToN64(this Int16 Value) { return Value.Reverse(); }
    }

    public static class UInt32Extension
    {
        public static UInt32 Reverse(this UInt32 Value)
        { return ((Value << 24) | ((Value >> 24) & 0xFF) | ((Value & 0xFF00) << 8) | ((Value >> 8) & 0xFF00)); }

        public static UInt32 ToN64(this UInt32 Value) { return (Value << 16) | (Value >> 16); }
    }

    public static class Int32Extension
    {
        public static Int32 Reverse(this Int32 Value)
        { return ((Value << 24) | ((Value >> 24) & 0xFF) | ((Value & 0xFF00) << 8) | ((Value >> 8) & 0xFF00)); }

        public static Int32 ToN64(this Int32 Value) { return (Value << 16) | (Value >> 16); }
    }
}
