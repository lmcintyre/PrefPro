using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Text;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Hooking;

namespace PrefPro
{
    public unsafe class PrefPro : IDalamudPlugin
    {
        public string Name => "Sample Plugin";

        private const string commandName = "/prefpro";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;

        private delegate int GetStringPrototype(void* unknown, byte* text, void* unknown2, void* stringStruct);
        private Hook<GetStringPrototype> GetStringHook; 

        public string PlayerGender
        {
            get
            {
                if (pi?.ClientState?.LocalPlayer?.Customize[(int)CustomizeIndex.Gender] == 0)
                    return "Male";
                if (pi?.ClientState?.LocalPlayer?.Customize[(int) CustomizeIndex.Gender] == 1)
                    return "Female";
                return "";
            }
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            pi = pluginInterface;
            
            configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pi);
            if (string.IsNullOrEmpty(configuration.SelectedGender))
                configuration.SelectedGender = PlayerGender;

            ui = new PluginUI(configuration);

            pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the PrefPro menu."
            });

            string getStringStr = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 83 B9 ?? ?? ?? ?? ?? 49 8B F9 49 8B F0 48 8B EA 48 8B D9 75 09 48 8B 01 FF 90 ?? ?? ?? ??";
            IntPtr getStringPtr = pi.TargetModuleScanner.ScanText(getStringStr);
            GetStringHook = new Hook<GetStringPrototype>(getStringPtr, (GetStringPrototype) GetStringDetour);
            
            GetStringHook.Enable();
            
            pi.UiBuilder.OnBuildUi += DrawUI;
            pi.UiBuilder.OnOpenConfigUi += (sender, args) => DrawConfigUI();
        }

        private int GetStringDetour(void* unknown, byte* text, void* unknown2, void* stringStruct)
        {
            int len = 0;
            byte* text2 = text;
            while (*text2 != 0) { text2++; len++; }
            
            // StringBuilder sb = new StringBuilder();
            // for (int i = 0; i < len; i++)
                // sb.Append($"{*(text + i):X} ");
            
            // PluginLog.Log($"GS Dump  : {sb.ToString()}");
            // PluginLog.Log($"GetString: {Encoding.ASCII.GetString(text, len)}");
            
            if (configuration.Enabled)
                ProcessGenderedParam(text);
            
            return GetStringHook.Original(unknown, text, unknown2, stringStruct);
        }
        
        private void ProcessGenderedParam(byte* ptr)
        {
            int len = 0;
            byte* text2 = ptr;
            while (*text2 != 0) { text2++; len++; }

            byte[] newText = new byte[len + 1];
            
            int currentPos = 0;

            for (int i = 0; i < len; i++)
            {
                if (ptr[i] == 2 && ptr[i + 1] == 8 && ptr[i + 3] == 0xE9 && ptr[i + 4] == 5)
                {
                    int codeStart = i;
                    int codeLen = codeStart;
                    while (ptr[codeLen] != 3) codeLen++;
                    codeLen -= codeStart;

                    int femaleStart = codeStart + 7;
                    int femaleLen = ptr[codeStart + 6] - 1;
                    int maleStart = femaleStart + femaleLen + 2;
                    int maleLen = ptr[maleStart - 1] - 1;
                    
                    if (configuration.SelectedGender == "Male")
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
