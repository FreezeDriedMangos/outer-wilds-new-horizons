﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NewHorizons.Patches
{
    [HarmonyPatch]
    public static class MapControllerPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapController), nameof(MapController.Awake))]
        public static void MapController_Awake(MapController __instance, ref float ____maxPanDistance, ref float ____maxZoomDistance, ref float ____minPitchAngle, ref float ____zoomSpeed)
        {
            ____maxPanDistance = Main.FurthestOrbit * 1.5f;
            ____maxZoomDistance *= 6f;
            ____minPitchAngle = -90f;
            ____zoomSpeed *= 4f;
            __instance._mapCamera.farClipPlane = Main.FurthestOrbit * 10f;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapController), nameof(MapController.OnTargetReferenceFrame))]
        public static void MapController_OnTargetReferenceFrame(MapController __instance, ReferenceFrame __0)
        {
            __instance._isLockedOntoMapSatellite = true;
        }
    }
}
