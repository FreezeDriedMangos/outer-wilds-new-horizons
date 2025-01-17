﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewHorizons.External.VariableSize
{
    public class RingModule : VariableSizeModule
    {
        public float InnerRadius { get; set; }
        public float OuterRadius { get; set; }
        public float Inclination { get; set; }
        public float LongitudeOfAscendingNode { get; set; }
        public string Texture { get; set; }
        public bool Unlit { get; set; } = false;
        public float RotationSpeed { get; set; } = 0f;
        public string FluidType { get; set; } 
    }
}
