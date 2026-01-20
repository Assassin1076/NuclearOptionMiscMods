using BepInEx;
using BepInEx.Logging;
using System.Reflection;
using UnityEngine;

namespace RealA10
{
    [BepInPlugin("fun.assassin1076.realA10", "RealA10", "0.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public bool done;
        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin RealA10 is loaded!");
        }
        void Update()
        {
            if (done) return;
            if (Encyclopedia.i == null)
            {
                Logger.LogInfo("Waiting for target prefabs to load...");
                return;
            }
            AircraftDefinition reference = Encyclopedia.i.aircraft.Find(def => def.code == "T/A-30");
            AircraftDefinition target = Encyclopedia.i.aircraft.Find(def => def.code == "A-19");

            PlanePrefabModifier.ModifyPlanePrefab(target.unitPrefab);
            if (JetEngineTemplateHelper.TryGetJetEngineTemplates(
                reference.unitPrefab,
                out Turbojet turbojetTemplate,
                out JetNozzle nozzleTemplate))
            {
                JetEngineAssembler.AssembleJetEngines(
                    target.unitPrefab,
                    turbojetTemplate,
                    nozzleTemplate
                );
            }

            done = true;
        }
    }



    public static class JetEngineTemplateHelper
    {
        public static bool TryGetJetEngineTemplates(
            GameObject root,
            out Turbojet turbojetTemplate,
            out JetNozzle nozzleTemplate
        )
        {
            turbojetTemplate = null;
            nozzleTemplate = null;

            if (root == null)
            {
                Debug.LogWarning("[JetEngineTemplateHelper] Root is null.");
                return false;
            }

            // ===== engine_R / Turbojet =====
            Transform engineTransform = root.transform.Find("engine_R");
            if (engineTransform == null)
            {
                Debug.LogWarning("[JetEngineTemplateHelper] engine_R not found.");
                return false;
            }

            turbojetTemplate = engineTransform.GetComponent<Turbojet>();
            if (turbojetTemplate == null)
            {
                Debug.LogWarning("[JetEngineTemplateHelper] Turbojet not found on engine_R.");
                return false;
            }

            // ===== nozzle_R / JetNozzle =====
            Transform nozzleTransform = engineTransform.Find("nozzle_R");
            if (nozzleTransform == null)
            {
                Debug.LogWarning("[JetEngineTemplateHelper] nozzle_R not found under engine_R.");
                return false;
            }

            nozzleTemplate = nozzleTransform.GetComponent<JetNozzle>();
            if (nozzleTemplate == null)
            {
                Debug.LogWarning("[JetEngineTemplateHelper] JetNozzle not found on nozzle_R.");
                return false;
            }

            return true;
        }
    }

    public static class PlanePrefabModifier
    {
        public static void ModifyPlanePrefab(GameObject root)
        {
            if (root == null)
            {
                Debug.LogWarning("[PlanePrefabModifier] Root GameObject is null.");
                return;
            }

            // 1. 禁用最末端的 prop 节点
            string[] propPaths =
            {
            // Left wing
            "wing1_L/engineMount_L/engine_L/hub_L/hub1_L/prop1_L",
            "wing1_L/engineMount_L/engine_L/hub_L/hub2_L/prop2_L",

            // Right wing
            "wing1_R/engineMount_R/engine_R/hub_R/hub1_R/prop1_R",
            "wing1_R/engineMount_R/engine_R/hub_R/hub2_R/prop2_R"
        };

            foreach (var path in propPaths)
            {
                Transform prop = root.transform.Find(path);
                if (prop != null)
                {
                    prop.gameObject.SetActive(false);
                    Debug.Log($"[PlanePrefabModifier] Disabled prop: {path}");
                }
                else
                {
                    Debug.LogWarning($"[PlanePrefabModifier] Prop path not found: {path}");
                }
            }

            // 2. 移除 engine_L / engine_R 上的 TurbineEngine 组件
            string[] enginePaths =
            {
            "wing1_L/engineMount_L/engine_L",
            "wing1_R/engineMount_R/engine_R"
        };

            foreach (var path in enginePaths)
            {
                Transform engine = root.transform.Find(path);
                if (engine == null)
                {
                    Debug.LogWarning($"[PlanePrefabModifier] Engine path not found: {path}");
                    continue;
                }

                var turbine = engine.GetComponent<TurbineEngine>();
                if (turbine != null)
                {
                    Object.Destroy(turbine);
                    Debug.Log($"[PlanePrefabModifier] Removed TurbineEngine from: {path}");
                }
                else
                {
                    Debug.Log($"[PlanePrefabModifier] TurbineEngine not found on: {path}");
                }
            }
        }
    }


