﻿using NewHorizons.Utility;
namespace NewHorizons.External.Modules
{
    public class SpawnModule
    {
        public MVector3 PlayerSpawnPoint { get; set; }
        public MVector3 ShipSpawnPoint { get; set; }
        public bool StartWithSuit { get; set; }
    }
}