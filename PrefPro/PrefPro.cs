using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Text;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.Internal.Libc;
using Dalamud.Hooking;

namespace PrefPro
{
    public unsafe class PrefPro : IDalamudPlugin
    {
        public enum NameSetting
        {
            FirstLast,
            FirstOnly,
            LastOnly,
            LastFirst
        }

        public enum GenderSetting
        {
            Male,
            Female,
            Random,
            Model
        }
        
        public string Name => "PrefPro";
        private const string commandName = "/prefpro";

        private SeStringManager manager;
        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;
        
        //reEncode[1] == 0x29 && reEncode[2] == 0x3 && reEncode[3] == 0xEB && reEncode[4] == 0x2
        private static byte[] FullNameBytes = {0x02, 0x29, 0x03, 0xEB, 0x02, 0x03};
        private static byte[] FirstNameBytes = {0x02, 0x2C, 0x0D, 0xFF, 0x07, 0x02, 0x29, 0x03, 0xEB, 0x02, 0x03, 0xFF, 0x02, 0x20, 0x02, 0x03};
        private static byte[] LastNameBytes = {0x02, 0x2C, 0x0D, 0xFF, 0x07, 0x02, 0x29, 0x03, 0xEB, 0x02, 0x03, 0xFF, 0x02, 0x20, 0x03, 0x03};
        
        private delegate int GetStringPrototype(void* unknown, byte* text, void* unknown2, void* stringStruct);
        private Hook<GetStringPrototype> GetStringHook;
        
        public static string filterText = "";
        public string PlayerName => pi?.ClientState?.LocalPlayer?.Name;
        public ulong CurrentPlayerContentId => pi.ClientState?.LocalContentId ?? 0;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            pi = pluginInterface;
            
            configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pi, this);

            ui = new PluginUI(configuration, this);
            
            pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the PrefPro menu."
            });

            string getStringStr = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 83 B9 ?? ?? ?? ?? ?? 49 8B F9 49 8B F0 48 8B EA 48 8B D9 75 09 48 8B 01 FF 90 ?? ?? ?? ??";
            IntPtr getStringPtr = pi.TargetModuleScanner.ScanText(getStringStr);
            GetStringHook = new Hook<GetStringPrototype>(getStringPtr, (GetStringPrototype) GetStringDetour);
            
            GetStringHook.Enable();

            manager = new SeStringManager(pi.Data);
            
            pi.UiBuilder.OnBuildUi += DrawUI;
            pi.UiBuilder.OnOpenConfigUi += (sender, args) => DrawConfigUI();
        }

        private int GetStringDetour(void* unknown, byte* text, void* unknown2, void* stringStruct)
        {
#if DEBUG
            int len = 0;
            byte* text2 = text;
            while (*text2 != 0) { text2++; len++; }
            string str = Encoding.ASCII.GetString(text, len);
            if (filterText != "" && str.Contains(filterText))
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < len; i++)
                    sb.Append($"{*(text + i):X} ");
            
                PluginLog.Log($"GS Dump  : {sb}");
                PluginLog.Log($"GetString: {Encoding.ASCII.GetString(text, len)}");

            }
#endif
            if (configuration.Enabled)
            {
                HandlePtr(manager, ref text);
            }
#if DEBUG
            len = 0;
            text2 = text;
            while (*text2 != 0) { text2++; len++; }
            int retVal = GetStringHook.Original(unknown, text, unknown2, stringStruct);
            str = Encoding.ASCII.GetString(text, len);
            if (filterText != "" && str.Contains(filterText))
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < len; i++)
                    sb.Append($"{*(text + i):X} ");
            
                PluginLog.Log($"GS Dump  : {sb}");
                PluginLog.Log($"GetString: {Encoding.ASCII.GetString(text, len)}");
            }
            return retVal;
#else
            return GetStringHook.Original(unknown, text, unknown2, stringStruct);
