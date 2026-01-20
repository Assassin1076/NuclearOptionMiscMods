using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
namespace FreeBirdMod
{
    [BepInPlugin("fun.assassin1076.FreeBirdMod", "FreeBird!!!!", "1.0.0")]
    public class FreeBirdModPlugin : BaseUnityPlugin
    {
        public static AudioClip Missile_Effect;
        public static AudioClip Missile_OnDetonate;
        public bool done = false;
        public static ConfigEntry<float> GlobalVolume;
        private string AudioPath =>
            Path.Combine(Paths.PluginPath, "FreeBirdModSettings/audio/");

        void Awake()
        {
            var harmony = new Harmony("FreeBird!!!!");
            harmony.PatchAll();
            GlobalVolume = Config.Bind(
                "Audio",
                "Volume",
                1f,
                new ConfigDescription(
                    "FreeBird 音量（0.0 - 1.0）",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );
            DontDestroyOnLoad(gameObject);
            StartCoroutine(PreloadAudio());
        }

        private IEnumerator PreloadAudio()
        {
            if (!File.Exists(AudioPath + "loop.wav"))
            {
                Logger.LogError($"Audio not found: {AudioPath + "loop.wav"}");
                yield break;
            }
            if (!File.Exists(AudioPath + "explode.wav"))
            {
                Logger.LogError($"Audio not found: {AudioPath + "explode.wav"}");
                yield break;
            }
            string url = "file://" + (AudioPath + "loop.wav").Replace("\\", "/");

            using (UnityWebRequest req =
                   UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Logger.LogError($"Load audio failed: {req.error}");
                    yield break;
                }

                Missile_Effect = DownloadHandlerAudioClip.GetContent(req);
                Missile_Effect.name = "Missile_Effect";

                Logger.LogInfo("AudioClip preloaded successfully");
            }

            url = "file://" + (AudioPath + "explode.wav").Replace("\\", "/");

            using (UnityWebRequest req =
                   UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Logger.LogError($"Load audio failed: {req.error}");
                    yield break;
                }

                Missile_OnDetonate = DownloadHandlerAudioClip.GetContent(req);
                Missile_OnDetonate.name = "Missile_OnDetonate";

                Logger.LogInfo("Missile_OnDetonate preloaded successfully");
            }
        }

        void Update()
        {
            if (done) return;

            if (Encyclopedia.i == null)
            {
                Logger.LogInfo("Waiting for target prefabs to load...");
                return;
            }

            string settingsDir = Path.Combine(Paths.PluginPath, "FreeBirdModSettings");
            string jsonPath = Path.Combine(settingsDir, "missiles.json");

            Directory.CreateDirectory(settingsDir);

            List<string> allMissileNames = new List<string>();
            Dictionary<string, GameObject> prefabMap = new Dictionary<string, GameObject>();

            foreach (MissileDefinition missile in Encyclopedia.i.missiles)
            {
                if (string.IsNullOrEmpty(missile.unitName) || missile.unitPrefab == null)
                    continue;

                allMissileNames.Add(missile.unitName);
                prefabMap[missile.unitName] = missile.unitPrefab;
            }

            if (!File.Exists(jsonPath))
            {
                string json = JsonConvert.SerializeObject(
                    allMissileNames,
                    Formatting.Indented
                );

                File.WriteAllText(jsonPath, json, Encoding.UTF8);
                Logger.LogInfo($"Missile list exported: {jsonPath}");
            }

            try
            {
                string json = File.ReadAllText(jsonPath, Encoding.UTF8);
                List<string> enabledMissiles =
                    JsonConvert.DeserializeObject<List<string>>(json);

                if (enabledMissiles != null)
                {
                    foreach (string unitName in enabledMissiles)
                    {
                        if (prefabMap.TryGetValue(unitName, out GameObject prefab))
                        {
                            ModifyPrefab(prefab);
                            Logger.LogInfo($"Applied audio to missile prefab: {unitName}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to read missiles.json: {e}");
            }

            done = true;
        }

        public static void ModifyPrefab(GameObject target)
        {
            if (target == null) return;

            if (target.GetComponent<ModLoopAudio>() != null)
                return;

            target.AddComponent<ModLoopAudio>();
        }
    }
    [HarmonyPatch(typeof(Missile))]
    [HarmonyPatch(nameof(Missile.Detonate))]
    public class Missile_Detonate_Patch
    {
        public static void Prefix(Missile __instance)
        {
            var additional_comp = __instance.gameObject.GetComponent<ModLoopAudio>();
            if (additional_comp != null)
            {
                additional_comp.PlayDetonate();
            }
        }
    }

    public class ModLoopAudio : MonoBehaviour
    {
        private AudioSource source;

        void Start()
        {
            if (FreeBirdModPlugin.Missile_Effect == null)
            {
                Debug.LogWarning("[MyAudioMod] AudioClip not loaded yet");
                return;
            }

            source = gameObject.AddComponent<AudioSource>();
            source.clip = FreeBirdModPlugin.Missile_Effect;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f; // 3D
            source.volume = FreeBirdModPlugin.GlobalVolume.Value;
            source.dopplerLevel = 0f;
            source.minDistance = 10f;
            source.maxDistance = 5000f;
            source.rolloffMode = AudioRolloffMode.Custom;

            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(5000f, 0f)
            );

            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);


            source.Play();
        }
        public void PlayDetonate()
        {
            source.clip = FreeBirdModPlugin.Missile_OnDetonate;
            source.loop = false;
            source.Play();
        }
        void OnDestroy()
        {
            if (source != null)
                source.Stop();
        }
    }

}

