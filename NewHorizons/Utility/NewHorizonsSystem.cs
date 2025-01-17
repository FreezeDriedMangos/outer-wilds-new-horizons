﻿using NewHorizons.External;
using OWML.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewHorizons.External.Configs;

namespace NewHorizons.Utility
{
    public class NewHorizonsSystem
    {
        public NewHorizonsSystem(string name, StarSystemConfig config, IModBehaviour mod)
        {
            Name = name;
            Config = config;
            Mod = mod;
        }

        public string Name;
        public SpawnModule Spawn = null;
        public SpawnPoint SpawnPoint = null;
        public StarSystemConfig Config;
        public IModBehaviour Mod;
    }
}
