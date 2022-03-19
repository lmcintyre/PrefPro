using ImGuiNET;
using System;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;

namespace PrefPro
{
    class PluginUI : IDisposable
    {
        private readonly Configuration _configuration;
        private readonly PrefPro _prefPro;
        private bool _settingsVisible;

        private const string FirstLastDesc = "First name, then last";
        private const string FirstOnlyDesc = "First name only";
        private const string LastOnlyDesc  = "Last name only";
        private const string LastFirstDesc = "Last name, then first";

        private const string MaleDesc = "Male";
        private const string FemaleDesc = "Female";
        private const string RandomDesc = "Random gender";
        private const string ModelDesc = "Model gender";

        private string _tmpFirstName = "";
        private string _tmpLastName = "";

        public bool SettingsVisible
        {
            get => _settingsVisible;
            // set => _settingsVisible = value;
            set
            {
                if (value)
                {
                    var split = _configuration.Name.Split(' ');
                    _tmpFirstName = split[0];
                    _tmpLastName = split[1];
                }
                _settingsVisible = value;
            }
        }

        public PluginUI(Configuration configuration, PrefPro prefPro)
        {
            _configuration = configuration;
            _prefPro = prefPro;
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
            
            ImGui.SetNextWindowSize(new Vector2(340 * ImGui.GetIO().FontGlobalScale, 360 * ImGui.GetIO().FontGlobalScale), ImGuiCond.Always);
            if (ImGui.Begin("PrefPro Config", ref _settingsVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                var enabled = _configuration.Enabled;
                var currentGender = _configuration.Gender;
                
                var nameFull = _configuration.FullName;
                var nameFirst = _configuration.FirstName;
                var nameLast = _configuration.LastName;
                
                if (ImGui.Checkbox("Enable PrefPro", ref enabled))
                {
                    _configuration.Enabled = enabled;
                    _configuration.Save();
                }

                if (ImGui.CollapsingHeader("Developer note regarding they/them pronouns"))
                {
                    ImGui.TextWrapped("PrefPro currently cannot and will never have support for they/them pronouns. " +
                                      "This is entirely due to technical limitations and the amount of work for such a feature. " +
                                      "This would require rewriting most dialogue in the game across all languages as well as upkeep on new patches. " +
                                      "It is simply an unreasonable amount of work.");
                    ImGui.End();
                    return;
                }

                ImGui.Text("For name replacement, PrefPro should use the name...");
                ImGui.Indent(10f * ImGuiHelpers.GlobalScale);
                ImGui.PushItemWidth(105f * ImGuiHelpers.GlobalScale);
                ImGui.InputText("##newFirstName", ref _tmpFirstName, 15);
                ImGui.SameLine();
                ImGui.InputText("##newLastName", ref _tmpLastName, 15);
                ImGui.PopItemWidth();
                ImGui.PushItemWidth(20f * ImGuiHelpers.GlobalScale);
                ImGui.SameLine();
                if (ImGui.Button("Set##prefProNameSet"))
                {
                    string setName = SanitizeName(_tmpFirstName, _tmpLastName);
                    _configuration.Name = setName;
                    var split = setName.Split(' ');
                    _tmpFirstName = split[0];
                    _tmpLastName = split[1];
                    _configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Reset##prefProNameReset"))
                {
                    string resetName = _prefPro.PlayerName;
                    _configuration.Name = resetName;
                    var split = resetName.Split(' ');
                    _tmpFirstName = split[0];
                    _tmpLastName = split[1];
                    _configuration.Save();
                }
                ImGui.PopItemWidth();
                ImGui.Indent(-10f * ImGuiHelpers.GlobalScale);
                
                ImGui.Text("When NPCs and dialogue use my full name, instead use...");
                ImGui.Indent(10f * ImGuiHelpers.GlobalScale);
                ImGui.PushItemWidth(300f * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo("##fullnameCombo", GetNameOptionDescriptor(nameFull)))
                {
                    if (ImGui.Selectable(FirstLastDesc))
                        _configuration.FullName = PrefPro.NameSetting.FirstLast;
                    if (ImGui.Selectable(FirstOnlyDesc))
                        _configuration.FullName = PrefPro.NameSetting.FirstOnly;
                    if (ImGui.Selectable(LastOnlyDesc))
                        _configuration.FullName = PrefPro.NameSetting.LastOnly;
                    if (ImGui.Selectable(LastFirstDesc))
                        _configuration.FullName = PrefPro.NameSetting.LastFirst;
                    _configuration.Save();
                    ImGui.EndCombo();
                }
                ImGui.Indent(-10f * ImGuiHelpers.GlobalScale);
                
                ImGui.Text("When NPCs and dialogue use my first name, instead use...");
                ImGui.Indent(10f * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo("##firstNameCombo", GetNameOptionDescriptor(nameFirst)))
                {
                    if (ImGui.Selectable(FirstLastDesc))
                        _configuration.FirstName = PrefPro.NameSetting.FirstLast;
                    if (ImGui.Selectable(FirstOnlyDesc))
                        _configuration.FirstName = PrefPro.NameSetting.FirstOnly;
                    if (ImGui.Selectable(LastOnlyDesc))
                        _configuration.FirstName = PrefPro.NameSetting.LastOnly;
                    if (ImGui.Selectable(LastFirstDesc))
                        _configuration.FirstName = PrefPro.NameSetting.LastFirst;
                    _configuration.Save();
                    ImGui.EndCombo();
                }
                ImGui.Indent(-10f * ImGuiHelpers.GlobalScale);
                
                ImGui.Text("When NPCs and dialogue use my last name, instead use...");
                ImGui.Indent(10f * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo("##lastNameCombo", GetNameOptionDescriptor(nameLast)))
                {
                    if (ImGui.Selectable(FirstLastDesc))
                        _configuration.LastName = PrefPro.NameSetting.FirstLast;
                    if (ImGui.Selectable(FirstOnlyDesc))
                        _configuration.LastName = PrefPro.NameSetting.FirstOnly;
                    if (ImGui.Selectable(LastOnlyDesc))
                        _configuration.LastName = PrefPro.NameSetting.LastOnly;
                    if (ImGui.Selectable(LastFirstDesc))
                        _configuration.LastName = PrefPro.NameSetting.LastFirst;
                    _configuration.Save();
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();
                ImGui.Indent(-10f * ImGuiHelpers.GlobalScale);

                ImGui.TextWrapped("When NPCs and dialogue use gendered text, instead refer to me" +
                                  " as if my character is...");

                ImGui.Indent(10f * ImGuiHelpers.GlobalScale);
                ImGui.PushItemWidth(140 * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo("##prefProComboBox:", GetGenderOptionDescriptor(currentGender)))
                {
                    if (ImGui.Selectable(MaleDesc))
                        _configuration.Gender = PrefPro.GenderSetting.Male;
                    if (ImGui.Selectable(FemaleDesc))
                        _configuration.Gender = PrefPro.GenderSetting.Female;
                    if (ImGui.Selectable(RandomDesc))
                        _configuration.Gender = PrefPro.GenderSetting.Random;
                    if (ImGui.Selectable(ModelDesc))
                        _configuration.Gender = PrefPro.GenderSetting.Model;
                    _configuration.Save();
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();
                ImGui.Indent(10f * ImGuiHelpers.GlobalScale);
            }
            ImGui.End();
        }
        
        private string SanitizeName(string first, string last)
        {
            string newFirst = first;
            string newLast = last;
            
            // Save the last valid name for fail cases
            string lastValid = _configuration.Name;
            
            if (newFirst.Length > 15 || newLast.Length > 15)
                return lastValid;
            string combined = $"{newFirst}{newLast}";
            if (combined.Length > 20)
                return lastValid;

            newFirst = Regex.Replace(newFirst, "[^A-Za-z'\\-\\s{1}]", "");
            newLast = Regex.Replace(newLast, "[^A-Za-z'\\-\\s{1}]", "");

            return $"{newFirst} {newLast}";
        }

        private string GetNameOptionDescriptor(PrefPro.NameSetting setting)
        {
            return setting switch
            {
                PrefPro.NameSetting.FirstLast => FirstLastDesc,
                PrefPro.NameSetting.FirstOnly => FirstOnlyDesc,
                PrefPro.NameSetting.LastOnly => LastOnlyDesc,
                PrefPro.NameSetting.LastFirst => LastFirstDesc,
                _ => ""
            };
        }

        private string GetGenderOptionDescriptor(PrefPro.GenderSetting setting)
        {
            return setting switch
            {
                PrefPro.GenderSetting.Male => MaleDesc,
                PrefPro.GenderSetting.Female => FemaleDesc,
                PrefPro.GenderSetting.Random => RandomDesc,
                PrefPro.GenderSetting.Model => ModelDesc,
                _ => ""
            };
        }
    }
}
