using ImGuiNET;
using System;
using System.Numerics;

namespace PrefPro
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;
        
        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return settingsVisible; }
            set { settingsVisible = value; }
        }

        public PluginUI(Configuration configuration)
        {
            this.configuration = configuration;
        }

        public void Dispose()
        {
			
        }

        public void Draw()
        {
            DrawSettingsWindow();
        }

        public void DrawSettingsWindow()
        {
            if (!SettingsVisible) return;
            
            ImGui.SetNextWindowSize(new Vector2(270, 100), ImGuiCond.Always);
            if (ImGui.Begin("PrefPro Config", ref settingsVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                var enabled = configuration.Enabled;
                var currentGender = configuration.SelectedGender;

                if (ImGui.Checkbox("Enable PrefPro", ref enabled))
                {
                    configuration.Enabled = enabled;
                    configuration.Save();
                }
                
                ImGui.Text("Refer to my character as:");
                ImGui.SameLine();
                
                ImGui.PushItemWidth(80);
                
                if (ImGui.BeginCombo("##prefProComboBox:", currentGender))
                {
                    if (ImGui.Selectable("Male"))
                        configuration.SelectedGender = "Male";
                    if (ImGui.Selectable("Female"))
                        configuration.SelectedGender = "Female";
                    configuration.Save();
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();
            }
            ImGui.End();
        }
    }
}
