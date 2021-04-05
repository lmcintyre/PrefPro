using System;
using System.IO;
using Dalamud.Game.Text.SeStringHandling;

namespace PrefPro
{
    public class ActuallyRawPayload : Payload
    {
        public override PayloadType Type => PayloadType.Unknown;
        private byte[] data;        
        
        public ActuallyRawPayload(byte[] bytes)
        {
            data = bytes;
        }
        
        protected override byte[] EncodeImpl()
        {
            return data;
        }

        protected override void DecodeImpl(BinaryReader reader, long endOfStream)
        {
            throw new NotSupportedException("Decoding an ActuallyRawPayload is not supported.");
        }
    }
}