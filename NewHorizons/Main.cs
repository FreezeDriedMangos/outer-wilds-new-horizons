﻿using NewHorizons.Builder.Body;
using NewHorizons.Builder.General;
using NewHorizons.Builder.Orbital;
using NewHorizons.Builder.Props;
using NewHorizons.Builder.ShipLog;
using NewHorizons.Components;
using NewHorizons.External;
using NewHorizons.External.Configs;
using NewHorizons.External.VariableSize;
using NewHorizons.Handlers;
using NewHorizons.Utility;
using Newtonsoft.Json.Linq;
using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OWML.Common.Menus;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = NewHorizons.Utility.Logger;
using NewHorizons.Builder.Atmosphere;
using UnityEngine.Events;
using HarmonyLib;
using System.Reflection;

namespace NewHorizons
{
    public class Main : ModBehaviour
    {
        public static AssetBundle ShaderBundle;
        public static Main Instance { get; private set; }

        // Settings
        public static bool Debug;
        private static bool _useCustomTitleScreen;
        private static bool _wasConfigured = false;

        public static Dictionary<string, NewHorizonsSystem> SystemDict = new Dictionary<string, NewHorizonsSystem>();
        public static Dictionary<string, List<NewHorizonsBody>> BodyDict = new Dictionary<string, List<NewHorizonsBody>>();
        public static Dictionary<string, AssetBundle> AssetBundles = new Dictionary<string, AssetBundle>();
        public static List<IModBehaviour> MountedAddons = new List<IModBehaviour>();

        public static bool IsSystemReady { get; private set; }
        public static float FurthestOrbit { get; set; } = 50000f;

        public string CurrentStarSystem { get { return Instance._currentStarSystem; } }
        public bool IsWarping { get; private set; } = false;
        public bool WearingSuit { get; private set; } = false;

        public static bool HasWarpDrive { get; private set; } = false;

        private string _defaultStarSystem = "SolarSystem";
        private string _currentStarSystem = "SolarSystem";
        private bool _isChangingStarSystem = false;
        private bool _firstLoad = true;
        private ShipWarpController _shipWarpController;

        // API events
        public class StarSystemEvent : UnityEvent<string> { }
        public StarSystemEvent OnChangeStarSystem;
        public StarSystemEvent OnStarSystemLoaded;

        // For warping to the eye system
        private GameObject _ship;

        public static bool HasDLC { get => EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.Owned; }

        public override object GetApi()
        {
            return new NewHorizonsApi();
        }

        public override void Configure(IModConfig config)
        {
            Logger.Log("Settings changed");

            Debug = config.GetSettingsValue<bool>("Debug");
            DebugReload.UpdateReloadButton();
            Logger.UpdateLogLevel(Debug ? Logger.LogType.Log : Logger.LogType.Error);

            var wasUsingCustomTitleScreen = _useCustomTitleScreen;
            _useCustomTitleScreen = config.GetSettingsValue<bool>("Custom title screen");
            // Reload the title screen if this was updated on it
            // Don't reload if we haven't configured yet (called on game start)
            if (wasUsingCustomTitleScreen != _useCustomTitleScreen && SceneManager.GetActiveScene().name == "TitleScreen" && _wasConfigured)
            {
                Logger.Log("Reloading");
                SceneManager.LoadScene("TitleScreen", LoadSceneMode.Single);
            }

            _wasConfigured = true;
        }

        public static void ResetConfigs(bool resetTranslation = true)
        {
            BodyDict.Clear();
            SystemDict.Clear();

            BodyDict["SolarSystem"] = new List<NewHorizonsBody>();
            BodyDict["EyeOfTheUniverse"] = new List<NewHorizonsBody>(); // Keep this empty tho fr
            SystemDict["SolarSystem"] = new NewHorizonsSystem("SolarSystem", new StarSystemConfig(null), Instance)
            {
                Config =
                {
                    destroyStockPlanets = false
                }
            };
            foreach (AssetBundle bundle in AssetBundles.Values)
            {
                bundle.Unload(true);
            }
            AssetBundles.Clear();
            if (!resetTranslation) return;
            TranslationHandler.ClearTables();
            TextTranslation.Get().SetLanguage(TextTranslation.Get().GetLanguage());
        }

        public void Start()
        {
            // Patches
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            OnChangeStarSystem = new StarSystemEvent();
            OnStarSystemLoaded = new StarSystemEvent();

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            Instance = this;
            GlobalMessenger<DeathType>.AddListener("PlayerDeath", OnDeath);
            GlobalMessenger.AddListener("WakeUp", new Callback(OnWakeUp));
            ShaderBundle = Main.Instance.ModHelper.Assets.LoadBundle("AssetBundle/shader");
            
            ResetConfigs(resetTranslation: false);
            
            Logger.Log("Begin load of config files...", Logger.LogType.Log);

            try
            {
                LoadConfigs(this);
            }
            catch (Exception)
            {
                Logger.LogWarning("Couldn't find planets folder");
            }

            Instance.ModHelper.Events.Unity.FireOnNextUpdate(() => OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single));
            Instance.ModHelper.Events.Unity.FireOnNextUpdate(() => _firstLoad = false);
            Instance.ModHelper.Menus.PauseMenu.OnInit += DebugReload.InitializePauseMenu;
        }        

