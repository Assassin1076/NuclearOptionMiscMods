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
using System.Reflection.Emit;
using UnityEngine;
using static CountermeasureManager;

namespace CountermeasureIndex
{
    [BepInPlugin("Experimental.assassin1076.CountermeasureIndex", "CountermeasureIndex", "0.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public static ConfigEntry<bool> CompatibilityMode;
        public static ConfigEntry<string> CM1Keywords;
        public static ConfigEntry<string> CM2Keywords;
        public static ConfigEntry<string> CM3Keywords;
        public static ConfigEntry<string> CM4Keywords;
        public static ConfigEntry<string> CM5Keywords;
        public static ConfigEntry<string> CM6Keywords;
        public static ConfigEntry<string> CM7Keywords;
        public static ConfigEntry<string> CM8Keywords;
        private void Awake()
        {
            CompatibilityMode = Config.Bind("General", "CompatibilityMode", 
                false, 
                "Enable compatibility mode for other mods that modify countermeasure behavior. This will disable simultaneous-use functionality for countermeasures");
            CM1Keywords = Config.Bind(
                "Countermeasure Search",
                "CM1Keywords",
                "Flare",
                "Keywords for countermeasure station 1. Separate multiple keywords with ';'"
            );

            CM2Keywords = Config.Bind(
                "Countermeasure Search",
                "CM2Keywords",
                "ECM",
                "Keywords for countermeasure station 2. Separate multiple keywords with ';'"
            );

            CM3Keywords = Config.Bind(
                "Countermeasure Search",
                "CM3Keywords",
                "",
                "Keywords for countermeasure station 3. Separate multiple keywords with ';'"
            );

            CM4Keywords = Config.Bind(
                "Countermeasure Search",
                "CM4Keywords",
                "",
                "Keywords for countermeasure station 4. Separate multiple keywords with ';'"
            );

            CM5Keywords = Config.Bind(
                "Countermeasure Search",
                "CM5Keywords",
                "",
                "Keywords for countermeasure station 5. Separate multiple keywords with ';'"
            );

            CM6Keywords = Config.Bind(
                "Countermeasure Search",
                "CM6Keywords",
                "",
                "Keywords for countermeasure station 6. Separate multiple keywords with ';'"
            );

            CM7Keywords = Config.Bind(
                "Countermeasure Search",
                "CM7Keywords",
                "",
                "Keywords for countermeasure station 7. Separate multiple keywords with ';'"
            );

            CM8Keywords = Config.Bind(
                "Countermeasure Search",
                "CM8Keywords",
                "",
                "Keywords for countermeasure station 8. Separate multiple keywords with ';'"
            );

            new Harmony("Experimental.assassin1076.CountermeasureIndex").PatchAll();

            // Plugin startup logic
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployCM1",
                InputActionType.Button,
                "Flight"
            );
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployCM2",
                InputActionType.Button,
                "Flight"
            );
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployCM3",
                InputActionType.Button,
                "Flight"
            );
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployCM4",
                InputActionType.Button,
                "Flight"
            );
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployCM5",
                InputActionType.Button,
                "Flight"
            );
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployCM6",
                InputActionType.Button,
                "Flight"
            );
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployCM7",
                InputActionType.Button,
                "Flight"
            );
            ExtraInputManager.RegisterAction(
                "CountermeasureIndex::DeployCM8",
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

    [HarmonyPatch(typeof(PilotPlayerState), nameof(PilotPlayerState.PlayerControls))]
    public static class Patch_PlayerInput_Countermeasures
    {
        private static readonly MethodInfo CountermeasuresMethod =
            AccessTools.Method(
                typeof(Aircraft),
                nameof(Aircraft.Countermeasures),
                new[] { typeof(bool), typeof(byte) });

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(CountermeasuresMethod))
                {
                    // 原栈:
                    // aircraft
                    // bool
                    // byte

                    yield return new CodeInstruction(OpCodes.Pop); // byte
                    yield return new CodeInstruction(OpCodes.Pop); // bool
                    yield return new CodeInstruction(OpCodes.Pop); // aircraft

                    continue;
                }

                yield return instruction;
            }
        }

    }

    [HarmonyPatch(typeof(PilotPlayerState), nameof(PilotPlayerState.UpdateState))]
    public static class PlayerInputCatcher
    {
        private static readonly string[] CMActionNames = new string[]
        {
            "CountermeasureIndex::DeployCM1",
            "CountermeasureIndex::DeployCM2",
            "CountermeasureIndex::DeployCM3",
            "CountermeasureIndex::DeployCM4",
            "CountermeasureIndex::DeployCM5",
            "CountermeasureIndex::DeployCM6",
            "CountermeasureIndex::DeployCM7",
            "CountermeasureIndex::DeployCM8",
        };

        // Track our own activation state to avoid jitter caused by
        // reading back countermeasureTrigger (a SyncVar with latency).
        private static bool _cmActive = false;

        private static string GetCMKeywords(int index)
        {
            switch (index)
            {
                case 0: return Plugin.CM1Keywords.Value;
                case 1: return Plugin.CM2Keywords.Value;
                case 2: return Plugin.CM3Keywords.Value;
                case 3: return Plugin.CM4Keywords.Value;
                case 4: return Plugin.CM5Keywords.Value;
                case 5: return Plugin.CM6Keywords.Value;
                case 6: return Plugin.CM7Keywords.Value;
                case 7: return Plugin.CM8Keywords.Value;
                default: return string.Empty;
            }
        }

        static void Postfix(PilotPlayerState __instance, Pilot pilot)
        {
            Rewired.Player player = ReInput.players.GetPlayer(0);
            var stations = pilot.aircraft.countermeasureManager.countermeasureStations;

            if (Plugin.CompatibilityMode.Value)
            {
                bool anyButtonPressed = false;
                for (int i = 0; i < CMActionNames.Length; i++)
                {
                    if (player.GetButton(CMActionNames[i]))
                    {
                        int idx = CountermeasureSearchUtil.FindStationIndex(
                            stations,
                            GetCMKeywords(i));
                        if (idx >= 0)
                        {
                            pilot.aircraft.countermeasureManager.activeIndex = (byte)idx;
                            anyButtonPressed = true;
                            break;
                        }
                    }
                }

                if (player.GetButton("Countermeasures") && pilot.aircraft.radarAlt > 0.2f)
                {
                    anyButtonPressed = true;
                }

                if (anyButtonPressed)
                {
                    if (!_cmActive)
                    {
                        pilot.aircraft.Countermeasures(active: true, pilot.aircraft.countermeasureManager.activeIndex);
                        _cmActive = true;
                    }
                }
                else
                {
                    if (_cmActive)
                    {
                        pilot.aircraft.Countermeasures(active: false, pilot.aircraft.countermeasureManager.activeIndex);
                        _cmActive = false;
                    }
                }
            }
            else
            {
                byte mask = 0;
                bool anyCMIndexButton = false;
                for (int i = 0; i < CMActionNames.Length; i++)
                {
                    if (player.GetButton(CMActionNames[i]))
                    {
                        int idx = CountermeasureSearchUtil.FindStationIndex(
                            stations,
                            GetCMKeywords(i));
                        if (idx >= 0)
                        {
                            anyCMIndexButton = true;
                            mask |= (byte)(1 << idx);
                        }
                    }
                }

                bool defaultButton = player.GetButton("Countermeasures") && pilot.aircraft.radarAlt > 0.2f;
                bool anyButtonPressed = anyCMIndexButton || defaultButton;

                if (anyButtonPressed)
                {
                    if (anyCMIndexButton)
                    {
                        pilot.aircraft.countermeasureManager.activeIndex = mask;
                    }
                    // else: default button keeps the previously-set activeIndex

                    if (!_cmActive)
                    {
                        pilot.aircraft.Countermeasures(active: true, pilot.aircraft.countermeasureManager.activeIndex);
                        _cmActive = true;
                    }
                }
                else
                {
                    if (_cmActive)
                    {
                        pilot.aircraft.Countermeasures(active: false, pilot.aircraft.countermeasureManager.activeIndex);
                        _cmActive = false;
                    }
                }
            }
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
