using System.Runtime.InteropServices;

namespace PrefPro;

[StructLayout(LayoutKind.Explicit, Size = 0x20)]
public struct UnknownStruct
{
	[FieldOffset(0x00)] public ulong Value;
	[FieldOffset(0x08)] public nuint Self;
	[FieldOffset(0x16)] public sbyte Status;
}