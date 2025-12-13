using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MissileSpawner
{
    [BepInPlugin("Misc.Assassin1076.MissileSpawner", "MissileSpawner", "0.0.1")]
    public class MissileSpawner : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public static MissileSpawner Instance;
        private void Awake()
        {
            // Plugin startup logic
            var harmony = new Harmony("MissileSpawner");
            harmony.PatchAll();
            Logger = base.Logger;
            Logger.LogInfo($"Plugin Misc.Assassin1076.MissileSpawner is loaded!");
            MissileSpawner.Instance = this;
        }
    }
    [HarmonyPatch(typeof(PilotPlayerState))]
    [HarmonyPatch("FixedUpdateState")]
    [HarmonyPatch(new Type[] { typeof(Pilot) })]
    public static class PilotPlayerState_FixedUpdateState_Patch
    {
        public static void Prefix(Pilot pilot)
        {
            if (pilot.aircraft != null && pilot.aircraft.cockpit.GetComponent<MountHelper>() == null)
            {
                var ret = pilot.aircraft.cockpit.gameObject.AddComponent<MountHelper>();
                Debug.Log($"Added MountSelector for pilot {pilot.name} : {pilot.aircraft.name}");
            }
        }
    }

    [HarmonyPatch(typeof(WeaponStation))]
    [HarmonyPatch("LaunchMount")]
    public static class Patch_WeaponStation_LaunchMount
    {
        public static void Prefix(WeaponStation __instance, Unit owner, Unit target, GlobalPosition aimpoint)
        {
            Aircraft aircraft = owner as Aircraft;
            if (aircraft == null) return;
            var marker = aircraft.cockpit.gameObject.GetComponent<MountHelper>();
            if (marker == null) return;
            if (__instance.WeaponInfo.troops || __instance.WeaponInfo.sling || __instance.Cargo) return;
            Type mountType = __instance.Weapons[0].GetType();
            FieldInfo fired_field = mountType.GetField("fired", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fired_field != null)
            {
                for (int i = 0; i < __instance.Weapons.Count; i++)
                {
                    bool var = (bool)fired_field.GetValue(__instance.Weapons[i]);
                    if (!var)
                    {
                        Type WSType = __instance.GetType();
                        FieldInfo Index_field = WSType.GetField("weaponIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                        Index_field.SetValue(__instance, i);
                        break;
                    }
                }
            }
                
        }
    }


    [HarmonyPatch(typeof(MountedMissile))]
    public static class Patch_MountedMissile_Fire
    {
        [HarmonyPostfix]
        [HarmonyPatch("Fire")]
        public static void PostfixFire(
            MountedMissile __instance,
            Unit owner,
            Unit target,
            Vector3 inheritedVelocity,
            WeaponStation weaponStation,
            GlobalPosition aimpoint)
        {

            Aircraft aircraft = owner as Aircraft;
            if(aircraft == null) return;
            var marker = aircraft.cockpit.gameObject.GetComponent<MountHelper>();
            if (marker == null) return;
            MissileSpawner.Instance.StartCoroutine(DelayedLogic(__instance,owner, target, marker,weaponStation));
        }
        private static IEnumerator DelayedLogic(MountedMissile __instance, Unit owner, Unit target, MountHelper host, WeaponStation weaponStation)
        {
            yield return new WaitForSeconds(4.0f);
            Type mountType = __instance.GetType();
            __instance.Rearm();
            weaponStation.AccountAmmo();
        }
    }

    [HarmonyPatch(typeof(Application), nameof(Application.version), MethodType.Getter)]
    static class VersionGetterPatch
    {
        static void Postfix(ref string __result)
        {
            __result += "_"+ "Misc.Assassin1076.MissileSpawner" + "-v" + "0.0.1";
        }
    }

    public class MountHelper : MonoBehaviour
    {
        public Dictionary<MountedMissile,float > mountedMissiles;
    }
}
