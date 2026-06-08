using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using InputFramework;
using Rewired;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static CountermeasureManager;

namespace CountermeasureIndex
{
    [BepInPlugin("Experimental.assassin1076.CountermeasureIndex", "CountermeasureIndex", "0.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public static ConfigEntry<bool> CompatibilityMode;
        public static ConfigEntry<string> FlareKeywords;
        public static ConfigEntry<string> ECMKeywords;
        private void Awake()
        {
            new Harmony("Experimental.assassin1076.CountermeasureIndex").PatchAll();
            CompatibilityMode = Config.Bind("General", "CompatibilityMode", 
                false, 
                "Enable compatibility mode for other mods that modify countermeasure behavior. This will disable simultaneous-use functionality for countermeasures");
            FlareKeywords = Config.Bind(
                "Countermeasure Search",
                "FlareKeywords",
                "Flare",
                "Keywords used to locate flare countermeasure stations. Separate multiple keywords with ';'"
            );

            ECMKeywords = Config.Bind(
                "Countermeasure Search",
                "ECMKeywords",
                "ECM",
                "Keywords used to locate ECM countermeasure stations. Separate multiple keywords with ';'"
            );

            // Plugin startup logic
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployFlares",
                InputActionType.Button,
                "Flight"
            );
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployECM",
                InputActionType.Button,
                "Flight"
            );
            Logger = base.Logger;
            Logger.LogInfo($"Plugin Experimental.assassin1076.CountermeasureIndex is loaded!");
        }
    }
    public static class CountermeasureMaskUtil
    {
        public static int LowestBitIndex(byte mask)
        {
            for (int i = 0; i < 8; i++)
                if ((mask & (1 << i)) != 0)
                    return i;
            return -1;
        }

        public static bool IsSingleBit(byte mask)
            => mask != 0 && (mask & (mask - 1)) == 0;

        public static byte SingleBit(int index)
            => (byte)(1 << index);
    }

    public static class CountermeasureSearchUtil
    {
        public static int FindStationIndex(
            List<CountermeasureStation> stations,
            string keywordConfig)
        {
            if (stations == null)
                return -1;

            string[] keywords = keywordConfig
                .Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();

            return stations.FindIndex(station =>
            {
                string name = station.displayName ?? "";

                foreach (string keyword in keywords)
                {
                    if (name.IndexOf(keyword,
                            System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                return false;
            });
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), nameof(PilotPlayerState.UpdateState))]
    public static class PlayerInputCatcher
    {
        static void Postfix(PilotPlayerState __instance, Pilot pilot)
        {
            Rewired.Player player = ReInput.players.GetPlayer(0);
            bool useFlares = player.GetButton("CountermeasureIndex::DeployFlares");
            bool useECM = player.GetButton("CountermeasureIndex::DeployECM");

            var stations = pilot.aircraft.countermeasureManager.countermeasureStations;

            int indexFlares = CountermeasureSearchUtil.FindStationIndex(
                stations,
                Plugin.FlareKeywords.Value);

            int indexECM = CountermeasureSearchUtil.FindStationIndex(
                stations,
                Plugin.ECMKeywords.Value);


            if (Plugin.CompatibilityMode.Value)
            {
                if (useFlares && indexFlares >= 0)
                {
                    pilot.aircraft.countermeasureManager.activeIndex = (byte)indexFlares;
                    if (!pilot.aircraft.countermeasureTrigger)
                    {
                        pilot.aircraft.Countermeasures(active: true, pilot.aircraft.countermeasureManager.activeIndex);
                    }
                }
                else if (useECM && indexECM >= 0)
                {
                    pilot.aircraft.countermeasureManager.activeIndex = (byte)indexECM;
                    if (!pilot.aircraft.countermeasureTrigger)
                    {
                        pilot.aircraft.Countermeasures(active: true, pilot.aircraft.countermeasureManager.activeIndex);
                    }
                }
            }
            else
            {
                byte mask = 0;
                bool flag = false;
                if (useFlares && indexFlares >= 0)
                {
                    flag = true;
                    mask |= (byte)(1 << indexFlares);
                }
                if (useECM && indexECM >= 0)
                {
                    flag = true;
                    mask |= (byte)(1 << indexECM);
                }

                if (flag)
                {
                    pilot.aircraft.countermeasureManager.activeIndex = mask;
                    if (!pilot.aircraft.countermeasureTrigger)
                    {
                        pilot.aircraft.Countermeasures(active: true, pilot.aircraft.countermeasureManager.activeIndex);
                    }
                }
            }
            return;
        }
    }

    [HarmonyPatch(typeof(CountermeasureManager), nameof(CountermeasureManager.NextCountermeasure))]
    public static class NextCountermeasure_Patch
    {
        static bool Prefix(CountermeasureManager __instance)
        {
            if(Plugin.CompatibilityMode.Value) return true;

            var stations = __instance.countermeasureStations;
            if (stations == null || stations.Count == 0) return false;

            int current = CountermeasureMaskUtil.LowestBitIndex(__instance.activeIndex);
            int next = (current + 1) % stations.Count;

            __instance.activeIndex = CountermeasureMaskUtil.SingleBit(next);

            __instance.aircraft
                ?.Countermeasures(false, __instance.activeIndex);

            stations[next].SetActive(__instance.aircraft);
            return false;
        }
    }

    [HarmonyPatch(typeof(CountermeasureManager), nameof(CountermeasureManager.DeployCountermeasure))]
    public static class DeployCountermeasure_Patch
    {
        static bool Prefix(CountermeasureManager __instance, Aircraft aircraft)
        {
            if (Plugin.CompatibilityMode.Value) return true;

            byte mask = __instance.activeIndex;
            if (mask == 0 || mask == 0xFF) return false;

            var stations = __instance.countermeasureStations;
            if (stations == null) return false;

            for (int i = 0; i < stations.Count && i < 8; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    stations[i].Fire(aircraft);
                }
            }
            return false;
        }
    }


    [HarmonyPatch(typeof(CountermeasureManager), nameof(CountermeasureManager.UpdateHUD))]
    public static class UpdateHUD_Patch
    {
        static bool Prefix(CountermeasureManager __instance)
        {
            if (Plugin.CompatibilityMode.Value) return true;

            var stations = __instance.countermeasureStations;
            if (stations == null || stations.Count == 0) return false;

            int index = CountermeasureMaskUtil.LowestBitIndex(__instance.activeIndex);
            if (index >= 0 && index < stations.Count)
            {
                stations[index].SetActive(__instance.aircraft);
            }
            return false;
        }
    }


    [HarmonyPatch(typeof(CountermeasureManager), nameof(CountermeasureManager.ChooseCountermeasure))]
    public static class ChooseCountermeasure_Patch
    {
        static bool Prefix(
            CountermeasureManager __instance,
            Missile missileThreat,
            ref string __result)
        {
            if (Plugin.CompatibilityMode.Value) return true;

            string seekerType = missileThreat.GetSeekerType();
            byte mask = 0;

            var stations = __instance.countermeasureStations;
            if (stations == null)
            {
                __result = string.Empty;
                return false;
            }

            for (int i = 0; i < stations.Count && i < 8; i++)
            {
                var threats = stations[i].threatTypes;
                if (threats != null && threats.Contains(seekerType))
                {
                    mask |= (byte)(1 << i);
                }
            }

            __instance.activeIndex = mask != 0 ? mask : byte.MaxValue;

            __instance.aircraft
                ?.Countermeasures(false, __instance.activeIndex);

            __result = mask != 0 ? seekerType : string.Empty;
            return false;
        }
    }


    [HarmonyPatch(typeof(CountermeasureManager), nameof(CountermeasureManager.GetActiveCountermeasure))]
    public static class GetActiveCountermeasure_Patch
    {
        static bool Prefix(CountermeasureManager __instance, ref Countermeasure __result)
        {
            if (Plugin.CompatibilityMode.Value) return true;

            var stations = __instance.countermeasureStations;
            if (stations == null || stations.Count == 0)
            {
                __result = null;
                return false;
            }

            int index = CountermeasureMaskUtil.LowestBitIndex(__instance.activeIndex);
            if (index >= 0 && index < stations.Count)
            {
                __result = stations[index].GetFirstCountermeasure() as Countermeasure;
            }
            else
            {
                __result = null;
            }
            return false;
        }
    }


}