        public void OnDestroy()
        {
            Logger.Log($"Destroying NewHorizons");
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GlobalMessenger<DeathType>.RemoveListener("PlayerDeath", OnDeath);
            GlobalMessenger.RemoveListener("WakeUp", new Callback(OnWakeUp));
        }

        private static void OnWakeUp()
        {
            IsSystemReady = true;
            Instance.OnStarSystemLoaded?.Invoke(Instance.CurrentStarSystem);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            SearchUtilities.ClearCache();
            ImageUtilities.ClearCache();
            AudioUtilities.ClearCache();
            IsSystemReady = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.Log($"Scene Loaded: {scene.name} {mode}");

            _isChangingStarSystem = false;

            if (scene.name == "TitleScreen" && _useCustomTitleScreen)
            {
                TitleSceneHandler.DisplayBodyOnTitleScreen(BodyDict.Values.ToList().SelectMany(x => x).ToList());
            }

            if(scene.name == "EyeOfTheUniverse" && IsWarping)
            {
                if(_ship != null) SceneManager.MoveGameObjectToScene(_ship, SceneManager.GetActiveScene());
                _ship.transform.position = new Vector3(50, 0, 0);
                _ship.SetActive(true);
            }

            if(scene.name == "SolarSystem")
            {
                foreach(var body in GameObject.FindObjectsOfType<AstroObject>())
                {
                    Logger.Log($"{body.name}, {body.transform.rotation}");
                }

                if(_ship != null)
                {
                    _ship = GameObject.Find("Ship_Body").InstantiateInactive();
                    DontDestroyOnLoad(_ship);
                }

                IsSystemReady = false;

                NewHorizonsData.Load();
                SignalBuilder.Init();
                AstroObjectLocator.Init();
                OWAssetHandler.Init();
                PlanetCreationHandler.Init(BodyDict[CurrentStarSystem]);
                SystemCreationHandler.LoadSystem(SystemDict[CurrentStarSystem]);
                LoadTranslations(ModHelper.Manifest.ModFolderPath + "AssetBundle/", this);

                Instance.ModHelper.Events.Unity.FireOnNextUpdate(() => Locator.GetPlayerBody().gameObject.AddComponent<DebugRaycaster>());

                // Warp drive
                StarChartHandler.Init(SystemDict.Values.ToArray());
                HasWarpDrive = StarChartHandler.CanWarp();
                _shipWarpController = GameObject.Find("Ship_Body").AddComponent<ShipWarpController>();
                _shipWarpController.Init();
                if (HasWarpDrive == true) EnableWarpDrive();

                if (IsWarping && _shipWarpController)
                {
                    Instance.ModHelper.Events.Unity.RunWhen(
                        () => IsSystemReady, 
                        () => _shipWarpController.WarpIn(WearingSuit)
                    );
                }
                else
                {
                    Instance.ModHelper.Events.Unity.RunWhen(
                        () => IsSystemReady, 
                        () => FindObjectOfType<PlayerSpawner>().DebugWarp(SystemDict[_currentStarSystem].SpawnPoint)
                    );
                }
                IsWarping = false;

                var map = GameObject.FindObjectOfType<MapController>();
                if (map != null) map._maxPanDistance = FurthestOrbit * 1.5f;

                // Fix the map satellite
                GameObject.Find("HearthianMapSatellite_Body").AddComponent<MapSatelliteOrbitFix>();
            }
            else
            {
                // Reset back to original solar system after going to main menu.
                _currentStarSystem = _defaultStarSystem;
            }
        }

        public void EnableWarpDrive()
        {
            Logger.Log("Setting up warp drive");
            PlanetCreationHandler.LoadBody(LoadConfig(this, "AssetBundle/WarpDriveConfig.json"));
            HasWarpDrive = true;
        }


