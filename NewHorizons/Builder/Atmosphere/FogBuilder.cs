﻿using NewHorizons.External;
using OWML.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Logger = NewHorizons.Utility.Logger;

namespace NewHorizons.Builder.Atmosphere
{
    public static class FogBuilder
    {
        public static void Make(GameObject planetGO, Sector sector, AtmosphereModule atmo)
        {
            GameObject fogGO = new GameObject("FogSphere");
            fogGO.SetActive(false);
            fogGO.transform.parent = sector?.transform ?? planetGO.transform;
            fogGO.transform.localScale = Vector3.one;

            // Going to copy from dark bramble
            var dbFog = GameObject.Find("DarkBramble_Body/Atmosphere_DB/FogLOD");
            var dbPlanetaryFogController = GameObject.Find("DarkBramble_Body/Atmosphere_DB/FogSphere_DB").GetComponent<PlanetaryFogController>();
            var brambleLODFog = GameObject.Find("DarkBramble_Body/Sector_DB/Proxy_DB/LOD_DB_VolumeticFog");

            MeshFilter MF = fogGO.AddComponent<MeshFilter>();
            MF.mesh = dbFog.GetComponent<MeshFilter>().mesh;

            MeshRenderer MR = fogGO.AddComponent<MeshRenderer>();
            MR.materials = dbFog.GetComponent<MeshRenderer>().materials;
            MR.allowOcclusionWhenDynamic = true;

            PlanetaryFogController PFC = fogGO.AddComponent<PlanetaryFogController>();
            PFC.fogLookupTexture = dbPlanetaryFogController.fogLookupTexture;
            PFC.fogRadius = atmo.FogSize;
            PFC.fogDensity = atmo.FogDensity;
            PFC.fogExponent = 1f;
            PFC.fogColorRampTexture = dbPlanetaryFogController.fogColorRampTexture;
            PFC.fogColorRampIntensity = 1f;
            PFC.fogTint = atmo.FogTint.ToColor();

            GameObject lodFogGO = new GameObject("LODFogSphere");
            lodFogGO.SetActive(false);
            lodFogGO.transform.parent = fogGO.transform;
            lodFogGO.transform.localScale = Vector3.one * atmo.Size / 320f;

            MeshFilter lodMF = lodFogGO.AddComponent<MeshFilter>();
            lodMF.mesh = brambleLODFog.GetComponent<MeshFilter>().mesh;

            MeshRenderer lodMR = lodFogGO.AddComponent<MeshRenderer>();
            lodMR.material = new Material(brambleLODFog.GetComponent<MeshRenderer>().material);
            lodMR.material.color = atmo.FogTint.ToColor();
            lodMR.material.renderQueue = 1000;

            /*
            SectorProxy lodFogSectorProxy = lodFogGO.AddComponent<SectorProxy>();
            lodFogSectorProxy._renderers = new List<Renderer> { lodMR };
            lodFogSectorProxy.SetSector(sector);
            */

            fogGO.transform.position = planetGO.transform.position;
            lodFogGO.transform.position = planetGO.transform.position;

            fogGO.SetActive(true);
            lodFogGO.SetActive(true);
        }
    }
}
