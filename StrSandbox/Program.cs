using System;
using System.Text;

namespace StrSandbox
{
    class Program
    {
        private static bool male = true;
        
        static unsafe void Main(string[] args)
        {
            // Paste a dump here, and write a function to handle what you want
            var test = new byte[]
            {
                0x49, 0x20, 0x73, 0x68, 0x61, 0x6C, 0x6C, 0x20, 0x73, 0x61, 0x79, 0x20, 0x69, 0x74, 0x20, 0x70, 0x6C,
                0x61, 0x69, 0x6E, 0x2C, 0x20, 0x4C, 0x75, 0x63, 0x69, 0x61, 0x6E, 0x65, 0x3A, 0x20, 0x74, 0x68, 0x69,
                0x73, 0x20, 0x2, 0x8, 0xF, 0xE9, 0x5, 0xFF, 0x6, 0x77, 0x6F, 0x6D, 0x61, 0x6E, 0xFF, 0x4, 0x6D, 0x61,
                0x6E, 0x3, 0x20, 0x69, 0x73, 0x20, 0x6E, 0x6F, 0x74, 0x20, 0x66, 0x69, 0x74, 0x20, 0x74, 0x6F, 0x20,
                0x77, 0x69, 0x65, 0x6C, 0x64, 0x20, 0x61, 0x20, 0x62, 0x6F, 0x77, 0x2E, 0x20, 0x46, 0x6F, 0x72, 0x20,
                0x6F, 0x75, 0x72, 0x20, 0x73, 0x61, 0x6B, 0x65, 0xE2, 0x94, 0x80, 0x61, 0x6E, 0x64, 0x20, 0x2, 0x8, 0xE,
                0xE9, 0x5, 0xFF, 0x5, 0x68, 0x65, 0x72, 0x73, 0xFF, 0x4, 0x68, 0x69, 0x73, 0x3, 0xE2, 0x94, 0x80, 0x77,
                0x65, 0x20, 0x73, 0x68, 0x6F, 0x75, 0x6C, 0x64, 0x20, 0x72, 0x65, 0x76, 0x6F, 0x6B, 0x65, 0x20, 0x2,
                0x8, 0xD, 0xE9, 0x5, 0xFF, 0x4, 0x68, 0x65, 0x72, 0xFF, 0x4, 0x68, 0x69, 0x73, 0x3, 0x20, 0x6D, 0x65,
                0x6D, 0x62, 0x65, 0x72, 0x73, 0x68, 0x69, 0x70, 0x2E
            };
            
            Console.WriteLine(Encoding.ASCII.GetString(test));
            Console.WriteLine(ByteArrayStr(test));

            fixed (byte* text = test)
            {
                ProcessGenderedParam(text);
            }
            
            Console.WriteLine(Encoding.ASCII.GetString(test));
            Console.WriteLine(ByteArrayStr(test));
        }

        private static string ByteArrayStr(byte[] arr)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < arr.Length; i++)
                sb.Append($"{arr[i]:X} ");
            return sb.ToString();
        }

        private static unsafe void ProcessGenderedParam(byte* ptr)
        {
            int len = 0;
            byte* text2 = ptr;
            while (*text2 != 0) { text2++; len++; }

            byte[] newText = new byte[len];
            
            int currentPos = 0;

            for (int i = 0; i < len; i++)
            {
                if (ptr[i] == 2 && ptr[i + 1] == 8 && ptr[i + 3] == 0xE9 && ptr[i + 4] == 5)
                {
                    int codeStart = i;
                    int codeLen = codeStart;
                    while (ptr[codeLen] != 3) codeLen++;
                    codeLen = codeLen - codeStart;

                    int femaleStart = codeStart + 7;
                    int femaleLen = ptr[codeStart + 6] - 1;
                    int maleStart = femaleStart + femaleLen + 2;
                    int maleLen = ptr[maleStart - 1] - 1;
                    
                    if (male)
                    {
                        for (int pos = maleStart; pos < maleStart + maleLen; pos++)
                        {
                            newText[currentPos] = ptr[pos];
                            currentPos++;
                        }
                    }
                    else
                    {
                        for (int pos = femaleStart; pos < femaleStart + femaleLen; pos++)
                        {
                            newText[currentPos] = ptr[pos];
                            currentPos++;
                        }
                    }

                    // Console.WriteLine($"Code: {Encoding.ASCII.GetString((ptr + codeStart), codeLen)}");
                    Console.WriteLine($"Prog: {Encoding.ASCII.GetString(newText)}");
                    Console.WriteLine($"femStart: {femaleStart} femLen: {femaleLen} maleStart: {maleStart} maleLen: {maleLen}");
                    i += codeLen;
                }
                else
                {
                    newText[currentPos] = ptr[i];
                    currentPos++;
                }
            }

            for (int i = 0; i < len; i++)
                ptr[i] = newText[i];
        }
    }
}