using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HUDCountermeasureComp
{
    public static class HUDCountermeasureCompInfo
    {
        public const string PLUGIN_GUID = "HUDCountermeasureComp";
        public const string PLUGIN_NAME = "Countermeasure HUD";
        public const string PLUGIN_VERSION = "1.0.0";
    }


    [BepInPlugin(HUDCountermeasureCompInfo.PLUGIN_GUID, HUDCountermeasureCompInfo.PLUGIN_NAME, HUDCountermeasureCompInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public bool done = false;
        internal static ConfigEntry<float> HUDX;
        internal static ConfigEntry<float> HUDY;
        internal static ConfigEntry<bool> ShowPowerCharge;
        internal static ConfigEntry<string> BlacklistCMs;

        // ---- Blueprinter 兼容 ----
        private bool _blueprinterDetected;
        private BaseUnityPlugin _blueprinterInstance;

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {HUDCountermeasureCompInfo.PLUGIN_GUID} is loaded!");
            HUDX = Config.Bind(
                "HUD",
                "PositionX",
                -260f,
                "HUD X Position");

            HUDY = Config.Bind(
                "HUD",
                "PositionY",
                -140f,
                "HUD Y Position");

            ShowPowerCharge = Config.Bind(
                "HUD",
                "ShowPowerCharge",
                false,
                "Enable showing Capacitor Power Charge level in %");

            BlacklistCMs = Config.Bind(
                "HUD",
                "BlacklistCMs",
                "ECM;Jammer;Transmitter",
                "Keywords for blacklisting countermeasure stations in HUD. Separate multiple keywords with ';'");

            if (Chainloader.PluginInfos.TryGetValue("com.nikkorap.blueprinter", out var bpInfo))
            {
                _blueprinterDetected = true;
                _blueprinterInstance = bpInfo.Instance;
                Logger.LogInfo("Blueprinter detected, enabling compatibility functions");
            }

        }
        void Update()
        {
            if (done) return;
            if (_blueprinterDetected)
            {
                try
                {
                    var prop = _blueprinterInstance.GetType()
                    .GetProperty("PatchingComplete");
                    bool ready = (bool)prop.GetValue(_blueprinterInstance);
                    if (!ready)
                    {
                        Logger.LogInfo("Waiting for Blueprinter to finish patching...");
                        return;
                    }
                }
                catch (Exception e)
                {
                    _blueprinterDetected = false;
                    _blueprinterInstance = null;
                    Logger.LogInfo($"Blueprinter compatibility function meets error and has been disabled: {e}");
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

            if (root.Find("CountermeasureIndicator_Assassin1076") != null && root.Find("CountermeasureIndicator_Assassin1076").gameObject.GetComponent<CountermeasureIndicator>() != null)
            {
                Logger.LogInfo(
                    $"Countermeasure HUD already exists for {aircraftDef.name}, skipping...");
                return;
            }
            GameObject hudObj = new GameObject("CountermeasureIndicator_Assassin1076");
            hudObj.transform.SetParent(root, false);

            CountermeasuresIndicator indicator =
                hudObj.AddComponent<CountermeasuresIndicator>();

            Logger.LogInfo(
                $"Added Countermeasure HUD to {aircraftDef.name}");
        }
    }
    public class CountermeasuresIndicator : MonoBehaviour
    {
        public Aircraft host = null;

        private Text counterText;
        private RectTransform rect;

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
            }

            if (host.countermeasureManager == null)
                return;

            var stations =
                host.countermeasureManager.countermeasureStations;

            if (stations == null)
                return;

            StringBuilder sb = new StringBuilder();

            // sb.AppendLine("CM");

            string[] blacklistKeywords = Plugin.BlacklistCMs.Value
                .Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();

            foreach (var station in stations)
            {
                if (station == null)
                    continue;

                string name = station.displayName;
                bool blacklistedStation = false;

                foreach (string keyword in blacklistKeywords)
                {
                    if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        blacklistedStation = true;
                        break;
                    }
                }

                if (blacklistedStation)
                    continue;
                sb.AppendLine(
                    $"{name} {station.ammo}");
            }

            if (Plugin.ShowPowerCharge.Value)
            {
                float charge = 0f;

                try
                {
                    charge =
                        host.GetPowerSupply().GetCharge();
                }
                catch
                {
                }

                sb.AppendLine(
                    $"PowerCharge {charge * 100f:F0}%");

            }

            counterText.text = sb.ToString();
        }
    }
}
