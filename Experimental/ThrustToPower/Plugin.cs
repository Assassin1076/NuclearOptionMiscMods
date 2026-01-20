using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace ThrustToPower
{
    [BepInPlugin("Experimental.assassin1076.ThrustToPower", "ThrustToPower", "0.0.1")]
    public class ThrustToPowerMod : BaseUnityPlugin
    {
        public bool done = false;
        public static ThrustToPowerMod i;

        private void Awake()
        {
            i = this;
            var harmony = new Harmony("com.yourname.generatorhud");
            harmony.PatchAll();
        }
        void Update()
        {
            if (done) return;
            if (Encyclopedia.i == null)
            {
                Logger.LogInfo("Waiting for target prefabs to load...");
                return;
            }
            foreach (AircraftDefinition definition in Encyclopedia.i.aircraft)
            {
                if (definition.code == "FS-12" || definition.code == "KR-67")
                {
                    definition.unitPrefab.AddComponent<ThrustToPowerTransformer>();
                    GameObject ThrustToPowerIndicator = new GameObject("ThrustToPowerIndicator");
                    ThrustToPowerIndicator.transform.SetParent(definition.aircraftParameters.HUDExtras.transform,false);
                    ThrustToPowerIndicator.AddComponent<GeneratorIndicator>();
                    if(definition.code == "KR-67")
                    {
                        var power = definition.unitPrefab.GetComponent<PowerSupply>();
                        GameObject[] ori = PowerSupply_Awake_Postfix.powerSourcesField.GetValue(power) as GameObject[];
                        List<GameObject> list = ori.ToList();
                        list.Add(definition.unitPrefab);
                        PowerSupply_Awake_Postfix.powerSourcesField.SetValue(power, list.ToArray());
                    }
                }
                
            }
            done = true;
        }
    }

    public class ThrustToPowerTransformer : MonoBehaviour, IEngine
    {
        public event Action OnEngineDisable;
        public event Action OnEngineDamage;

        [Header("Force to Power Settings")]
        [Tooltip("最大可用于驱动发电机的推力")]
        public float maxForceUse = 80000f;

        public float CurrentForce;

        [Header("RPM Settings")]
        [Tooltip("发电机最大 RPM")]
        public float maxRPM = 12000f;

        [Tooltip("推力到 RPM 的响应曲线")]
        public AnimationCurve forceToRPMCurve =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // 内部缓存
        private float currentRPM;

        private float accumulatedForce;
        private int lastFrame = -1;
        public float GetThrust()
        {
            return 0f;
        }
        public float GetMaxThrust()
        {
            return 0f;
        }
        public float GetRPM()
        {
            return currentRPM;
        }

        public float GetRPMRatio()
        {
            if (maxRPM <= 0f)
                return 0f;

            return Mathf.Clamp01(currentRPM / maxRPM);
        }

        public void SetInteriorSounds(bool useInteriorSound)
        {
            // 发电机无声音
        }

        private void FixedUpdate()
        {
            UpdateRPM();
        }

        private void UpdateRPM()
        {
            CurrentForce = accumulatedForce;
            accumulatedForce = 0f;
            if (CurrentForce <= 0f)
            {
                currentRPM = Mathf.Lerp(currentRPM, 0f, Time.deltaTime * 5f);
                return;
            }

            float forceRatio = Mathf.Clamp01(CurrentForce / Mathf.Max(0.0001f, maxForceUse));
            float curveValue = forceToRPMCurve.Evaluate(forceRatio);
            float targetRPM = curveValue * maxRPM;

            currentRPM = Mathf.Lerp(currentRPM, targetRPM, Time.deltaTime * 8f);
        }

        public void AddForce(float force)
        {
            // 新的一帧，清零
            if (Time.frameCount != lastFrame)
            {
                accumulatedForce = 0f;
                lastFrame = Time.frameCount;
            }

            accumulatedForce += Mathf.Max(0f, force);
        }

        private void OnDisable()
        {
            currentRPM = 0f;
            OnEngineDisable?.Invoke();
        }
    }

    public class GeneratorIndicator : MonoBehaviour
    {
        private ThrustToPowerTransformer[] generators;
        private RectTransform bar;
        private Text rpmText;

        private Aircraft aircraft;

        private void Awake()
        {
            TryResolveAircraft();
            BuildUI();
        }

        private void OnEnable()
        {
            RefreshGenerators();
        }

        private void Update()
        {
            if (aircraft == null)
            {
                TryResolveAircraft();
                return;
            }

            if (generators == null || generators.Length == 0)
                RefreshGenerators();

            UpdateIndicator();
        }

        private void TryResolveAircraft()
        {
            if (SceneSingleton<CombatHUD>.i == null)
                return;

            aircraft = SceneSingleton<CombatHUD>.i.aircraft;
            if (aircraft != null)
            {
                RefreshGenerators();
            }
        }

        private void RefreshGenerators()
        {
            if (aircraft == null)
                return;

            generators = aircraft.GetComponentsInChildren<ThrustToPowerTransformer>(true);
        }

        private void BuildUI()
        {
            RectTransform root = gameObject.AddComponent<RectTransform>();
            root.sizeDelta = new Vector2(140, 48);
            root.anchorMin = new Vector2(1, 0);
            root.anchorMax = new Vector2(1, 0);
            root.anchoredPosition = new Vector2(400, 180);

            Image bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.45f);

            GameObject barObj = new GameObject("Bar");
            barObj.transform.SetParent(transform, false);

            Image barImg = barObj.AddComponent<Image>();
            barImg.color = Color.cyan;

            bar = barObj.GetComponent<RectTransform>();
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(0, 1);
            bar.sizeDelta = Vector2.zero;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(transform, false);

            rpmText = textObj.AddComponent<Text>();
            rpmText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            rpmText.alignment = TextAnchor.MiddleCenter;
            rpmText.color = Color.white;

            RectTransform textRT = rpmText.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
        }

        private void UpdateIndicator()
        {
            if (aircraft == null)
                return;

            if (generators == null || generators.Length == 0)
            {
                rpmText.text = "GEN OFF";
                bar.anchorMax = new Vector2(0f, 1f);
                return;
            }

            float maxRPM = 0f;
            float maxRatio = 0f;
            float totalForce = 0f;

            foreach (var gen in generators)
            {
                if (gen == null) continue;

                maxRPM = Mathf.Max(maxRPM, gen.GetRPM());
                maxRatio = Mathf.Max(maxRatio, gen.GetRPMRatio());
                totalForce += gen.CurrentForce;
            }

            float inputRatio = 0f;
            try
            {
                inputRatio = aircraft.GetInputs().customAxis1;
            }
            catch
            {
                inputRatio = 0f;
            }

            bar.anchorMax = new Vector2(Mathf.Clamp01(maxRatio), 1);

            rpmText.text =
                $"GEN {Mathf.RoundToInt(maxRPM)} RPM\n" +
                $"F {totalForce:0.0}\n" +
                $"R {inputRatio:0.00}";
        }
    }

    [HarmonyPatch(typeof(PowerSupply))]
    [HarmonyPatch("Awake")]
    public static class PowerSupply_Awake_Postfix
    {
        public static FieldInfo powerSourcesField =
                typeof(PowerSupply).GetField("powerSources", BindingFlags.NonPublic | BindingFlags.Instance);

        public static FieldInfo engineInterfacesField =
            typeof(PowerSupply).GetField("engineInterfaces", BindingFlags.NonPublic | BindingFlags.Instance);
        static void Postfix(PowerSupply __instance)
        {
            

            if (powerSourcesField == null || engineInterfacesField == null)
                return;

            GameObject[] powerSources =
                powerSourcesField.GetValue(__instance) as GameObject[];

            if (powerSources == null || powerSources.Length == 0)
                return;

            List<IEngine> engines = new List<IEngine>();

            foreach (GameObject source in powerSources)
            {
                if (source == null)
                    continue;

                IEngine[] foundEngines = source.GetComponents<IEngine>();

                if (foundEngines != null && foundEngines.Length > 0)
                {
                    engines.AddRange(foundEngines);
                }
            }

            if (engines.Count == powerSources.Length)
                return;

            engineInterfacesField.SetValue(__instance, engines.ToArray());
        }
    }
    [HarmonyPatch(typeof(JetNozzle))]
    [HarmonyPatch("GetTotalThrust")]
    public static class JetNozzle_GetTotalThrust_Patch
    {
        public static FieldInfo totalThrustField = typeof(JetNozzle).GetField("totalThrust", BindingFlags.NonPublic | BindingFlags.Instance);
        static void Postfix(JetNozzle __instance, ref float __result)
        {
            if (__instance == null)
                return;
            __result = (float)totalThrustField.GetValue(__instance);
            
        }
    }

    [HarmonyPatch(typeof(Turbojet))]
    [HarmonyPatch("GetThrust")]
    public static class TurboJet_GetThrust_Patch
    {
        public static FieldInfo ThrustField = typeof(Turbojet).GetField("thrust", BindingFlags.NonPublic | BindingFlags.Instance);
        static void Postfix(Turbojet __instance, ref float __result)
        {
            if (__instance == null)
                return;
            __result -= (float)ThrustField.GetValue(__instance);
            
        }
    }
    [HarmonyPatch(typeof(JetNozzle))]
    [HarmonyPatch("Thrust")]
    class JetNozzle_Thrust_Patch
    {
        public static FieldInfo aircraftField = typeof(JetNozzle)
                .GetField("aircraft", BindingFlags.NonPublic | BindingFlags.Instance);
        static bool Prefix(
            JetNozzle __instance,
            ref float thrustAmount,
            float rpmRatio,
            float thrustRatio,
            float throttle,
            bool allowAfterburner)
        {
            

            if (aircraftField == null)
                return true;

            Aircraft aircraft = aircraftField.GetValue(__instance) as Aircraft;
            if (aircraft == null)
                return true;

            ThrustToPowerTransformer transformer =
                aircraft.gameObject.GetComponent<ThrustToPowerTransformer>();

            if (transformer == null)
                return true;

            var controlInputs = aircraft.GetInputs();
            float customAxis1 = controlInputs.customAxis1;

            // 安全条件：customAxis1 必须大于 0，且推力必须大于 0
            if (customAxis1 <= 0f || thrustAmount <= 0f)
                return true;

            // 计算理论扣减量
            float rawDeduct = thrustAmount * customAxis1;

            // 最大允许扣减：当前推力的 50%
            float maxAllowedDeduct = thrustAmount * 0.5f;

            // 最终扣减量
            float finalDeduct = Mathf.Min(rawDeduct, maxAllowedDeduct);

            // 应用结果
            transformer.AddForce(finalDeduct);
            thrustAmount -= finalDeduct;

            return true;
        }
    }
}
