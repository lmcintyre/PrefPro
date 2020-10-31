using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PrefPro
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        
        public struct ConfigHolder
        {
            public bool Enabled;
            public string Name;
            public PrefPro.NameSetting FullName;
            public PrefPro.NameSetting FirstName;
            public PrefPro.NameSetting LastName;
            public PrefPro.GenderSetting Gender;
        }

        public Dictionary<ulong, ConfigHolder> Configs { get; set; } = new Dictionary<ulong, ConfigHolder>();

        [JsonIgnore]
        public bool Enabled
        {
            get => GetOrDefault().Enabled;
            set
            {
                var config = GetOrDefault();
                config.Enabled = value;
                Configs[_prefPro.CurrentPlayerContentId] = config;
            }
        }
        [JsonIgnore]
        public string Name
        {
            get => GetOrDefault().Name;
            set
            {
                var config = GetOrDefault();
                config.Name = value;
                Configs[_prefPro.CurrentPlayerContentId] = config;
            }
        }
        [JsonIgnore]
        public PrefPro.NameSetting FullName
        {
            get => GetOrDefault().FullName;
            set
            {
                var config = GetOrDefault();
                config.FullName = value;
                Configs[_prefPro.CurrentPlayerContentId] = config;
            }
        }
        [JsonIgnore]
        public PrefPro.NameSetting FirstName
        {
            get => GetOrDefault().FirstName;
            set
            {
                var config = GetOrDefault();
                config.FirstName = value;
                Configs[_prefPro.CurrentPlayerContentId] = config;
            }
        }
        [JsonIgnore]
        public PrefPro.NameSetting LastName
        {
            get => GetOrDefault().LastName;
            set
            {
                var config = GetOrDefault();
                config.LastName = value;
                Configs[_prefPro.CurrentPlayerContentId] = config;
            }
        }
        [JsonIgnore]
        public PrefPro.GenderSetting Gender
        {
            get => GetOrDefault().Gender;
            set
            {
                var config = GetOrDefault();
                config.Gender = value;
                Configs[_prefPro.CurrentPlayerContentId] = config;
            }
        }

        [NonSerialized] private DalamudPluginInterface _pluginInterface;
        [NonSerialized] private PrefPro _prefPro;

        public void Initialize(DalamudPluginInterface pluginInterface, PrefPro prefPro)
        {
            _pluginInterface = pluginInterface;
            _prefPro = prefPro;
        }

        public ConfigHolder GetOrDefault()
        {
            bool result = Configs.TryGetValue(_prefPro.CurrentPlayerContentId, out var holder);
            if (!result)
            {
                var ch = new ConfigHolder
                {
                    Name = _prefPro.PlayerName,
                    FullName = PrefPro.NameSetting.FirstLast,
                    FirstName = PrefPro.NameSetting.FirstOnly,
                    LastName = PrefPro.NameSetting.LastOnly,
                    Gender = PrefPro.GenderSetting.Model,
                    Enabled = false
                };
                return ch;
            }
            return holder;
        }

        public void Save()
        {
            _pluginInterface.SavePluginConfig(this);
        }
    }
}