    public static class JetEngineAssembler
    {
        public static void AssembleJetEngines(
            GameObject root,
            Turbojet turbojetTemplate,
            JetNozzle nozzleTemplate
        )
        {
            if (root == null)
            {
                Debug.LogWarning("[JetEngineAssembler] Root is null.");
                return;
            }

            // 左引擎
            AssembleSingleEngine(
                root,
                "wing1_L/engineMount_L/engine_L",   // Turbojet挂载
                "wing1_L/engineMount_L/engine_L/hub_L", // JetNozzle挂载点 hub_L
                turbojetTemplate,
                nozzleTemplate
            );

            // 右引擎
            AssembleSingleEngine(
                root,
                "wing1_R/engineMount_R/engine_R",
                "wing1_R/engineMount_R/engine_R/hub_R", // JetNozzle挂载点 hub_R
                turbojetTemplate,
                nozzleTemplate
            );
        }

        private static void AssembleSingleEngine(
            GameObject root,
            string enginePath,
            string nozzlePath,
            Turbojet turbojetTemplate,
            JetNozzle nozzleTemplate
        )
        {
            // ===== Turbojet挂载 =====
            Transform engineTransform = root.transform.Find(enginePath);
            if (engineTransform == null)
            {
                Debug.LogWarning($"[JetEngineAssembler] Engine path not found: {enginePath}");
                return;
            }

            Turbojet turbojet = engineTransform.gameObject.AddComponent<Turbojet>();
            TurbojetUtils.InitializeFromTemplate(turbojetTemplate, turbojet);

            // ===== JetNozzle挂载 =====
            Transform nozzleTransform = root.transform.Find(nozzlePath);
            if (nozzleTransform == null)
            {
                Debug.LogWarning($"[JetEngineAssembler] Nozzle path not found: {nozzlePath}");
                return;
            }

            JetNozzle nozzle = nozzleTransform.gameObject.AddComponent<JetNozzle>();
            JetNozzleUtils.InitializeFromTemplate(nozzleTemplate, nozzle);

            // ===== 互相引用注入 =====
            turbojet.nozzles = new JetNozzle[] { nozzle };
            nozzle.turbojet = turbojet;

            Debug.Log($"[JetEngineAssembler] Jet engine assembled at {engineTransform.name} -> {nozzleTransform.name}");
        }
    }



    public static class JetNozzleUtils
    {
        public static void InitializeFromTemplate(JetNozzle template, JetNozzle target)
        {
            if (template == null || target == null)
            {
                Debug.LogWarning("[JetNozzleUtils] Template or target is null.");
                return;
            }

            // ========= Part binding =========
            // 必须绑定到目标 GameObject 上的 AeroPart
            target.part = target.GetComponent<AeroPart>();
            if (target.part == null)
            {
                Debug.LogWarning(
                    $"[JetNozzleUtils] AeroPart not found on {target.gameObject.name}"
                );
            }

            // ========= Engine reference =========
            // 由顶层装配逻辑在完成 Turbojet / Nozzle 创建后填充
            target.turbojet = null;

            // ========= Visual / effects =========
            target.failureEffect = template.failureEffect;
            target.heatHaze = template.heatHaze;
            target.glow = template.glow;

            // ========= Thrust / motion parameters =========
            target.thrustProportion = template.thrustProportion;
            target.swivelSpeed = template.swivelSpeed;
            target.pitchThrust = template.pitchThrust;
            target.rollThrust = template.rollThrust;
            target.yawSwivel = template.yawSwivel;
            target.angleMax = template.angleMax;
            target.angleMin = template.angleMin;

            // ========= IR / volume =========
            target.thrustMaxVolume = template.thrustMaxVolume;
            target.IRMin = template.IRMin;
            target.IRMax = template.IRMax;
            target.volumeMultiplier = template.volumeMultiplier;

            // ========= Swivel audio tuning =========
            target.swivelPitchBase = template.swivelPitchBase;
            target.swivelPitchFactor = template.swivelPitchFactor;

            // ========= Reference re-binding =========

            // 后续由装配步骤填充
            target.afterburners = null;
            target.vectorTransforms = null;

            // 不创建 swivelAudio
            target.swivelAudio = null;

            // 喷口推力 Transform 绑定自身
            target.thrustTransform = target.transform;

            // ========= 创建并复制 thrustAudio =========
            target.thrustAudio = CreateAudioSourceFromTemplate(
                template.thrustAudio,
                target.transform
            );
        }