#endif
        }
        
        private void HandlePtr(SeStringManager mgr, ref byte* ptr)
        {
            var byteList = new List<byte>();
            int i = 0;
            while (ptr[i] != 0)
                byteList.Add(ptr[i++]);
            var byteArr = byteList.ToArray();
            
            // Write handlers, put them here
            SeString parsed = mgr.Parse(byteArr);
            for (int payloadIndex = 0; payloadIndex < parsed.Payloads.Count; payloadIndex++)
            {
                var thisPayload = parsed.Payloads[payloadIndex];
                if (thisPayload.Type == PayloadType.Unknown)
                {
                    // Add handlers here
                    parsed.Payloads[payloadIndex] = HandleGenderPayload(parsed.Payloads[payloadIndex]);
                    parsed.Payloads[payloadIndex] = HandleFullNamePayload(parsed.Payloads[payloadIndex]);
                    parsed.Payloads[payloadIndex] = HandleFirstNamePayload(parsed.Payloads[payloadIndex]);
                    parsed.Payloads[payloadIndex] = HandleLastNamePayload(parsed.Payloads[payloadIndex]);
                }
            }
            var encoded = parsed.Encode();

            if (ByteArrayEquals(encoded, byteArr))
                return;
            
            // var encodedNullTerminated = new byte[encoded.Length + 1];
            // encoded.CopyTo(encodedNullTerminated, 0);
            // encodedNullTerminated[encoded.Length] = 0;
            //
            // var newStr = pi.Framework.Libc.NewString(encodedNullTerminated);
            //
            // ptr = (byte*) newStr.Address.ToPointer();

            if (encoded.Length <= byteArr.Length)
            {
                int j;
                for (j = 0; j < encoded.Length; j++)
                    ptr[j] = encoded[j];
                ptr[j] = 0;    
            }
            else
            {
                byte* newStr = (byte*) Marshal.AllocHGlobal(encoded.Length + 1);
                int j;
                for (j = 0; j < encoded.Length; j++)
                    newStr[j] = encoded[j];
                newStr[j] = 0;
                ptr = newStr;
            }
        }
        
        private static bool ByteArrayEquals(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
        {
            return a1.SequenceEqual(a2);
        }

        private Payload HandleGenderPayload(Payload thisPayload)
        {
            byte[] reEncode = thisPayload.Encode();
            // We have to compare bytes here because there is a wildcard in the middle
            if (reEncode[1] != 8 || reEncode[3] != 0xE9 || reEncode[4] != 5
                || configuration.Gender == GenderSetting.Model)
                return thisPayload;
            
            int femaleStart = 7;
            int femaleLen = reEncode[6] - 1;
            int maleStart = femaleStart + femaleLen + 2;
            int maleLen = reEncode[maleStart - 1] - 1;

            bool male;
            if (configuration.Gender == GenderSetting.Random)
                male = new Random().Next(0, 2) == 0;
            else
                male = configuration.Gender == GenderSetting.Male;
            
            int len = male ? maleLen : femaleLen;
            int start = male ? maleStart : femaleStart;

            byte[] newTextBytes = new byte[len];
            for (int c = 0; c < newTextBytes.Length; c++)
                newTextBytes[c] = reEncode[start + c];

            return new ActuallyRawPayload(newTextBytes);
        }

        private Payload HandleFullNamePayload(Payload thisPayload)
        {
            byte[] reEncode = thisPayload.Encode();
            if (!ByteArrayEquals(reEncode, FullNameBytes)) return thisPayload;

            return new TextPayload(GetNameText(configuration.FullName));
        }

        private Payload HandleFirstNamePayload(Payload thisPayload)
        {
            byte[] reEncode = thisPayload.Encode();
            if (!ByteArrayEquals(reEncode, FirstNameBytes)) return thisPayload;
            
            return new TextPayload(GetNameText(configuration.FirstName));
        }
        
        private Payload HandleLastNamePayload(Payload thisPayload)
        {
            byte[] reEncode = thisPayload.Encode();
            if (!ByteArrayEquals(reEncode, LastNameBytes)) return thisPayload;

            return new TextPayload(GetNameText(configuration.LastName));
        }

        private string GetNameText(PrefPro.NameSetting setting)
        {
            var name = configuration.Name;
            var split = name.Split(' ');
            var first = split[0];
            var last = split[1];

            return setting switch
            {
                NameSetting.FirstLast => name,
                NameSetting.FirstOnly => first,
                NameSetting.LastOnly => last,
                NameSetting.LastFirst => $"{last} {first}",
                _ => PlayerName
            };
        }
        
        // private void ProcessGenderedParam(byte* ptr)
        // {
        //     int len = 0;
        //     byte* text2 = ptr;
        //     while (*text2 != 0) { text2++; len++; }
        //
        //     byte[] newText = new byte[len + 1];
        //     
        //     int currentPos = 0;
        //
        //     for (int i = 0; i < len; i++)
        //     {
        //         if (ptr[i] == 2 && ptr[i + 1] == 8 && ptr[i + 3] == 0xE9 && ptr[i + 4] == 5)
        //         {
        //             int codeStart = i;
        //             int codeLen = ptr[i + 2] + 2;
        //
        //             int femaleStart = codeStart + 7;
        //             int femaleLen = ptr[codeStart + 6] - 1;
        //             int maleStart = femaleStart + femaleLen + 2;
        //             int maleLen = ptr[maleStart - 1] - 1;
        //
        //             if (configuration.SelectedGender == "Male")
        //             {
        //                 for (int pos = maleStart; pos < maleStart + maleLen; pos++)
        //                 {
        //                     newText[currentPos] = ptr[pos];
        //                     currentPos++;
        //                 }
        //             }
        //             else
        //             {
        //                 for (int pos = femaleStart; pos < femaleStart + femaleLen; pos++)
        //                 {
        //                     newText[currentPos] = ptr[pos];
        //                     currentPos++;
        //                 }
        //             }
        //
        //             i += codeLen;
        //         }
        //         else
        //         {
        //             newText[currentPos] = ptr[i];
        //             currentPos++;
        //         }
        //     }
        //
        //     for (int i = 0; i < len; i++)
        //         ptr[i] = newText[i];
        // }

        public void Dispose()
        {
            ui.Dispose();
            
            GetStringHook.Disable();
            GetStringHook.Dispose();

            pi.CommandManager.RemoveHandler(commandName);
            pi.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            ui.SettingsVisible = true;
        }

        private void DrawUI()
        {
            ui.Draw();
        }
        
        private void DrawConfigUI()
        {
            ui.SettingsVisible = true;
        }
    }
}
