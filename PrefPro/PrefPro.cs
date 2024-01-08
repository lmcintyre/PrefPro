using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.STD;
using PrefPro.Settings;

namespace PrefPro;

public unsafe class PrefPro : IDalamudPlugin
{
    private const string CommandName = "/prefpro";
        
    private readonly Configuration _configuration;
    private readonly PluginUI _ui;
        
    //reEncode[1] == 0x29 && reEncode[2] == 0x3 && reEncode[3] == 0xEB && reEncode[4] == 0x2
    private static readonly byte[] FullNameBytes = {0x02, 0x29, 0x03, 0xEB, 0x02, 0x03};
    private static readonly byte[] FirstNameBytes = {0x02, 0x2C, 0x0D, 0xFF, 0x07, 0x02, 0x29, 0x03, 0xEB, 0x02, 0x03, 0xFF, 0x02, 0x20, 0x02, 0x03};
    private static readonly byte[] LastNameBytes = {0x02, 0x2C, 0x0D, 0xFF, 0x07, 0x02, 0x29, 0x03, 0xEB, 0x02, 0x03, 0xFF, 0x02, 0x20, 0x03, 0x03};
        
    private delegate int GetStringPrototype(RaptureTextModule* textModule, byte* text, void* decoder, Utf8String* stringStruct);
    private readonly Hook<GetStringPrototype> _getStringHook;
        
    private delegate int GetCutVoGenderPrototype(void* a1, void* a2);
    private readonly Hook<GetCutVoGenderPrototype> _getCutVoGenderHook;
        
    private delegate int GetCutVoLangPrototype(void* a1);
    private readonly GetCutVoLangPrototype _getCutVoLang;

    private delegate byte GetLuaVarPrototype(nint poolBase, nint a2, nint a3);
    private readonly Hook<GetLuaVarPrototype> _getLuaVarHook;

    public string PlayerName => DalamudApi.ClientState.LocalPlayer?.Name.ToString();
    public int PlayerGender => DalamudApi.ClientState.LocalPlayer?.Customize[(int)CustomizeIndex.Gender] ?? 0;
    public RaceSetting PlayerRace => (RaceSetting) (DalamudApi.ClientState.LocalPlayer?.Customize[(int)CustomizeIndex.Race] ?? 0);
    public TribeSetting PlayerTribe => (TribeSetting) (DalamudApi.ClientState.LocalPlayer?.Customize[(int)CustomizeIndex.Tribe] ?? 0);
    
    public ulong CurrentPlayerContentId => DalamudApi.ClientState.LocalContentId;

    private uint _frameworkLangCallOffset = 0;
    private LuaHandler _luaHandler;

    public PrefPro(DalamudPluginInterface pi
    )
    {
        DalamudApi.Initialize(pi);
            
        _configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _configuration.Initialize(this);

        _ui = new PluginUI(_configuration, this);
            
        DalamudApi.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Display the PrefPro configuration interface.",
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

        var getLuaVar = "E8 ?? ?? ?? ?? 48 85 DB 74 1B 48 8D 8F";
        var getLuaVarPtr = DalamudApi.SigScanner.ScanText(getLuaVar);
        _getLuaVarHook = DalamudApi.Hooks.HookFromAddress<GetLuaVarPrototype>(getLuaVarPtr, GetLuaVarDetour);
            
        var frameworkLangCallOffsetStr = "48 8B 88 ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C 63 7E 24";
        var frameworkLangCallOffsetPtr = DalamudApi.SigScanner.ScanText(frameworkLangCallOffsetStr);
        _frameworkLangCallOffset = *(uint*)(frameworkLangCallOffsetPtr + 3);
        DalamudApi.PluginLog.Verbose($"framework lang call offset {_frameworkLangCallOffset} {_frameworkLangCallOffset:X}");

        _luaHandler = new LuaHandler(_configuration);

        // TODO: Include? no idea
        // if (frameworkLangCallOffset is < 10000 or > 14000)
        // {
        //     PluginLog.Error("Framework language call offset is invalid. The plugin will be disabled.");
        //     throw new InvalidOperationException();
        // }
            
        _getStringHook.Enable();
        _getCutVoGenderHook.Enable();
        _getLuaVarHook.Enable();
            
        DalamudApi.PluginInterface.UiBuilder.Draw += DrawUI;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        DalamudApi.ClientState.Login += OnLogin;
    }
    
    private void OnLogin()
    {
        _luaHandler = new LuaHandler(_configuration);
    }

    public void Dispose()
    {
        _ui.Dispose();
            
        _getStringHook?.Disable();
        _getStringHook?.Dispose();
        _getCutVoGenderHook?.Disable();
        _getCutVoGenderHook?.Dispose();
        _luaHandler?.Dispose();
        _getLuaVarHook?.Dispose();

        DalamudApi.CommandManager.RemoveHandler(CommandName);
    }

