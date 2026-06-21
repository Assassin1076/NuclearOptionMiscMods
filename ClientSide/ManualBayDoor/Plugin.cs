using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Rewired;
using System.Collections.Generic;
using System.Diagnostics.PerformanceData;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.CoreUtils;
namespace ManualBayDoor
{
    public static class ManualBayDoorCompInfo
    {
        public const string PLUGIN_GUID = "ManualBayDoorComp";
        public const string PLUGIN_NAME = "ManualBayDoor";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInDependency("com.nikkorap.blueprinter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(ManualBayDoorCompInfo.PLUGIN_GUID, ManualBayDoorCompInfo.PLUGIN_NAME, ManualBayDoorCompInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public bool done = false;
        public bool blueprinterDetected = false;
        public BaseUnityPlugin blueprinterInstance = null;
        internal static ConfigEntry<float> HUDX;
        internal static ConfigEntry<float> HUDY;
        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {ManualBayDoorCompInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony(ManualBayDoorCompInfo.PLUGIN_GUID);
            harmony.PatchAll();
            RewiredActionInjector.RegisterAction("ManualBayDoor::CycleBayDoorSelection", Rewired.InputActionType.Button, "Flight");
            RewiredActionInjector.RegisterAction("ManualBayDoor::ToggleBayDoor", Rewired.InputActionType.Button, "Flight");
            HUDX = Config.Bind(
                "HUD",
                "PositionX",
                400f,
                "HUD X Position");

            HUDY = Config.Bind(
                "HUD",
                "PositionY",
                400f,
                "HUD Y Position");
            if (Chainloader.PluginInfos.TryGetValue("com.nikkorap.blueprinter", out var info))
            {
                Logger.LogInfo("启用 Blueprinter 兼容功能");
                blueprinterDetected = true;
                blueprinterInstance = info.Instance;
            }
        }
        void Update()
        {
            if (done) return;
            if (blueprinterDetected)
            {
                var prop = blueprinterInstance.GetType()
                    .GetProperty("PatchingComplete");

                bool ready = (bool)prop.GetValue(blueprinterInstance);
                if(!ready)
                {
                    Logger.LogInfo("Waiting for Blueprinter to finish patching...");
                    return;
                }
            }
            if (Encyclopedia.i == null)
            {
                Logger.LogInfo("Waiting for target prefabs to load...");
                return;
            }
            foreach (AircraftDefinition aircraft in Encyclopedia.i.aircraft)
            {
                testFunc(aircraft, aircraft.aircraftParameters.HUDExtras.transform);
                //要获取Aircraft类，通过aircraft.unitPrefab.GetComponent<Aircraft>即可
            }

            done = true;
        }

        public void testFunc(AircraftDefinition aircraftDef, Transform root)
        {
            Aircraft aircraft = aircraftDef.unitPrefab.GetComponent<Aircraft>();

            if (aircraft == null)
                return;

            if (root.Find("BaydoorIndicator_Assassin1076") != null && root.Find("BaydoorIndicator_Assassin1076").gameObject.GetComponent<BaydoorIndicator>() != null)
            {
                Logger.LogInfo(
                    $"Baydoor HUD already exists for {aircraftDef.name}, skipping...");
                return;
            }
            GameObject hudObj = new GameObject("BaydoorIndicator_Assassin1076");
            hudObj.transform.SetParent(root, false);
            
            BaydoorIndicator indicator =
                hudObj.AddComponent<BaydoorIndicator>();

            Logger.LogInfo(
                $"Added Baydoor HUD to {aircraftDef.name}");
        }
    }

    public class BaydoorManager : MonoBehaviour
    {
        public Aircraft host = null;

