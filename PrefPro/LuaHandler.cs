using System;
using System.Globalization;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace PrefPro;

public unsafe class LuaHandler : IDisposable
{
	public delegate nuint LuaFunction(nuint a1);

	private readonly Hook<LuaFunction> _getRace;
	private readonly Hook<LuaFunction> _getSex;
	private readonly Hook<LuaFunction> _getTribe;

	private readonly byte* _luaRacePtr;
	private readonly byte* _luaSexPtr;
	private readonly byte* _luaTribePtr;

	private readonly Configuration _configuration;
	
	public LuaHandler(Configuration configuration)
	{
		_configuration = configuration;
		
		try
		{
			var raceFunctionAddress = GetAddress("return Pc.GetRace");
			var sexFunctionAddress = GetAddress("return Pc.GetSex");
			var tribeFunctionAddress = GetAddress("return Pc.GetTribe");
			
			_getRace = DalamudApi.Hooks.HookFromAddress<LuaFunction>(raceFunctionAddress, RaceFunctionDetour);
			_getSex = DalamudApi.Hooks.HookFromAddress<LuaFunction>(sexFunctionAddress, SexFunctionDetour);
			_getTribe = DalamudApi.Hooks.HookFromAddress<LuaFunction>(tribeFunctionAddress, TribeFunctionDetour);

			_luaRacePtr = (byte*)CodeUtil.GetStaticAddressFromPtr(raceFunctionAddress + 0x30);
			_luaSexPtr = (byte*)CodeUtil.GetStaticAddressFromPtr(sexFunctionAddress + 0x30);
			_luaTribePtr = (byte*)CodeUtil.GetStaticAddressFromPtr(tribeFunctionAddress + 0x30);
			
			DalamudApi.PluginLog.Debug($"[LuaHandler] Race function address: {raceFunctionAddress:X}");
			DalamudApi.PluginLog.Debug($"[LuaHandler] Sex function address: {sexFunctionAddress:X}");
			DalamudApi.PluginLog.Debug($"[LuaHandler] Tribe function address: {tribeFunctionAddress:X}");

			DalamudApi.PluginLog.Debug($"[LuaHandler] Race data address: {(nint) _luaRacePtr:X}");
			DalamudApi.PluginLog.Debug($"[LuaHandler] Sex data address: {(nint) _luaSexPtr:X}");
			DalamudApi.PluginLog.Debug($"[LuaHandler] Tribe data address: {(nint) _luaTribePtr:X}");
			
			_getRace.Enable();
			_getSex.Enable();
			_getTribe.Enable();
		}
		catch (Exception e)
		{
			DalamudApi.PluginLog.Error(e.ToString());
		}
	}

	public void Dispose()
	{
		_getRace?.Dispose();
		_getSex?.Dispose();
		_getTribe?.Dispose();
	}
    
	private nint GetAddress(string code) {
		var l = Framework.Instance()->LuaState.State;
		l->luaL_loadbuffer(code, code.Length, "test_chunk");
		if (l->lua_pcall(0, 1, 0) != 0)
			throw new Exception(l->lua_tostring(-1));
		var luaFunc = *(nint*)l->index2adr(-1);
		l->lua_pop(1);
		return *(nint*)(luaFunc + 0x20);
	}

	private nuint RaceFunctionDetour(nuint a1)
	{
		var oldRace = *_luaRacePtr;
		*_luaRacePtr = (byte)_configuration.Race;
		DalamudApi.PluginLog.Debug($"[RaceFunctionDetour] oldRace: {oldRace} race: {(byte)_configuration.Race}");
		var ret = _getRace.Original(a1);
		*_luaRacePtr = oldRace;
		return ret;
	}

	private nuint SexFunctionDetour(nuint a1)
	{
		var oldSex = *_luaSexPtr;
		*_luaSexPtr = (byte)_configuration.GetGender();
		DalamudApi.PluginLog.Debug($"[SexFunctionDetour] oldSex: {oldSex} sex: {(byte)_configuration.GetGender()}");
		var ret = _getSex.Original(a1);
		*_luaSexPtr = oldSex;
		return ret;
	}

	private nuint TribeFunctionDetour(nuint a1)
	{
		var oldTribe = *_luaTribePtr;
		*_luaTribePtr = (byte)_configuration.Tribe;
		DalamudApi.PluginLog.Debug($"[TribeFunctionDetour] oldTribe: {oldTribe} sex: {(byte)_configuration.Tribe}");
		var ret = _getTribe.Original(a1);
		*_luaTribePtr = oldTribe;
		return ret;
	}
}