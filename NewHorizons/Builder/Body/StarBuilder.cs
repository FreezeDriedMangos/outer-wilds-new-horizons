using NewHorizons.External;
using OWML.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using NewHorizons.Utility;
using Logger = NewHorizons.Utility.Logger;
using NewHorizons.External.VariableSize;
using NewHorizons.Components;
using NewHorizons.Components.SizeControllers;

namespace NewHorizons.Builder.Body
{
    static class StarBuilder
    {
        public const float OuterRadiusRatio = 1.5f;

        private static Texture2D _colorOverTime;
        public static StarController Make(GameObject body, Sector sector, StarModule starModule)
        {
            if (_colorOverTime == null) _colorOverTime = ImageUtilities.GetTexture(Main.Instance, "AssetBundle/StarColorOverTime.png");

            var starGO = GameObject.Find("Sun_Body").InstantiateInactive();
            starGO.name = "Star";
            starGO.transform.parent = body.transform;
            starGO.transform.localPosition = Vector3.zero;

            // These are already done on the planet
            GameObject.Destroy(starGO.transform.Find("GravityWell_SUN").gameObject);
            GameObject.Destroy(starGO.transform.Find("RFVolume_SUN").gameObject);
            Component.Destroy(starGO.GetComponent<AstroObject>());
            Component.Destroy(starGO.GetComponent<QuantumOrbit>());
            Component.Destroy(starGO.GetComponent<OWRigidbody>());
            Component.Destroy(starGO.GetComponent<KinematicRigidbody>());
            Component.Destroy(starGO.GetComponent<Rigidbody>());
            Component.Destroy(starGO.GetComponent<CenterOfTheUniverseOffsetApplier>());

            if (!starModule.HasAtmosphere) GameObject.Destroy(starGO.transform.Find("Atmosphere_SUN"));

            // Sun controller
            var sunController = starGO.GetComponent<SunController>();
            sunController._rfVolume = body.GetComponent<ReferenceFrameVolume>();

            var sunAudio = GameObject.Instantiate(GameObject.Find("Sun_Body/Sector_SUN/Audio_SUN"), starGO.transform);
            sunAudio.transform.localPosition = Vector3.zero;
            sunAudio.transform.localScale = Vector3.one;
            sunAudio.transform.Find("SurfaceAudio_Sun").GetComponent<AudioSource>().maxDistance = starModule.Size * 2f;
            var surfaceAudio = sunAudio.GetComponentInChildren<SunSurfaceAudioController>();
            surfaceAudio.SetSector(sector);
            surfaceAudio._sunController = null;

            sunAudio.name = "Audio_Star";

            if(starModule.HasAtmosphere)
            {
                var sunAtmosphere = starGO.transform.Find("Atmosphere_SUN").gameObject;
                PlanetaryFogController fog = sunAtmosphere.transform.Find("FogSphere").GetComponent<PlanetaryFogController>();
                if (starModule.Tint != null)
                {
                    fog.fogTint = starModule.Tint.ToColor();
                    sunAtmosphere.transform.Find("AtmoSphere").transform.localScale = Vector3.one * (starModule.Size * OuterRadiusRatio);
                    foreach (var lod in sunAtmosphere.transform.Find("AtmoSphere").GetComponentsInChildren<MeshRenderer>())
                    {
                        lod.material.SetColor("_SkyColor", starModule.Tint.ToColor());
                        lod.material.SetFloat("_InnerRadius", starModule.Size);
                        lod.material.SetFloat("_OuterRadius", starModule.Size * OuterRadiusRatio);
                    }
                }
                fog.transform.localScale = Vector3.one;
                fog.fogRadius = starModule.Size * OuterRadiusRatio;
                fog.lodFadeDistance = fog.fogRadius * (StarBuilder.OuterRadiusRatio - 1f);
                if (starModule.Curve != null)
                {
                    var controller = sunAtmosphere.AddComponent<StarAtmosphereSizeController>();
                    controller.scaleCurve = starModule.ToAnimationCurve();
                    controller.initialSize = starModule.Size;
                }
            }

            var light = starGO.transform.Find("Sector_SUN/Effects_SUN/SunLight").GetComponent<Light>();
            light.intensity *= starModule.SolarLuminosity;
            light.range *= Mathf.Sqrt(starModule.SolarLuminosity);

            Light ambientLight = starGO.transform.Find("AmbientLight_SUN").GetComponent<Light>();

            Color lightColour = light.color;
            if (starModule.LightTint != null) lightColour = starModule.LightTint.ToColor();
            if (lightColour == null && starModule.Tint != null)
            {
                // Lighten it a bit
                var r = Mathf.Clamp01(starModule.Tint.R * 1.5f);
                var g = Mathf.Clamp01(starModule.Tint.G * 1.5f);
                var b = Mathf.Clamp01(starModule.Tint.B * 1.5f);
                lightColour = new Color(r, g, b);
            }
            if (lightColour != null) light.color = (Color)lightColour;

            light.color = lightColour;
            ambientLight.color = lightColour;

            if(starModule.Tint != null)
            {
                var colour = starModule.Tint.ToColor();

                var startMaterial = new Material(sunController._startSurfaceMaterial);
                var endMaterial = new Material(sunController._endSurfaceMaterial);

                var mod = Mathf.Max(0.5f, 2f * Mathf.Sqrt(starModule.SolarLuminosity));
                var adjustedColour = new Color(colour.r * mod, colour.g * mod, colour.b * mod);
                Color.RGBToHSV(adjustedColour, out float H, out float S, out float V);
                var darkenedColor = Color.HSVToRGB(H, S, V * 0.05f);

                var colorRamp = ImageUtilities.LerpGreyscaleImage(_colorOverTime, adjustedColour, darkenedColor);

                startMaterial.SetTexture("_ColorRamp", colorRamp);
                endMaterial.SetTexture("_ColorRamp", colorRamp);

                startMaterial.color = adjustedColour;
                endMaterial.color = adjustedColour;

                sunController._startSurfaceMaterial = startMaterial;
                sunController._endSurfaceMaterial = endMaterial;
            }

            if(starModule.SolarFlareTint != null)
            {
                var solarFlareEmitter = starGO.transform.Find("Sector_SUN/Effects_SUN/SolarFlareEmitter");
                solarFlareEmitter.GetComponent<SolarFlareEmitter>().tint = starModule.SolarFlareTint.ToColor();
            }

            StarController starController = null;
            if (starModule.SolarLuminosity != 0)
            {
                starController = body.AddComponent<StarController>();
                starController.Light = light;
                starController.AmbientLight = ambientLight;
                starController.FaceActiveCamera = starGO.GetComponentInChildren<FaceActiveCamera>();
                starController.CSMTextureCacher = starGO.GetComponentInChildren<CSMTextureCacher>();
                starController.ProxyShadowLight = starGO.GetComponentInChildren<ProxyShadowLight>();
                starController.Intensity = starModule.SolarLuminosity;
                starController.SunColor = lightColour;
            }

            if (starModule.Curve != null)
            {
                var levelController = starGO.AddComponent<SandLevelController>();
                var curve = new AnimationCurve();
                foreach (var pair in starModule.Curve)
                {
                    curve.AddKey(new Keyframe(pair.Time, starModule.Size * pair.Value));
                }
                levelController._scaleCurve = curve;
            }

            // Fix sizes
            var size = Vector3.one * starModule.Size;
            sunController._surface.transform.localScale = size;
            sunController._origSurfaceScale = size;
            starGO.transform.Find("Sector_SUN/Effects_SUN/SolarFlareEmitter").localScale = size;

            sunController._ambientLightOuterRadius = 3000 * starModule.SolarLuminosity;
            
            sunController._atmosphereInnerRadius = starModule.HasAtmosphere ? 2000 * starModule.SolarLuminosity : 0;
            sunController._atmosphereOuterRadius = starModule.HasAtmosphere ? 3000 * starModule.SolarLuminosity : 0;

            sunController._rfVolume = body.GetComponentInChildren<ReferenceFrameVolume>();

            // Make the sun proxy

            var starProxyGO = Resources.Load("SunProxy") as GameObject;
            starGO.name = "StarProxy";
            starGO.transform.parent = body.transform;
            starGO.transform.localPosition = Vector3.zero;

            var proxy = starProxyGO.GetComponent<SunProxy>();
            proxy._sunTransform = body.transform;
            proxy._realSunController = sunController;

            var proxyEffectController = starProxyGO.GetComponentInChildren<SunProxyEffectController>();
            sunController._sunProxyEffects = proxyEffectController;

            //starGO.SetActive(true);
            //starProxyGO.SetActive(true);

            return starController;
        }
    }
}