        private bool hardpointsCached = false;
        public List<Hardpoint> hardpointsHasBaydoor = new List<Hardpoint>();
        public int selectedIndex = 0;
        public List<int> openedBays = new List<int>();
        void Update()
        {
            if (host == null)
            {
                host = SceneSingleton<CombatHUD>.i.aircraft;
                return;
            }

            if (host.weaponManager == null) { return; }

            if (!hardpointsCached)
            {
                try
                {
                    foreach (var hardpointSet in host.weaponManager.hardpointSets)
                    {
                        foreach (var item in hardpointSet.hardpoints)
                        {
                            var ret = item.GetCargoDoor();
                            if (ret != null)
                            {
                                hardpointsHasBaydoor.Add(item);
                            }
                        }
                    }
                    hardpointsCached = true;
                }
                catch (System.Exception e)
                {

                    Debug.LogError($"Failed to cache hardpoints, skipping baydoor indicator update: {e}");
                }

                return;
            }

            Rewired.Player player = ReInput.players.GetPlayer(0);
            bool cycle = player.GetButtonTimedPressUp("ManualBayDoor::CycleBayDoorSelection", 0f, PlayerSettings.clickDelay);
            bool toggle = player.GetButtonTimedPressUp("ManualBayDoor::ToggleBayDoor", 0f, PlayerSettings.clickDelay);

            if (cycle)
            {
                if (hardpointsHasBaydoor.Count > 0)
                {
                    selectedIndex = (selectedIndex + 1) % hardpointsHasBaydoor.Count;
                }
                else
                {
                    SceneSingleton<AircraftActionsReport>.i.ReportText("Aircraft do not have any baydoor", 1f);
                }
            }

            if (toggle && selectedIndex >= 0 && selectedIndex < hardpointsHasBaydoor.Count)
            {
                if (openedBays.Contains(selectedIndex))
                {
                    foreach (var item in hardpointsHasBaydoor[selectedIndex].bayDoors)
                    {
                        if(item != null)
                        {
                            item.openTimer = 0f;
                        }
                    }
                    openedBays.Remove(selectedIndex);
                    SceneSingleton<AircraftActionsReport>.i.ReportText($"Closing Baydoor: {hardpointsHasBaydoor[selectedIndex].bayDoors[0].gameObject.name}", 1f);

                }
                else
                {
                    openedBays.Add(selectedIndex);
                    SceneSingleton<AircraftActionsReport>.i.ReportText($"Opening Baydoor: {hardpointsHasBaydoor[selectedIndex].bayDoors[0].gameObject.name}", 1f);
                }

            }

            MaintainOpenBays();
            
        }

        void MaintainOpenBays()
        {
            foreach (int index in openedBays)
            {
                if (index >= 0 && index < hardpointsHasBaydoor.Count)
                {
                    foreach (var item in hardpointsHasBaydoor[index].bayDoors)
                    {
                        if (item != null)
                        {
                            item.OpenDoor(1f);
                        }
                    }
                }
            }
        }
    }

    public class BaydoorIndicator : MonoBehaviour
    {
        public Aircraft host = null;

        private BaydoorManager manager;
        private Text counterText;
        private RectTransform rect;
        private bool inited = false;
        
        void Awake()
        {
            rect = gameObject.AddComponent<RectTransform>();

            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);

            rect.anchoredPosition =
                new Vector2(
                    Plugin.HUDX.Value,
                    Plugin.HUDY.Value);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(transform, false);

            counterText = txtObj.AddComponent<Text>();

            counterText.font =
                Resources.GetBuiltinResource<Font>("Arial.ttf");

            counterText.fontSize = 18;
            counterText.alignment = TextAnchor.UpperRight;

            counterText.horizontalOverflow =
                HorizontalWrapMode.Overflow;

            counterText.verticalOverflow =
                VerticalWrapMode.Overflow;

            counterText.color = Color.green;
            counterText.supportRichText = true;
            RectTransform txtRect =
                txtObj.GetComponent<RectTransform>();

            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
        }

        void Update()
        {
            if (host == null)
            {
                host = SceneSingleton<CombatHUD>.i.aircraft;
                return;
            }

            if (!inited)
            {
                manager = host.gameObject.AddComponent<BaydoorManager>();
                inited = true;
            }

            StringBuilder sb = new StringBuilder();
            if (manager.selectedIndex >= 0 && manager.selectedIndex < manager.hardpointsHasBaydoor.Count)
            {
                sb.AppendLine($"SelectedBaydoor: {manager.hardpointsHasBaydoor[manager.selectedIndex].bayDoors[0].gameObject.name}");
            }
            if (manager.openedBays.Count > 0)
            {
                sb.AppendLine("OpenedBaydoor: ");
                foreach (var item in manager.openedBays)
                {
                    sb.AppendLine(manager.hardpointsHasBaydoor[item].bayDoors[0].gameObject.name);
                }
            }
            counterText.text = sb.ToString();
        }

        
    }
}
