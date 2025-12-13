using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.Jobs;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MoreAirspeedGauge
{
    public enum SpeedDisplayMode
    {
        IAS,
        TAS,
        EAS,
        GS
    }
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]

    public class SpeedGaugePlugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public static ConfigEntry<KeyboardShortcut> Switch { get; set; }
        public static ConfigEntry<KeyboardShortcut> CycleGauge { get; set; }
        public static ConfigEntry<SpeedDisplayMode> DisplayMode;
        public static bool MainSwitch = true;
        private Harmony _harmony;
        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Switch = Config.Bind("Hotkeys", "Switch speed gauge replace on/off", new KeyboardShortcut(KeyCode.M, KeyCode.LeftControl));
            CycleGauge = Config.Bind("Hotkeys", "Cycle Speed Display Mode", new KeyboardShortcut(KeyCode.F7));
            DisplayMode = Config.Bind("SpeedGauge","Speed Display Mode",SpeedDisplayMode.IAS);
            _harmony = new Harmony("misc.assassin1076.speedgaugepatch");
            _harmony.PatchAll();
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }
        private void Update()
        {
            if (CycleGauge.Value.IsDown())
            {
                DisplayMode.Value = (SpeedDisplayMode)(((int)DisplayMode.Value + 1) % System.Enum.GetValues(typeof(SpeedDisplayMode)).Length);
            }
            if (Switch.Value.IsDown())
            {
                MainSwitch = !MainSwitch;
            }
        }
    }
    public static class ChartHelperCompat
    {
        public static float SafeRead(float index, NativeArray<float> chart)
        {
            if (chart.Length == 0)
                throw new System.ArgumentException("Chart array is empty");

            // NaN / Infinity 检查
            if (float.IsNaN(index))
            {
                Debug.LogWarning("Index was NaN, returning last element");
                return chart[chart.Length - 1];
            }
            if (float.IsInfinity(index))
            {
                Debug.LogWarning("Index was Infinity, returning last element");
                return chart[chart.Length - 1];
            }

            // 边界处理
            if (index <= 0f)
                return chart[0];
            if (index >= chart.Length - 1)
                return chart[chart.Length - 1];

            // 线性插值
            int lower = Mathf.FloorToInt(index);
            int upper = Mathf.CeilToInt(index);
            float t = index - lower;
            return Mathf.Lerp(chart[lower], chart[upper], t);
        }
    }
    [HarmonyPatch(typeof(SpeedGauge))]
    [HarmonyPatch("Refresh")]
    public class SpeedGauge_Refresh_Patch
    {
        private static System.Reflection.FieldInfo aircraftField = typeof(SpeedGauge).GetField("aircraft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static System.Reflection.FieldInfo airspeedDisplayField = typeof(SpeedGauge).GetField("airspeedDisplay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static float rho0 = ChartHelperCompat.SafeRead(4f * 0.0021f, LevelInfo.airDensityChart);
        public static void Postfix(SpeedGauge __instance)
        {
            if (!SpeedGaugePlugin.MainSwitch) return;
            try
            {
                // Get private field airspeedDisplay via reflection
                var field = typeof(SpeedGauge).GetField("airspeedDisplay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var aircraft = aircraftField.GetValue(__instance) as Aircraft;
                var textUI = airspeedDisplayField?.GetValue(__instance) as Text;
                if (textUI == null || aircraft == null)
                    return;


                float density = ChartHelperCompat.SafeRead(aircraft.GlobalPosition().y * 0.0021f, LevelInfo.airDensityChart);
                var wind = NetworkSceneSingleton<LevelInfo>.i.GetWind();
                var GS_vec = aircraft.rb.velocity;
                var velRel = GS_vec - wind;
                float GS = GS_vec.magnitude;
                float TAS = velRel.magnitude;
                float IAS = TAS * Mathf.Sqrt(density / rho0);
                float EAS = IAS;


                float result = 0f;
                switch (SpeedGaugePlugin.DisplayMode.Value)
                {
                    case SpeedDisplayMode.IAS: result = IAS; break;
                    case SpeedDisplayMode.TAS: result = TAS; break;
                    case SpeedDisplayMode.EAS: result = EAS; break;
                    case SpeedDisplayMode.GS: result = GS; break;
                }
                string resultStr = UnitConverter.SpeedReading(result);

                textUI.text = $"{SpeedGaugePlugin.DisplayMode.Value} {resultStr}";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Postfix error: {ex}");
            }
        }

    }
}
