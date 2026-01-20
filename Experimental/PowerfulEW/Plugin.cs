using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using NuclearOption.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Configuration;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerfulEW
{
    [BepInPlugin("fun.assassin1076.powerfulEW", "PowerfulEW", "0.0.1")]
    public class EWPlugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        private bool done = false;
        private void Awake()
        {
            // Plugin startup logic
            Harmony harmony = new Harmony("fun.assassin1076.powerfulEW");
            harmony.PatchAll();
            Logger = base.Logger;
            Logger.LogInfo($"Plugin fun.assassin1076.powerfulEW is loaded!");
        }

        void Update()
        {
            if (done) return;
            if (Encyclopedia.i == null)
            {
                Logger.LogInfo("Waiting for target prefabs to load...");
                return;
            }
            foreach (AircraftDefinition aircraft in Encyclopedia.i.aircraft)
            {
                if (aircraft.code == "FS-12")
                {
                    var newcomp = aircraft.unitPrefab.AddComponent<ActiveMirage>();
                    var newcomp2 = aircraft.unitPrefab.AddComponent<ActiveScoutEnhancement>();
                    aircraft.unitPrefab.AddComponent<ActiveMirageSwitch>();
                    newcomp.aircraft = aircraft.unitPrefab.GetComponent<Aircraft>();
                    newcomp2.aircraft = newcomp.aircraft;
                    newcomp.displayName = "Active Mirage";
                    newcomp2.displayName = "Radar Enhancement";
                    newcomp.chargeable = true;
                    newcomp2.chargeable = true;
                    var ECM = aircraft.unitPrefab.GetComponent<RadarJammer>();

                    newcomp.displayImage = ECM.displayImage;
                    newcomp2.displayImage = ECM.displayImage;
                    newcomp.dischargeSound = typeof(RadarJammer).GetField("dischargeSound", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(ECM) as AudioClip;
                    newcomp2.dischargeSound = newcomp.dischargeSound;
                }
            }
            done = true;
        }

    }

    public static class CountermeasureUtils
    {
        public static int FindStationIndexByDisplayName(
            object countermeasureManager,
            string targetName)
        {
            if (countermeasureManager == null || string.IsNullOrEmpty(targetName))
                return -1;

            Type managerType = countermeasureManager.GetType();

            // 取 private List<CountermeasureStation> countermeasureStations
            FieldInfo stationsField = managerType.GetField(
                "countermeasureStations",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (stationsField == null)
                return -1;

            var list = stationsField.GetValue(countermeasureManager) as IList;
            if (list == null)
                return -1;

            for (int i = 0; i < list.Count; i++)
            {
                object station = list[i];
                if (station == null)
                    continue;

                // CountermeasureStation.displayName
                FieldInfo nameField = station.GetType().GetField(
                    "displayName",
                    BindingFlags.Instance | BindingFlags.Public);

                if (nameField == null)
                    continue;

                string name = nameField.GetValue(station) as string;
                if (name == targetName)
                    return i;
            }

            return -1;
        }
    }

    public static class CountermeasureToggle
    {
        public static bool enable = false;
        public static bool IsEnabled()
        {
            return enable;
        }
    }

    


    public class ActiveMirageSwitch: MonoBehaviour
    {
        public ActiveMirage mirage;
        public Aircraft aircraft;
        public bool isOn = false;
        private int knownIndex = -1;
        public void Awake()
        {
            aircraft = this.gameObject.GetComponent<Aircraft>();
            mirage = this.gameObject.GetComponent<ActiveMirage>();
            if (aircraft == null || mirage == null) { Destroy(this); }
        }
        public void OnDestroy()
        {
            isOn = false ;
            CountermeasureToggle.enable = false;
        }
        public void Switch()
        {
            isOn = !isOn;
            CountermeasureToggle.enable = isOn;
            if (isOn)
            {
                if (knownIndex == -1)
                {
                    knownIndex = CountermeasureUtils.FindStationIndexByDisplayName(
                        aircraft.countermeasureManager,
                        "Active Mirage");
                }
                byte index = (byte)knownIndex;
                aircraft.Countermeasures(true, index);
            }
        }
    }

    public class ActiveMirage: Countermeasure
    {

        // Fields
        [SerializeField]
        public float powerUsage = 20f;
        [SerializeField]
        public float capacitance = 100f;
        [SerializeField]
        public float mirageIntensity = -1f;
        [SerializeField]
        public float dischargeVolume = 1f;
        [SerializeField]
        public AudioClip dischargeSound;
        [SerializeField, Range(0f, 1f)]
        public float volumeMultiplier;

        

        private float lastActivated;
        private float mirageIntensityPrev;
        private float mirageIntensityCurrent;
        private AudioSource dischargeSource;
        private PowerSupply powerSupply;

        public override void AttachToUnit(Aircraft aircraft)
        {
            this.powerSupply = aircraft.GetPowerSupply();
            base.AttachToUnit(aircraft);
            this.powerSupply.AddUser();
        }
        protected override void Awake()
        {
            base.ammo = 1;
            if (base.aircraft != null)
            {
                this.powerSupply = base.aircraft.GetPowerSupply();
                this.powerSupply.AddUser();
                this.powerSupply.ModifyCapacitance(this.capacitance);
            }
            base.Awake();
        }

        public override void Fire()
        {
            base.enabled = true;
            this.lastActivated = Time.timeSinceLevelLoad;
            float num = this.powerSupply.DrawPower(this.powerUsage);
            this.mirageIntensityCurrent = (this.mirageIntensity * num) / this.powerUsage;
            base.aircraft.ModifyRCS(this.mirageIntensityCurrent - this.mirageIntensityPrev);
            this.mirageIntensityPrev = this.mirageIntensityCurrent;
            if (this.dischargeSource == null)
            {
                this.dischargeSource = base.gameObject.AddComponent<AudioSource>();
                this.dischargeSource.outputAudioMixerGroup = SoundManager.i.InterfaceMixer;
                this.dischargeSource.clip = this.dischargeSound;
                this.dischargeSource.spatialBlend = 1f;
                this.dischargeSource.dopplerLevel = 0f;
                this.dischargeSource.spread = 5f;
                this.dischargeSource.maxDistance = 40f;
                this.dischargeSource.minDistance = 5f;
            }
            this.dischargeSource.pitch = num / this.powerUsage;
            this.dischargeSource.volume = (num / this.powerUsage) * this.volumeMultiplier;
            if (!this.dischargeSource.isPlaying)
            {
                this.dischargeSource.Play();
            }
        }
        private void Update()
        {
            if (((Time.timeSinceLevelLoad - this.lastActivated) > 0.1f) && (base.aircraft != null))
            {
                base.aircraft.ModifyRCS(-this.mirageIntensityPrev);
                this.mirageIntensityPrev = 0f;
                base.enabled = false;
                if ((SceneSingleton<CombatHUD>.i != null) && (base.aircraft == SceneSingleton<CombatHUD>.i.aircraft))
                {
                    this.UpdateHUD();
                }
            }
        }
        public override void UpdateHUD()
        {
            SceneSingleton<CombatHUD>.i.DisplayCountermeasures(base.displayName, base.displayImage, (Time.timeSinceLevelLoad - this.lastActivated) < 0.05f);
            if ((this.dischargeSource != null) && (this.dischargeSource.isPlaying && ((Time.timeSinceLevelLoad - this.lastActivated) > 0.05f)))
            {
                this.dischargeSource.Stop();
            }
        }

    }

    public class ActiveScoutEnhancement : Countermeasure
    {

        // Fields
        [SerializeField]
        public float powerUsage = 100f;
        [SerializeField]
        public float capacitance = 100f;
        [SerializeField]
        public float RadarBoostIntensity = 10f;
        [SerializeField]
        public float dischargeVolume = 1f;
        [SerializeField]
        public AudioClip dischargeSound;
        [SerializeField, Range(0f, 1f)]
        public float volumeMultiplier;

        public static FieldInfo RadarLocator_onlySurface = typeof(RadarLocator).GetField("onlySurface", BindingFlags.Instance | BindingFlags.NonPublic);
        public static FieldInfo Radar_cone = typeof(Radar).GetField("radarCone", BindingFlags.Instance | BindingFlags.NonPublic);
        private float lastActivated;
        private float RadarBoostIntensityPrev;
        private float RadarBoostIntensityCurrent;
        private AudioSource dischargeSource;
        private PowerSupply powerSupply;
        private RadarLocator RadarLocator;
        public override void AttachToUnit(Aircraft aircraft)
        {
            this.powerSupply = aircraft.GetPowerSupply();
            base.AttachToUnit(aircraft);
            this.powerSupply.AddUser();
            RadarLocator = aircraft.gameObject.GetComponentInChildren<RadarLocator>();
        }
        protected override void Awake()
        {
            base.ammo = 1;
            if (base.aircraft != null)
            {
                this.powerSupply = base.aircraft.GetPowerSupply();
                this.powerSupply.AddUser();
                this.powerSupply.ModifyCapacitance(this.capacitance);
                RadarLocator = aircraft.gameObject.GetComponentInChildren<RadarLocator>();
            }
            base.Awake();
        }

        public override void Fire()
        {
            base.enabled = true;
            this.lastActivated = Time.timeSinceLevelLoad;
            float num = this.powerSupply.DrawPower(this.powerUsage);
            this.RadarBoostIntensityCurrent = (this.RadarBoostIntensity * num) / this.powerUsage;
            ModifyRadar( aircraft.radar as Radar ,this.RadarBoostIntensityCurrent - this.RadarBoostIntensityPrev);
            if(RadarLocator != null) RadarLocator_onlySurface.SetValue(RadarLocator, false);
            this.RadarBoostIntensityPrev = this.RadarBoostIntensityCurrent;
            if (this.dischargeSource == null)
            {
                this.dischargeSource = base.gameObject.AddComponent<AudioSource>();
                this.dischargeSource.outputAudioMixerGroup = SoundManager.i.InterfaceMixer;
                this.dischargeSource.clip = this.dischargeSound;
                this.dischargeSource.spatialBlend = 1f;
                this.dischargeSource.dopplerLevel = 0f;
                this.dischargeSource.spread = 5f;
                this.dischargeSource.maxDistance = 40f;
                this.dischargeSource.minDistance = 5f;
            }
            this.dischargeSource.pitch = num / this.powerUsage;
            this.dischargeSource.volume = (num / this.powerUsage) * this.volumeMultiplier;
            if (!this.dischargeSource.isPlaying)
            {
                this.dischargeSource.Play();
            }
        }
        private void Update()
        {
            if (((Time.timeSinceLevelLoad - this.lastActivated) > 0.1f) && (base.aircraft != null))
            {
                ModifyRadar(aircraft.radar as Radar, -this.RadarBoostIntensityPrev);
                this.RadarBoostIntensityPrev = 0f;
                if (RadarLocator != null) RadarLocator_onlySurface.SetValue(RadarLocator, true);
                base.enabled = false;
                if ((SceneSingleton<CombatHUD>.i != null) && (base.aircraft == SceneSingleton<CombatHUD>.i.aircraft))
                {
                    this.UpdateHUD();
                }
            }
        }
        public override void UpdateHUD()
        {
            SceneSingleton<CombatHUD>.i.DisplayCountermeasures(base.displayName, base.displayImage, (Time.timeSinceLevelLoad - this.lastActivated) < 0.05f);
            if ((this.dischargeSource != null) && (this.dischargeSource.isPlaying && ((Time.timeSinceLevelLoad - this.lastActivated) > 0.05f)))
            {
                this.dischargeSource.Stop();
            }
        }
        public static void ModifyRadar(Radar radar, float boost)
        {
            if (radar != null)
            {
                float distanceBuff = boost * 5000f;
                float clutterBuff = -boost * 0.01f;
                float dopplerBuff = boost * 0.01f;
                float minSingalBuff = -boost * 0.05f;
                float coneBuff = boost * 20f;

                Radar_cone.SetValue(radar, (float)Radar_cone.GetValue(radar) + coneBuff);
                radar.RadarParameters.dopplerFactor += dopplerBuff;
                radar.RadarParameters.clutterFactor += clutterBuff;
                radar.RadarParameters.minSignal += minSingalBuff;
                radar.RadarParameters.maxRange += distanceBuff;
            }
        }

    }

    public static class JammingPod_Reflection
    {
        private static Type type;

        public static FieldInfo currentTargetField;
        public static FieldInfo directionTransformField;
        public static FieldInfo powerSupplyField;
        public static FieldInfo powerField;
        public static FieldInfo lastJammingTickField;
        public static FieldInfo rangeFalloffField;
        public static FieldInfo rewardCountField;
        public static FieldInfo rewardAmountField;
        public static FieldInfo rewardThresholdField;

        static JammingPod_Reflection()
        {
            type = typeof(JammingPod);

            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            currentTargetField = type.GetField("currentTarget", flags);
            directionTransformField = type.GetField("directionTransform", flags);
            powerSupplyField = type.GetField("powerSupply", flags);
            powerField = type.GetField("power", flags);
            lastJammingTickField = type.GetField("lastJammingTick", flags);
            rangeFalloffField = type.GetField("rangeFalloff", flags);
            rewardCountField = type.GetField("rewardCount", flags);
            rewardAmountField = type.GetField("rewardAmount", flags);
            rewardThresholdField = type.GetField("rewardThreshold", flags);
        }
        public static T GetField<T>(object instance, FieldInfo field)
        {
            return (T)field.GetValue(instance);
        }

        public static void SetField<T>(object instance, FieldInfo field, T value)
        {
            field.SetValue(instance, value);
        }
    }


    public class ConeJammingExtras : MonoBehaviour
    {
        public List<Unit> cachedTargets = new List<Unit>();
        public float lastUpdateTime = 0f;

        // 参数
        public float updateInterval = 0.5f;
        public float coneAngle = 10f;
        public float maxDistance = 50000f;
        public float powerUseMul = 2.0f;
        private JammingPod pod;

        private void Awake()
        {
            pod = GetComponent<JammingPod>();
            if (pod == null)
            {
                Debug.LogError("ConeJammingExtras 必须挂在 JammingPod 所在 GameObject");
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (!pod.enabled) return;
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                lastUpdateTime = Time.time;
                _ = RefreshPotentialTargetsAsync();
            }

            ApplyConeJamming();
        }

        private async Task RefreshPotentialTargetsAsync()
        {
            if (pod == null) return;

            Vector3 origin = JammingPod_Reflection.GetField<Transform>(pod, JammingPod_Reflection.directionTransformField).position;

            Collider[] hits = Physics.OverlapSphere(origin, maxDistance, LayerMask.GetMask("Default") | LayerMask.GetMask("Ships"));

            List<Unit> units = new List<Unit>();

            await Task.Run(() =>
            {
                foreach (var col in hits)
                {
                    
                }
            });

            foreach (var col in hits)
            {
                Unit unit = col.GetComponentInParent<Unit>();
                if (unit == null || unit.NetworkHQ == pod.attachedUnit.NetworkHQ) continue;
                units.Add(unit);
            }

            cachedTargets = units;
        }

        private void ApplyConeJamming()
        {
            if (pod == null) return;

            // 获取基础数据
            Unit currentTarget = JammingPod_Reflection.GetField<Unit>(pod, JammingPod_Reflection.currentTargetField);
            if (currentTarget == null) return;

            Transform directionTransform = JammingPod_Reflection.GetField<Transform>(pod, JammingPod_Reflection.directionTransformField);
            PowerSupply powerSupply = JammingPod_Reflection.GetField<PowerSupply>(pod, JammingPod_Reflection.powerSupplyField);
            float power = JammingPod_Reflection.GetField<float>(pod, JammingPod_Reflection.powerField);
            float lastJammingTick = JammingPod_Reflection.GetField<float>(pod, JammingPod_Reflection.lastJammingTickField);
            AnimationCurve rangeFalloff = JammingPod_Reflection.GetField<AnimationCurve>(pod, JammingPod_Reflection.rangeFalloffField);
            float rewardCount = JammingPod_Reflection.GetField<float>(pod, JammingPod_Reflection.rewardCountField);
            float rewardAmount = JammingPod_Reflection.GetField<float>(pod, JammingPod_Reflection.rewardAmountField);
            float rewardThreshold = JammingPod_Reflection.GetField<float>(pod, JammingPod_Reflection.rewardThresholdField);

            power = power * this.powerUseMul;
            float powerRatio = powerSupply.DrawPower(power) / power;
            // 0.2s 节奏限制
            if (Time.time - lastJammingTick < 0.2f) return;
            JammingPod_Reflection.SetField(pod, JammingPod_Reflection.lastJammingTickField, Time.time);

            Vector3 origin = directionTransform.position;
            Vector3 forward = (currentTarget.transform.position - origin).normalized;

            

            foreach (var unit in cachedTargets)
            {
                if (unit.NetworkHQ == pod.attachedUnit.NetworkHQ) continue;
                if (!unit.HasRadarEmission()) continue;

                Vector3 toUnit = (unit.transform.position - origin);
                float distance = toUnit.magnitude;
                Vector3 dirToUnit = toUnit.normalized;

                if (Vector3.Angle(forward, dirToUnit) > coneAngle) continue;
                if (Physics.Linecast(origin, unit.transform.position, 0x40)) continue;

                float jamAmount = rangeFalloff.Evaluate(distance) * powerRatio;

                Unit.JamEventArgs args = new Unit.JamEventArgs
                {
                    jamAmount = jamAmount,
                    jammingUnit = pod.attachedUnit
                };
                unit.Jam(args);
                Aircraft attachedUnit = pod.attachedUnit as Aircraft;
                if (attachedUnit != null && unit.HasRadarEmission() && attachedUnit.Player != null &&
                    unit.NetworkHQ != attachedUnit.NetworkHQ)
                {
                    rewardCount += jamAmount * 0.2f;
                    rewardAmount += (0.0001f * jamAmount) * Mathf.Sqrt(unit.definition.value);

                    if (rewardCount > rewardThreshold)
                    {
                        attachedUnit.NetworkHQ.ReportJammingAction(attachedUnit.Player, unit, rewardAmount);
                        rewardAmount = 0f;
                        rewardCount = 0f;
                    }
                }
            }

            // 写回私有字段
            JammingPod_Reflection.SetField(pod, JammingPod_Reflection.rewardCountField, rewardCount);
            JammingPod_Reflection.SetField(pod, JammingPod_Reflection.rewardAmountField, rewardAmount);
        }
    }

    [HarmonyPatch(typeof(RadarParams), "GetSignalStrength")]
    public static class RadarParamsPatch
    {
        static bool Prefix(ref float RCS)
        {
            if (RCS < 0f)
            {
                RCS = 0f;
            }

            return true;
        }
    }


    [HarmonyPatch(typeof(JammingPod), "FixedUpdate")]
    public static class JammingPodPatch
    {
        static bool Prefix(JammingPod __instance)
        {
            // 确保挂载了 ConeJammingExtras
            if (__instance.gameObject.GetComponent<ConeJammingExtras>() == null)
            {
                __instance.gameObject.AddComponent<ConeJammingExtras>();
            }

            return false;
        }
    }
    [HarmonyPatch(typeof(PilotPlayerState), "PlayerControls")]
    public static class Patch_PlayerControls_Countermeasure_OR
    {
        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);

            MethodInfo getButton =
                AccessTools.Method(typeof(Rewired.Player), "GetButton", new[] { typeof(string) });

            MethodInfo thirdPartyEnabled =
                AccessTools.Method(typeof(CountermeasureToggle), nameof(CountermeasureToggle.IsEnabled));

            FieldInfo radarAltField =
                AccessTools.Field(typeof(Unit), "radarAlt");

            for (int i = 1; i < code.Count - 5; i++)
            {
                // ldstr "Countermeasures"
                if (code[i - 1].opcode == OpCodes.Ldstr &&
                    code[i - 1].operand is string s &&
                    s == "Countermeasures" &&

                    // callvirt GetButton(string)
                    code[i].opcode == OpCodes.Callvirt &&
                    code[i].operand is MethodInfo mi &&
                    mi == getButton &&

                    // brfalse / brfalse.s
                    IsBrfalse(code[i + 1]) &&

                    // 后面确实是 radarAlt > 0.2f
                    ContainsRadarAltCompare(code, i + 1, radarAltField))
                {
                    // 插入 OR gate
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, thirdPartyEnabled));
                    code.Insert(i + 2, new CodeInstruction(OpCodes.Or));

                    i += 2; // 跳过插入部分
                }
            }

            return code;
        }

        static bool IsBrfalse(CodeInstruction ci)
        {
            return ci.opcode == OpCodes.Brfalse ||
                   ci.opcode == OpCodes.Brfalse_S;
        }

        /// <summary>
        /// 从 brfalse 之后向前扫描有限步数，确认存在 radarAlt > 0.2f 判断
        /// </summary>
        static bool ContainsRadarAltCompare(
            List<CodeInstruction> code,
            int startIndex,
            FieldInfo radarAltField)
        {
            const int scanRange = 10;

            for (int i = startIndex; i < Math.Min(code.Count - 1, startIndex + scanRange); i++)
            {
                if (code[i].opcode == OpCodes.Ldfld &&
                    code[i].operand is FieldInfo fi &&
                    fi == radarAltField)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(CountermeasureManager), "NextCountermeasure")]
    public static class CountermeasureManagerPatch
    {
        static FieldInfo aircraftField = typeof(CountermeasureManager).GetField("aircraft", BindingFlags.Instance | BindingFlags.NonPublic);
        static bool Prefix(CountermeasureManager __instance)
        {
            Aircraft aircraft = aircraftField.GetValue(__instance) as Aircraft;
            if (aircraft != null)
            {
                var test = aircraft.gameObject.GetComponent<ActiveMirageSwitch>();
                if (test != null && test.isOn)
                {
                    test.Switch();
                }
            }
            return true;
        }
    }
}