        #region Load
        public void LoadConfigs(IModBehaviour mod)
        {
            try
            {
                if (_firstLoad)
                {
                    MountedAddons.Add(mod);
                }
                var folder = mod.ModHelper.Manifest.ModFolderPath;

                // Load systems first so that when we load bodies later we can check for missing ones
                if (Directory.Exists(folder + @"systems\"))
                {
                    foreach (var file in Directory.GetFiles(folder + @"systems\", "*.json", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);

                        Logger.Log($"Loading system {name}");

                        var relativePath = file.Replace(folder, "");
                        var starSystemConfig = mod.ModHelper.Storage.Load<StarSystemConfig>(relativePath);

                        var system = new NewHorizonsSystem(name, starSystemConfig, mod);
                        SystemDict[name] = system;
                    }
                }
                if (Directory.Exists(folder + "planets"))
                {
                    foreach (var file in Directory.GetFiles(folder + @"planets\", "*.json", SearchOption.AllDirectories))
                    {
                        var relativeDirectory = file.Replace(folder, "");
                        var body = LoadConfig(mod, relativeDirectory);

                        if (body != null)
                        {
                            // Wanna track the spawn point of each system
                            if (body.Config.Spawn != null) SystemDict[body.Config.StarSystem].Spawn = body.Config.Spawn;

                            // Add the new planet to the planet dictionary
                            if (!BodyDict.ContainsKey(body.Config.StarSystem)) BodyDict[body.Config.StarSystem] = new List<NewHorizonsBody>();
                            BodyDict[body.Config.StarSystem].Add(body);
                        }
                    }
                }
                if (Directory.Exists(folder + @"translations\"))
                {
                    LoadTranslations(folder, mod);
                }
            }
            catch(Exception ex)
            {
                Logger.LogError($"{ex.Message}, {ex.StackTrace}");
            }
        }

        private void LoadTranslations(string folder, IModBehaviour mod)
        {
            var foundFile = false;
            foreach (TextTranslation.Language language in Enum.GetValues(typeof(TextTranslation.Language)))
            {
                if (language == TextTranslation.Language.UNKNOWN || language == TextTranslation.Language.TOTAL) continue;

                var relativeFile = $"translations/{language.ToString().ToLower()}.json";

                if (File.Exists($"{folder}{relativeFile}"))
                {
                    Logger.Log($"Registering {language} translation from {mod.ModHelper.Manifest.Name} from {relativeFile}");

                    var config = new TranslationConfig($"{folder}{relativeFile}");
                    if (config == null)
                    {
                        Logger.Log($"Found {folder}{relativeFile} but couldn't load it");
                        continue;
                    }

                    foundFile = true;

                    TranslationHandler.RegisterTranslation(language, config);
                }
            }
            if (!foundFile) Logger.LogWarning($"{mod.ModHelper.Manifest.Name} has a folder for translations but none were loaded");
        }

        public NewHorizonsBody LoadConfig(IModBehaviour mod, string relativeDirectory)
        {
            NewHorizonsBody body = null;
            try
            {
                var config = mod.ModHelper.Storage.Load<PlanetConfig>(relativeDirectory);
                Logger.Log($"Loaded {config.Name}");
                if (config.Base.CenterOfSolarSystem) config.Orbit.IsStatic = true;
                if (!SystemDict.ContainsKey(config.StarSystem))
                {
                    // See if theres a star system config
                    var starSystemConfig = mod.ModHelper.Storage.Load<StarSystemConfig>($"systems/{config.StarSystem}.json");
                    if (starSystemConfig == null) starSystemConfig = new StarSystemConfig(null);
                    else Logger.Log($"Loaded system config for {config.StarSystem}");

                    var system = new NewHorizonsSystem(config.StarSystem, starSystemConfig, mod);

                    if (system.Config.startHere) SetDefaultSystem(system.Name);

                    SystemDict.Add(config.StarSystem, system);

                    BodyDict.Add(config.StarSystem, new List<NewHorizonsBody>());
                }

                body = new NewHorizonsBody(config, mod);
            }
            catch (Exception e)
            {
                Logger.LogError($"Couldn't load {relativeDirectory}: {e.Message}, is your Json formatted correctly?");
            }

            return body;
        }

        public void SetDefaultSystem(string defaultSystem)
        {
            _defaultStarSystem = defaultSystem;
            _currentStarSystem = defaultSystem;
        }

        #endregion Load

        #region Change star system
        public void ChangeCurrentStarSystem(string newStarSystem, bool warp = false)
        {
            if (_isChangingStarSystem) return;

            OnChangeStarSystem?.Invoke(newStarSystem);

            Logger.Log($"Warping to {newStarSystem}");
            if(warp && _shipWarpController) _shipWarpController.WarpOut();
            _currentStarSystem = newStarSystem;
            _isChangingStarSystem = true;
            IsWarping = warp;
            WearingSuit = PlayerState.IsWearingSuit();

            // We kill them so they don't move as much
            Locator.GetDeathManager().KillPlayer(DeathType.Meditation);

            if(newStarSystem == "EyeOfTheUniverse")
            {
                PlayerData.SaveWarpedToTheEye(60);
                LoadManager.LoadSceneAsync(OWScene.EyeOfTheUniverse, true, LoadManager.FadeType.ToBlack, 0.1f, true);
            }
            else
            {
                LoadManager.LoadSceneAsync(OWScene.SolarSystem, true, LoadManager.FadeType.ToBlack, 0.1f, true);
            }
        }

        void OnDeath(DeathType _)
        {
            // We reset the solar system on death (unless we just killed the player)
            if (!_isChangingStarSystem)
            {
                _currentStarSystem = _defaultStarSystem;
                IsWarping = false;
            }
        }
        #endregion Change star system
    }
}
