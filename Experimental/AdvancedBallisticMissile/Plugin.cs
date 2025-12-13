using BepInEx;
using HarmonyLib;
using Mirage.Logging;
using NuclearOption.SavedMission;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static Mirage.NetworkBehaviour;
using static UnityEngine.ParticleSystem.PlaybackState;

namespace MissileGuidancePatch
{
    [BepInPlugin("BallisticMissileMod", "BallisticMissileMod", "1.1.0")]
    public class MissilePatchPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony harmony = new Harmony("BallisticMissileMod");
            harmony.PatchAll();
            Logger.LogInfo("BallisticMissileMod 已加载");
        }
    }
    public class MissileExtraData : MonoBehaviour
    {
        public bool HasExtraMotor;
        public bool HasEnteredDive;
        public float SlowCheckTime;
        public GlobalPosition knownPos;
        public FieldInfo KnownPositionRef;
        public Missile MissileRef;
        public BallisticMissileGuidance GuidanceRef;
        public Unit TargetRef;
    }


    [HarmonyPatch(typeof(BallisticMissileGuidance))]
    [HarmonyPatch("Initialize")]
    public static class Patch_BallisticMissileGuidance_Initialize
    {
        public static void Postfix(BallisticMissileGuidance __instance)
        {
            var go = __instance.gameObject;
            var extra = go.GetComponent<MissileExtraData>();
            if (extra == null)
            {
                extra = go.AddComponent<MissileExtraData>();
                extra.GuidanceRef = __instance;
                var missileField_new = AccessTools.Field(__instance.GetType(), "missile");
                var targetField_new = AccessTools.Field(__instance.GetType(), "targetUnit");
                FieldInfo knownpos_ref = AccessTools.Field(typeof(BallisticMissileGuidance), "knownPos");
                var missile_ref = missileField_new?.GetValue(__instance) as Missile;
                var target_ref = targetField_new?.GetValue(__instance) as Unit;
                extra.MissileRef = missile_ref;
                extra.TargetRef = target_ref;
                extra.KnownPositionRef = knownpos_ref;
                Debug.Log($"[BallisticMissileMod] 已为导弹 {go.name} 添加扩展数据组件");
            }
            GlobalPosition newAim = Patch_BallisticMissileGuidance_Seek.GetRandomPoint(extra.knownPos, 10000, 15000);
            extra.KnownPositionRef.SetValue(__instance, newAim);
        }
    
    }
    [HarmonyPatch(typeof(BallisticMissileGuidance))]
    [HarmonyPatch("Seek")]
    public static class Patch_BallisticMissileGuidance_Seek
    {
        private const float triggerDistance = 60000f;
        private const float lastDistance = 10000f;
        private const float randGap = 10f;
        private const float randMin = 15000f;
        private const float randMax = 20000f;
        public static void Postfix(BallisticMissileGuidance __instance)
        {
            try
            {
                var go = __instance.gameObject;
                var extra = go.GetComponent<MissileExtraData>();
                if (extra == null)
                {
                    extra = go.AddComponent<MissileExtraData>();
                    extra.GuidanceRef = __instance;
                    var missileField_new = AccessTools.Field(__instance.GetType(), "missile");
                    var targetField_new = AccessTools.Field(__instance.GetType(), "targetUnit");
                    FieldInfo knownpos_ref = AccessTools.Field(typeof(BallisticMissileGuidance), "knownPos");
                    var missile_ref = missileField_new?.GetValue(__instance) as Missile;
                    var target_ref = targetField_new?.GetValue(__instance) as Unit;
                    extra.MissileRef = missile_ref;
                    extra.TargetRef = target_ref;
                    extra.KnownPositionRef = knownpos_ref;
                    Debug.Log($"[BallisticMissileMod] 已为导弹 {go.name} 添加扩展数据组件");
                }


                var missile = extra.MissileRef;
                var target = extra.TargetRef;
                if (missile == null || target == null) return;
                if (extra.HasExtraMotor){
                    if (missile.transform.position.y - target.transform.position.y > lastDistance)
                    {
                        if(Time.timeSinceLevelLoad - extra.SlowCheckTime > randGap)
                        {
                            GlobalPosition newAim = GetRandomPoint(extra.knownPos, randMin, randMax);
                            extra.MissileRef.SetAimpoint(newAim,extra.TargetRef.rb.velocity);
                            extra.SlowCheckTime = Time.timeSinceLevelLoad;
                        }
                    }
                    else
                    {
                        extra.MissileRef.SetAimpoint(extra.knownPos, extra.TargetRef.rb.velocity);
                    }

                    return;
                }
                if (!extra.HasEnteredDive)
                {
                    if (Vector3.Angle(missile.transform.forward, Vector3.down) < 20f)
                    {
                        extra.HasEnteredDive = true;
                    }
                    
                    return;
                }
                float distance = Vector3.Distance(
                    missile.transform.position,
                    target.transform.position
                );

                
                if (distance <= triggerDistance)
                {
                    extra.HasExtraMotor = true;
                    var missileType = missile.GetType();
                    var motorType = missileType.GetNestedType("Motor", System.Reflection.BindingFlags.NonPublic);
                    if (motorType == null)
                    {
                        return;
                    }
                    var motorsField = AccessTools.Field(missileType, "motors");
                    var currentMotors = motorsField?.GetValue(missile) as Array;

                    var firstMotor = currentMotors.GetValue(0);
                    var cloneMotor = CloneMotor(firstMotor, motorType);
                    if (cloneMotor == null)
                    {
                        return;
                    }
                    int oldLen = currentMotors.Length;
                    var newArray = Array.CreateInstance(motorType, oldLen + 1);
                    Array.Copy(currentMotors, newArray, oldLen);
                    newArray.SetValue(cloneMotor, oldLen);
                    motorsField.SetValue(missile, newArray);

                    extra.knownPos = (GlobalPosition)extra.KnownPositionRef.GetValue(__instance);
                    GlobalPosition newAim = GetRandomPoint(extra.knownPos, randMin, randMax);
                    extra.MissileRef.SetAimpoint(newAim,extra.TargetRef.rb.velocity);
                    extra.SlowCheckTime = Time.timeSinceLevelLoad;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MissilePatch] Seek Postfix 异常: {ex}");
            }
        }
        public static object CloneMotor(object sourceMotor, Type motorType)
        {
            if (sourceMotor == null)
                return null;
            var cloneMethod = AccessTools.Method(typeof(object), "MemberwiseClone");
            var clone = cloneMethod.Invoke(sourceMotor, null);

            FieldInfo fuelField = motorType.GetField("fuelMass", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo thrustField = motorType.GetField("thrust", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo burnTimeField = motorType.GetField("burnTime", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo ActField = motorType.GetField("activated", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fuelField == null)
            {
                Debug.LogError($"[MissilePatch] Field fuelField not found");
            }
            if (thrustField == null)
            {
                Debug.LogError($"[MissilePatch] Field thrustField not found");
            }
            if (burnTimeField == null)
            {
                Debug.LogError($"[MissilePatch] Field burnTimeField not found");
            }
            if (ActField == null)
            {
                Debug.LogError($"[MissilePatch] Field ActField not found");
            }
            object motor = clone;
            fuelField.SetValue(motor, 500f);
            burnTimeField.SetValue(motor, 40f);
            thrustField.SetValue(motor, 200000);
            ActField.SetValue(motor,false);
            return clone;
        }
        public static GlobalPosition GetRandomPoint(GlobalPosition Source,float minDist, float maxDist)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius = UnityEngine.Random.Range(minDist, maxDist);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            return new GlobalPosition(Source.AsVector3() + offset);
        }
    }

    [HarmonyPatch(typeof(Missile))]
    [HarmonyPatch("EngineOn")]
    public static class Patch_Missile_EngineOn
    {
        public static void Postfix(Missile __instance, ref bool __result)
        {
            try
            {
                var go = __instance.gameObject;
                var extra = go.GetComponent<MissileExtraData>();
                if(extra == null)
                {
                    return;
                }

                // 如果导弹曾被标记过，则强制引擎关闭
                if (extra.HasEnteredDive)
                {
                    __result = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MissilePatch] EngineOn Postfix 异常: {ex}");
            }
        }
    }
}
