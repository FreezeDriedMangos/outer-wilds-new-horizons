﻿using NewHorizons.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewHorizons.External.Configs
{
    public class StarSystemConfig : Config
    {
        public bool canEnterViaWarpDrive = true;
        public bool startHere = false;
        public bool destroyStockPlanets = true;
        public string factRequiredForWarp;
        public NomaiCoordinates coords;
        public SkyboxConfig skybox;

        public class NomaiCoordinates
        {
            public int[] x;
            public int[] y;
            public int[] z;
        }

        public class SkyboxConfig
        {
            public string assetBundle = null;
            public string path = null;
            public bool destroyStarField = false;
        }

        public StarSystemConfig(Dictionary<string, object> dict) : base(dict) { }
        
    }
}
