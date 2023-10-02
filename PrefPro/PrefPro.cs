using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

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

        private const string CommandName = "/prefpro";
        
        private readonly Configuration _configuration;
        private readonly PluginUI _ui;
        
        //reEncode[1] == 0x29 && reEncode[2] == 0x3 && reEncode[3] == 0xEB && reEncode[4] == 0x2
        private static readonly byte[] FullNameBytes = {0x02, 0x29, 0x03, 0xEB, 0x02, 0x03};
        private static readonly byte[] FirstNameBytes = {0x02, 0x2C, 0x0D, 0xFF, 0x07, 0x02, 0x29, 0x03, 0xEB, 0x02, 0x03, 0xFF, 0x02, 0x20, 0x02, 0x03};
        private static readonly byte[] LastNameBytes = {0x02, 0x2C, 0x0D, 0xFF, 0x07, 0x02, 0x29, 0x03, 0xEB, 0x02, 0x03, 0xFF, 0x02, 0x20, 0x03, 0x03};
        
        private delegate int GetStringPrototype(void* unknown, byte* text, void* unknown2, void* stringStruct);
        private readonly Hook<GetStringPrototype> _getStringHook;
        
        private delegate int GetCutVoGenderPrototype(void* a1, void* a2);
        private readonly Hook<GetCutVoGenderPrototype> _getCutVoGenderHook;
        
        private delegate int GetCutVoLangPrototype(void* a1);
        private readonly GetCutVoLangPrototype _getCutVoLang;

        public string PlayerName => DalamudApi.ClientState.LocalPlayer?.Name.ToString();
        public ulong CurrentPlayerContentId => DalamudApi.ClientState.LocalContentId;

        private uint _frameworkLangCallOffset = 0;

        public PrefPro(DalamudPluginInterface pi
        )
        {
            DalamudApi.Initialize(pi);
            
            _configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            _configuration.Initialize(this);

            _ui = new PluginUI(_configuration, this);
            
            DalamudApi.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Display the PrefPro configuration interface."
            });

            var getStringStr = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 83 B9 ?? ?? ?? ?? ?? 49 8B F9 49 8B F0 48 8B EA 48 8B D9 75 09 48 8B 01 FF 90";
            var getStringPtr = DalamudApi.SigScanner.ScanText(getStringStr);
            _getStringHook = DalamudApi.Hooks.HookFromAddress<GetStringPrototype>(getStringPtr, GetStringDetour);
            
            var getCutVoGender = "E8 ?? ?? ?? ?? 8B F0 85 ED 7E 43";
            var getCutVoGenderPtr = DalamudApi.SigScanner.ScanText(getCutVoGender);
            _getCutVoGenderHook = DalamudApi.Hooks.HookFromAddress<GetCutVoGenderPrototype>(getCutVoGenderPtr, GetCutVoGenderDetour);
            
            var getCutVoLang = "E8 ?? ?? ?? ?? 48 63 56 1C";
            var getCutVoLangPtr = DalamudApi.SigScanner.ScanText(getCutVoLang);
            _getCutVoLang = Marshal.GetDelegateForFunctionPointer<GetCutVoLangPrototype>(getCutVoLangPtr);
            
            var frameworkLangCallOffsetStr = "48 8B 88 ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C 63 7E 24";
            var frameworkLangCallOffsetPtr = DalamudApi.SigScanner.ScanText(frameworkLangCallOffsetStr);
            _frameworkLangCallOffset = *(uint*)(frameworkLangCallOffsetPtr + 3);
            DalamudApi.PluginLog.Verbose($"framework lang call offset {_frameworkLangCallOffset} {_frameworkLangCallOffset:X}");
            
            // TODO: Include? no idea
            // if (frameworkLangCallOffset is < 10000 or > 14000)
            // {
            //     PluginLog.Error("Framework language call offset is invalid. The plugin will be disabled.");
            //     throw new InvalidOperationException();
            // }
            
            _getStringHook.Enable();
            _getCutVoGenderHook.Enable();
            
            DalamudApi.PluginInterface.UiBuilder.Draw += DrawUI;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        public void Dispose()
        {
            _ui.Dispose();
            
            _getStringHook?.Disable();
            _getStringHook?.Dispose();
            _getCutVoGenderHook?.Disable();
            _getCutVoGenderHook?.Dispose();

            DalamudApi.CommandManager.RemoveHandler(CommandName);
        }

        private int GetStringDetour(void* unknown, byte* text, void* unknown2, void* stringStruct)
        {
            if (_configuration.Enabled)
                HandlePtr(ref text);
            
            return _getStringHook.Original(unknown, text, unknown2, stringStruct);
        }
        
        private int GetCutVoGenderDetour(void* a1, void* a2)
        {
            var originalRet = _getCutVoGenderHook.Original(a1, a2);
            DalamudApi.PluginLog.Verbose($"[genderDetour] original returned {originalRet}");

            if (!_configuration.Enabled)
                return originalRet;
            
            var lang = GetCutVoLang();
            DalamudApi.PluginLog.Verbose($"Lang returned {lang}");

            var v1 = *(int*) ((ulong)a2 + 28);
            var v2 = 12 * lang;
            var v3 = *(int*) ((ulong)a2 + (ulong)v1 + (ulong)v2);

            if (v3 == 1)
            {
                DalamudApi.PluginLog.Verbose($"[genderDetour] v3 is 1");
                return 0;
            }
            
            switch (_configuration.Gender)
            {
                case GenderSetting.Male:
                    DalamudApi.PluginLog.Verbose($"[genderDetour] returning 0");
                    return 0;
                case GenderSetting.Female:
                    DalamudApi.PluginLog.Verbose($"[genderDetour] returning 1");
                    return 1;
                case GenderSetting.Random:
                    var ret = new Random().Next(0, 2);
                    DalamudApi.PluginLog.Verbose($"[genderDetour] returning {ret}");
                    return ret;
                case GenderSetting.Model:
                    DalamudApi.PluginLog.Verbose($"[genderDetour] returning original: {originalRet}");
                    return originalRet;
            }

            DalamudApi.PluginLog.Verbose($"[genderDetour] returning original: {originalRet}");
            return originalRet;
        }

        private int GetCutVoLang()
        {
            var offs = *(void**) ((nint)Framework.Instance() + (int) _frameworkLangCallOffset);
            DalamudApi.PluginLog.Verbose($"GetCutVoLang: {(ulong) offs} {(ulong) offs:X}");
            return _getCutVoLang(offs);
        }
        
        private void HandlePtr(ref byte* ptr)
        {
            var byteList = new List<byte>();
            int i = 0;
            while (ptr[i] != 0)
                byteList.Add(ptr[i++]);
            var byteArr = byteList.ToArray();
            
            // Write handlers, put them here
            var parsed = SeString.Parse(byteArr);
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
                || _configuration.Gender == GenderSetting.Model)
                return thisPayload;
            
            int femaleStart = 7;
            int femaleLen = reEncode[6] - 1;
            int maleStart = femaleStart + femaleLen + 2;
            int maleLen = reEncode[maleStart - 1] - 1;

            bool male;
            if (_configuration.Gender == GenderSetting.Random)
                male = new Random().Next(0, 2) == 0;
            else
                male = _configuration.Gender == GenderSetting.Male;
            
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

            return new TextPayload(GetNameText(_configuration.FullName));
        }

        private Payload HandleFirstNamePayload(Payload thisPayload)
        {
            byte[] reEncode = thisPayload.Encode();
            if (!ByteArrayEquals(reEncode, FirstNameBytes)) return thisPayload;
            
            return new TextPayload(GetNameText(_configuration.FirstName));
        }
        
        private Payload HandleLastNamePayload(Payload thisPayload)
        {
            byte[] reEncode = thisPayload.Encode();
            if (!ByteArrayEquals(reEncode, LastNameBytes)) return thisPayload;

            return new TextPayload(GetNameText(_configuration.LastName));
        }

        private string GetNameText(NameSetting setting)
        {
            var name = _configuration.Name;
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

        private void OnCommand(string command, string args)
        {
            _ui.SettingsVisible = true;
        }

        private void DrawUI()
        {
            _ui.Draw();
        }
        
        private void DrawConfigUI()
        {
            _ui.SettingsVisible = true;
        }
    }
}