    private int GetStringDetour(RaptureTextModule* raptureTextModule, byte* text, void* unknown2, Utf8String* output)
    {
        // DalamudApi.PluginLog.Verbose($"[getStringDetour] {(nuint)raptureTextModule:X2} {(nuint)text:X2} {(nuint)unknown2:X2} {(nuint)stringStruct:X2}");
        if (!_configuration.Enabled)
            return _getStringHook.Original(raptureTextModule, text, unknown2, output);
                
        var decoderParams = ***(StdDeque<UnknownStruct>***) ((nuint) RaptureTextModule.Instance() + 0x40);

        var raceParam = decoderParams.Get(70);
        var oldRace = raceParam.Value;
        if (raceParam.Self == 0)
            return _getStringHook.Original(raptureTextModule, text, unknown2, output);
        var racePtr = (ulong*) raceParam.Self;
        *racePtr = (ulong)_configuration.Race;

        var genderParam = decoderParams.Get(3);
        var oldGender = genderParam.Value;
        if (genderParam.Self == 0)
            return _getStringHook.Original(raptureTextModule, text, unknown2, output);
        var genderPtr = (ulong*)genderParam.Self;
        *genderPtr = (ulong)_configuration.GetGender();
            
        HandleName(ref text);
        var result = _getStringHook.Original(raptureTextModule, text, unknown2, output);
        // Marshal.FreeHGlobal((IntPtr)text);

        raceParam = decoderParams.Get(70);
        racePtr = (ulong*) raceParam.Self;
        *racePtr = oldRace;

        genderParam = decoderParams.Get(3);
        genderPtr = (ulong*)genderParam.Self;
        *genderPtr = oldGender;
        
        return result;
    }
    
    private byte GetLuaVarDetour(nint poolBase, IntPtr a2, IntPtr a3)
    {
        var oldGender = GetLuaVarGender(poolBase);
        var newGender = _configuration.GetGender();
        SetLuaVarGender(poolBase, newGender);
        var returnValue = _getLuaVarHook.Original(poolBase, a2, a3);
        SetLuaVarGender(poolBase, oldGender);
        return returnValue;
    }

    private int GetLuaVarGender(nint poolBase)
    {
        var genderVarId = 0x1B;
        var gender = *(int*)(poolBase + 4 * genderVarId);
        return gender;
    }
    
    private void SetLuaVarGender(nint poolBase, int gender)
    {
        var genderVarId = 0x1B;
        *(int*)(poolBase + 4 * genderVarId) = gender;
    }
        
    private int GetCutVoGenderDetour(void* a1, void* a2)
    {
        var originalRet = _getCutVoGenderHook.Original(a1, a2);
        DalamudApi.PluginLog.Verbose($"[GetCutVoGenderDetour] original returned {originalRet}");

        if (!_configuration.Enabled)
            return originalRet;
            
        var lang = GetCutVoLang();
        DalamudApi.PluginLog.Verbose($"[GetCutVoGenderDetour] Lang returned {lang}");

        var v1 = *(int*) ((ulong)a2 + 28);
        var v2 = 12 * lang;
        var v3 = *(int*) ((ulong)a2 + (ulong)v1 + (ulong)v2);

        if (v3 == 1)
        {
            DalamudApi.PluginLog.Verbose($"[GetCutVoGenderDetour] v3 is 1");
            return 0;
        }
        
        return _configuration.GetGender();
    }

    private int GetCutVoLang()
    {
        var offs = *(void**) ((nint)Framework.Instance() + (int) _frameworkLangCallOffset);
        DalamudApi.PluginLog.Verbose($"[GetCutVoLang] {(ulong) offs} {(ulong) offs:X}");
        return _getCutVoLang(offs);
    }
    
    /**
     * This function is still necessary because of the name options provided in earlier versions.
     * So, we will never really be able to get rid of the string parsing in PrefPro.
     */
    private void HandleName(ref byte* ptr)
    {
        var firstApply = _configuration.FirstName != NameSetting.FirstOnly;
        var lastApply = _configuration.LastName != NameSetting.LastOnly;
        var fullApply = _configuration.FullName != NameSetting.FirstLast;
        var nameApply = _configuration.Name != PlayerName;
            
        if (!firstApply && !lastApply && !fullApply && !nameApply) return;
        
        var byteList = new List<byte>();
        int i = 0;
        while (ptr[i] != 0)
            byteList.Add(ptr[i++]);
        var byteArr = byteList.ToArray();
            
        var parsed = SeString.Parse(byteArr);
        for (int payloadIndex = 0; payloadIndex < parsed.Payloads.Count; payloadIndex++)
        {
            var thisPayload = parsed.Payloads[payloadIndex];
            if (thisPayload.Type == PayloadType.Unknown)
            {
                parsed.Payloads[payloadIndex] = HandleFullNamePayload(parsed.Payloads[payloadIndex]);
                parsed.Payloads[payloadIndex] = HandleFirstNamePayload(parsed.Payloads[payloadIndex]);
                parsed.Payloads[payloadIndex] = HandleLastNamePayload(parsed.Payloads[payloadIndex]);
            }
        }
        var encoded = parsed.Encode();

        if (ByteArrayEquals(encoded, byteArr)) return;
            
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
    
    private Payload HandleFullNamePayload(Payload thisPayload)
    {
        if (_configuration.FullName == NameSetting.FirstLast && _configuration.Name == PlayerName) return thisPayload;
        byte[] reEncode = thisPayload.Encode();
        if (!ByteArrayEquals(reEncode, FullNameBytes)) return thisPayload;

        return new TextPayload(GetNameText(_configuration.FullName));
    }

    private Payload HandleFirstNamePayload(Payload thisPayload)
    {
        if (_configuration.FirstName == NameSetting.FirstOnly && _configuration.Name == PlayerName) return thisPayload;
        byte[] reEncode = thisPayload.Encode();
        if (!ByteArrayEquals(reEncode, FirstNameBytes)) return thisPayload;
            
        return new TextPayload(GetNameText(_configuration.FirstName));
    }
        
    private Payload HandleLastNamePayload(Payload thisPayload)
    {
        if (_configuration.LastName == NameSetting.LastOnly && _configuration.Name == PlayerName) return thisPayload;
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