        private static AudioSource CreateAudioSourceFromTemplate(
            AudioSource template,
            Transform parent)
        {
            if (template == null || parent == null)
                return null;

            AudioSource audio = parent.gameObject.AddComponent<AudioSource>();

            // ---- AudioClip & mixer ----
            audio.clip = template.clip;
            audio.outputAudioMixerGroup = template.outputAudioMixerGroup;

            // ---- Playback ----
            audio.playOnAwake = template.playOnAwake;
            audio.loop = template.loop;
            audio.priority = template.priority;

            // ---- Volume / pitch ----
            audio.volume = template.volume;
            audio.pitch = template.pitch;

            // ---- 3D sound ----
            audio.spatialBlend = template.spatialBlend;
            audio.dopplerLevel = template.dopplerLevel;
            audio.spread = template.spread;
            audio.rolloffMode = template.rolloffMode;
            audio.minDistance = template.minDistance;
            audio.maxDistance = template.maxDistance;

            // ---- Curves ----
            audio.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                template.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            audio.SetCustomCurve(AudioSourceCurveType.SpatialBlend,
                template.GetCustomCurve(AudioSourceCurveType.SpatialBlend));
            audio.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix,
                template.GetCustomCurve(AudioSourceCurveType.ReverbZoneMix));
            audio.SetCustomCurve(AudioSourceCurveType.Spread,
                template.GetCustomCurve(AudioSourceCurveType.Spread));

            return audio;
        }
    }


    public static class TurbojetUtils
    {
        public static void InitializeFromTemplate(Turbojet template, Turbojet target)
        {
            if (template == null || target == null)
            {
                Debug.LogWarning("[TurbojetUtils] Template or target is null.");
                return;
            }

            // ========= Public fields =========
            target.maxThrust = 60000f;
            target.damageFactor = template.damageFactor;

            // ========= Turbine audio（重新创建并复制配置） =========
            target.turbineAudio = CreateAudioSourceFromTemplate(
                template.turbineAudio,
                target.transform
            );

            // ========= Thrust vectoring parameters =========
            target.thrustVectoring = template.thrustVectoring;
            target.thrustVectoringGain = template.thrustVectoringGain;
            target.throttleRemap = template.throttleRemap;
            target.thrustVectoringMaxAirspeed = template.thrustVectoringMaxAirspeed;

            // ========= Atmosphere / thrust shaping =========
            target.minDensity = template.minDensity;
            target.splitThrustFactor = template.splitThrustFactor;
            target.altitudeThrust = template.altitudeThrust;

            // ========= Nozzle / vectoring references =========
            // 顶层装配函数处理
            target.nozzles = null;
            target.vectoringTransforms = null;

            // ========= Turbine / RPM behavior =========
            target.turbineMaxPitch = template.turbineMaxPitch;
            target.minRPM = template.minRPM;
            target.maxRPM = template.maxRPM;
            target.maxSpeed = template.maxSpeed;
            target.spoolRate = template.spoolRate;
            target.startupRate = template.startupRate;

            // ========= Fuel =========
            target.fuelConsumptionMin = template.fuelConsumptionMin;
            target.fuelConsumptionMax = template.fuelConsumptionMax;

            // ========= Damage / failure =========
            target.damageThreshold = template.damageThreshold;

            // criticalParts 绑定为当前 AeroPart
            AeroPart part = target.GetComponent<AeroPart>();
            if (part != null)
            {
                target.criticalParts = new UnitPart[] { part };
            }
            else
            {
                target.criticalParts = null;
                Debug.LogWarning(
                    $"[TurbojetUtils] AeroPart not found on {target.gameObject.name}"
                );
            }

            target.failureMessage = template.failureMessage;
            target.failureMessageAudio = template.failureMessageAudio;
        }

        private static AudioSource CreateAudioSourceFromTemplate(
            AudioSource template,
            Transform parent)
        {
            if (template == null || parent == null)
                return null;

            AudioSource audio = parent.gameObject.AddComponent<AudioSource>();

            // ---- AudioClip & mixer ----
            audio.clip = template.clip;
            audio.outputAudioMixerGroup = template.outputAudioMixerGroup;

            // ---- Playback ----
            audio.playOnAwake = template.playOnAwake;
            audio.loop = template.loop;
            audio.priority = template.priority;

            // ---- Volume / pitch ----
            audio.volume = template.volume;
            audio.pitch = template.pitch;

            // ---- 3D sound ----
            audio.spatialBlend = template.spatialBlend;
            audio.dopplerLevel = template.dopplerLevel;
            audio.spread = template.spread;
            audio.rolloffMode = template.rolloffMode;
            audio.minDistance = template.minDistance;
            audio.maxDistance = template.maxDistance;

            // ---- Curves ----
            audio.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                template.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            audio.SetCustomCurve(AudioSourceCurveType.SpatialBlend,
                template.GetCustomCurve(AudioSourceCurveType.SpatialBlend));
            audio.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix,
                template.GetCustomCurve(AudioSourceCurveType.ReverbZoneMix));
            audio.SetCustomCurve(AudioSourceCurveType.Spread,
                template.GetCustomCurve(AudioSourceCurveType.Spread));

            return audio;
        }
    }



}
