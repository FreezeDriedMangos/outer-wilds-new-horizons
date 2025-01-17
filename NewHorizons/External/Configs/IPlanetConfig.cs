﻿using NewHorizons.External.VariableSize;
using NewHorizons.Utility;

namespace NewHorizons.External.Configs
{
    public interface IPlanetConfig
    {
        string Name { get; }
        string StarSystem { get; }
        bool Destroy { get; }
        string[] ChildrenToDestroy { get; }
        int BuildPriority { get; }
        bool CanShowOnTitle { get; }
        bool IsQuantumState { get; }
        BaseModule Base { get; }
        AtmosphereModule Atmosphere { get; }
        OrbitModule Orbit { get; }
        RingModule Ring { get; }
        HeightMapModule HeightMap { get; }
        ProcGenModule ProcGen { get; }
        AsteroidBeltModule AsteroidBelt { get; }
        StarModule Star { get; }
        FocalPointModule FocalPoint { get; }
        PropModule Props { get; }
        ShipLogModule ShipLog { get; }
        SpawnModule Spawn { get; }
        SignalModule Signal { get; }
        SingularityModule Singularity { get; }
        LavaModule Lava { get; }
        SandModule Sand { get; }
        WaterModule Water { get; }
        FunnelModule Funnel { get; }
    }
}
