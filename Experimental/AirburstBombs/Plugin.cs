using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.SavedMission;
using NuclearOption.SceneLoading;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AirburstBombs
{
    [BepInPlugin("Experimental.assassin1076.AirburstBombs", "AirburstBombs", "0.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public bool done;
        private void Awake()
        {
            // Plugin startup logic
            var harmony = new Harmony("Experimental.assassin1076.AirburstBombs");
            harmony.PatchAll();
            Logger = base.Logger;
            Logger.LogInfo($"Plugin AirburstBombs is loaded!");
        }
        void Update()
        {
            if (done) return;
            if (Encyclopedia.i == null)
            {
                Logger.LogInfo("Waiting for target prefabs to load...");
                return;
            }
            foreach (MissileDefinition definition in Encyclopedia.i.missiles)
            {
                if(definition.unitName == "PAB-125")
                {
                    SubBombFactory.basePrefab = definition.unitPrefab;
                }
                if (definition.unitName == "GPO-2P Auger")
                {
                    definition.unitPrefab.AddComponent<AirburstModMarker>();
                    var missile = definition.unitPrefab.GetComponent<Missile>();
                    missile.GetWeaponInfo().airburstHeight = 150f;
                    Accessor.blastYieldField.SetValue(missile, 1920f);
                    var seeker = definition.unitPrefab.GetComponent<OpticalSeekerBomb>();
                    typeof(OpticalSeekerBomb).GetField("altitudeFuseHeight", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(seeker, 150f);
                }
            }
            done = true;
        }
    }
    public class AirburstModMarker : MonoBehaviour
    {
        public float visualYield = 0.1f;
        public int subBombCount = 24;
        public float minEjectSpeed = 20f;
        public float maxEjectSpeed = 60f;
        public float originalYield;
        public GameObject subBombPrefab;
    }

    public struct AirburstState
    {
        public float originalYield;
        public AirburstModMarker marker;
    }

    public static class Accessor
    {
        public static FieldInfo blastYieldField =
            typeof(Missile).GetField("blastYield", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate))]
    public static class MissileDetonate_AirburstPrefix
    {
        

        // Prefix：修改当量 + 缓存原始值
        public static void Prefix(
            Missile __instance
        )
        {

            if (!__instance.TryGetComponent(out AirburstModMarker marker))
                return;
            marker.originalYield = __instance.GetYield();

            if (Accessor.blastYieldField != null)
            {
                Accessor.blastYieldField.SetValue(__instance, marker.visualYield);
            }
        }
        public static void Postfix(
            Missile __instance
        )
        {
            if (!__instance.TryGetComponent(out AirburstModMarker marker))
                return;

            float subYield = 8 * marker.originalYield / marker.subBombCount;
            Vector3 pos = __instance.transform.position;

            for (int i = 0; i < marker.subBombCount; i++)
            {
                SpawnSubBomb(pos,__instance.rb.velocity ,subYield,__instance.owner);
            }
        }

        static void SpawnSubBomb(Vector3 pos, Vector3 parentVelocity, float yield,Unit owner)
        {
            var prefab = SubBombFactory.basePrefab;
            if (prefab == null)
                return;
            Missile sub = NetworkSceneSingleton<Spawner>.i.SpawnMissile(prefab,pos,prefab.transform.rotation,parentVelocity,null,owner);
            SubBombFactory.ModifyFromInstance(sub.gameObject, new Vector3(1f, 1f, 0.5f), Color.black);
            sub.gameObject.SetActive(true);

            if (sub.gameObject.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 dir = Random.onUnitSphere;
                if (dir.y > 0f) dir.y = -dir.y;
                float speed = Random.Range(0, 60);
                rb.velocity = parentVelocity + dir * speed;
            }

            if (sub.gameObject.TryGetComponent<SubBomb>(out var bomb))
            {
                bomb.yield = yield;
            }
        }


    }


    public class SubBomb : MonoBehaviour
    {
        public float yield;
        bool armed;
        public Missile missile;
        
        void Start()
        {
            missile = this.gameObject.GetComponent<Missile>();
            Accessor.blastYieldField.SetValue(missile, yield);
            missile.enabled = false;
            Invoke(nameof(Arm), 0.2f);

            Destroy(gameObject, 60f); // 生命周期上限
        }

        void Arm() { armed = true; missile.Arm(); }

        void OnCollisionEnter(Collision collision)
        {
            if (!armed)
                return;

            Explode();
        }

        void Explode()
        {
            // 调用原游戏爆炸逻辑
            missile.enabled = true;
            missile.Detonate(transform.position,false,true);

            Destroy(gameObject);
        }
    }
    public static class SubBombFactory
    {
        public static GameObject basePrefab { get; set; }
        public static void ModifyFromInstance(GameObject instance, Vector3 visualScale, Color visualColor)
        {
            if (basePrefab == null)
            {
                Debug.LogError("SubBombFactory: basePrefab is null!");
                return;
            }

            for (int i = instance.transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(instance.transform.GetChild(i).gameObject);
            }

            instance.transform.localScale = visualScale;

            var meshRenderer = instance.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                // 复制原有材质，避免修改原Prefab
                Material newMat;
                if (meshRenderer.sharedMaterial != null)
                    newMat = new Material(meshRenderer.sharedMaterial);
                else
                    newMat = new Material(Shader.Find("Sprites/Default")); // 最后备选

                // 修改颜色
                newMat.color = visualColor;

                meshRenderer.material = newMat;
            }

            Rigidbody rb = instance.GetComponent<Rigidbody>();
            if (rb == null)
                rb = instance.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            Collider col = instance.GetComponent<Collider>();
            if (col == null)
                col = instance.AddComponent<CapsuleCollider>();

            if (instance.GetComponent<SubBomb>() == null)
                instance.AddComponent<SubBomb>();

        }
    }
}
