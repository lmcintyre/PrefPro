using System;
using System.Collections.Generic;
using Iced.Intel;

namespace PrefPro;

public class CodeUtil
{
    public static bool TryGetStaticAddressFromPtr(nint instructionAddressNint, out IntPtr result)
    {
        try
        {
            result = GetStaticAddressFromPtr(instructionAddressNint);
            return true;
        }
        catch (KeyNotFoundException)
        {
            result = IntPtr.Zero;
            return false;
        }
    }
    
    public static unsafe IntPtr GetStaticAddressFromPtr(nint instructionAddressNint)
    {
        var instructionAddress = (byte*)instructionAddressNint;
        try
        {
            var reader = new UnsafeCodeReader(instructionAddress, 64);
            var decoder = Decoder.Create(64, reader, (ulong)instructionAddress, DecoderOptions.AMD);
            while (reader.CanReadByte)
            {
                var instruction = decoder.Decode();
                if (instruction.IsInvalid) continue;
                if (instruction.Op0Kind is OpKind.Memory || instruction.Op1Kind is OpKind.Memory)
                {
                    return (IntPtr)instruction.MemoryDisplacement64;
                }
            }
        }
        catch
        {
            // ignored
        }

        throw new KeyNotFoundException($"Can't find any referenced address at the given pointer.");
    }

    private unsafe class UnsafeCodeReader : CodeReader
    {
        private readonly int length;
        private readonly byte* address;
        private int pos;

        public UnsafeCodeReader(byte* address, int length)
        {
            this.length = length;
            this.address = address;
        }

        public bool CanReadByte => this.pos < this.length;

        public override int ReadByte()
        {
            if (this.pos >= this.length) return -1;
            return *(this.address + this.pos++);
        }
    }
}