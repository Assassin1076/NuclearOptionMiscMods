using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Better3rdCam
{
    [BepInPlugin("Better3rdCam", "Better3rdCam", "0.0.1")]
    public class Better3rdCam : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public ConfigEntry<float> mouseSensitivity { get; set; }

        private void Awake()
        {
            mouseSensitivity = Config.Bind("Better3rdCam", "Mouse Sensitivity", 100f,
            new ConfigDescription("Mouse sensitivity for orbit camera",
                new AcceptableValueRange<float>(10f, 500f)));
            // Plugin startup logic
            Logger = base.Logger;
            var harmony = new Harmony("Better3rdCam");
            harmony.PatchAll();
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }
        private void Update()
        {
            OrbitFixState.mouseSensitivity = this.mouseSensitivity.Value;
        }
    }


    public static class OrbitFixState
    {
        public static float yaw;
        public static float pitch;
        public static bool initialized;
        public static float mouseSensitivity;
    }

    public static class OrbitFields
    {
        public static readonly AccessTools.FieldRef<CameraOrbitState, float> panRef =
            AccessTools.FieldRefAccess<CameraOrbitState, float>("panView");

        public static readonly AccessTools.FieldRef<CameraOrbitState, float> tiltRef =
            AccessTools.FieldRefAccess<CameraOrbitState, float>("tiltView");
    }

    [HarmonyPatch(typeof(CameraOrbitState), "UpdateState")]
    public static class PatchCameraOrbit_Update
    {
        static void Prefix(CameraOrbitState __instance, CameraStateManager cam)
        {
            if (!OrbitFixState.initialized)
            {
                Vector3 e = cam.cameraPivot.transform.rotation.eulerAngles;
                OrbitFixState.yaw = e.y;
                OrbitFixState.pitch = e.x > 180 ? e.x - 360 : e.x;
                OrbitFixState.initialized = true;
            }

            // 使用 Unity 原生鼠标输入

            OrbitFields.panRef(__instance) = 0f;
            OrbitFields.tiltRef(__instance) = 0f;

            float rotationScale = OrbitFixState.mouseSensitivity;
            float dt = Time.unscaledDeltaTime;
            OrbitFixState.yaw += Input.GetAxisRaw("Mouse X") * rotationScale * dt;
            OrbitFixState.pitch -= Input.GetAxisRaw("Mouse Y") * rotationScale * dt;
            OrbitFixState.pitch = Mathf.Clamp(OrbitFixState.pitch, -80f, 45f);
        }

        static void Postfix(CameraOrbitState __instance, CameraStateManager cam)
        {
            const float fixedDownAngle = -10f;

            // 绕世界 Y 轴旋转（yaw）
            Quaternion qYaw = Quaternion.AngleAxis(OrbitFixState.yaw, Vector3.up);

            // 绕本地 X 轴旋转（pitch + 固定俯视）
            Quaternion qPitch = Quaternion.AngleAxis(OrbitFixState.pitch + fixedDownAngle, Vector3.right);

            // 最终旋转
            Transform pivot = cam.cameraPivot.transform;
            pivot.rotation = qYaw * qPitch;
            Vector3 baseOffset = cam.transform.position - pivot.position; // 原先偏移
            float heightIncrease = 5f; // 你希望提高的高度（单位：米）
            cam.transform.position = pivot.position + baseOffset + Vector3.up * heightIncrease;
        }
    }
}
