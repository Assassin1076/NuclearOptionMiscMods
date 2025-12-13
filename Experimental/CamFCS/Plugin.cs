using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
namespace CamFCS
{
    [BepInPlugin("Experimental.Assassin1076.CamFCS", "CamFCS", "0.0.1")]
    public class CamFCS : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        private static string ConfigPath => Path.Combine(Paths.PluginPath, "CamFCS", "CamFCSConfig.json");
        private ConfigEntry<KeyboardShortcut> ShowCounter { get; set; }
        private bool done;
        private void Awake()
        {
            Logger = base.Logger;
            var harmony = new Harmony("CamFCS");
            harmony.PatchAll();
            ShowCounter = Config.Bind("Hotkeys", "Update CamFCS Parameters", new KeyboardShortcut(KeyCode.U, KeyCode.LeftControl));
        }
        private void Update()
        {
            if (done && !ShowCounter.Value.IsDown()) return;

            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                    var defaultConfig = new CamFCS_Params();
                    File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(defaultConfig, Newtonsoft.Json.Formatting.Indented));
                    Debug.Log($"[CamFCS] Generated default profile: {ConfigPath}");
                }

                string json = File.ReadAllText(ConfigPath);
                CamFCS_Params LoadedParams = JsonConvert.DeserializeObject<CamFCS_Params>(json);
                PilotPlayerState_FixedUpdateState_Prefix.Params = LoadedParams;
                Debug.Log($"[CamFCS] Parameters Reloaded。");
                done = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FlightControlMod] Failed to loaded profile: {ex}");
            }

        }
    }

    public struct CamFCS_Params
    {
        public float yaw2roll_factor;
        public float yaw2roll_limit;
        public float rollErr_limit;
        public float PID_pitch_limit;
        public float PID_yaw_limit;
        public float PID_roll_limit;

        public CamFCS_Params()
        {
            yaw2roll_factor = 0.03f;
            yaw2roll_limit = 30f;
            rollErr_limit = 45f;
            PID_pitch_limit = 0.9f;
            PID_yaw_limit = 0.9f;
            PID_roll_limit = 0.9f;
        }
    }





    // 存储状态
    











    [HarmonyPatch(typeof(PilotPlayerState))]
    [HarmonyPatch("FixedUpdateState")]
    [HarmonyPatch(new Type[] { typeof(Pilot) })]
    public static class PilotPlayerState_FixedUpdateState_Prefix
    {
        static FieldInfo forwardFlightControllerField = typeof(Autopilot)
            .GetField("forwardFlightController",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        static FieldInfo controlInputsField = typeof(PilotPlayerState)
            .GetField("controlInputs", BindingFlags.NonPublic | BindingFlags.Instance);

        static MethodInfo playerAxisControlsMethod = typeof(PilotPlayerState)
            .GetMethod("PlayerAxisControls",
                BindingFlags.NonPublic | BindingFlags.Instance);

        public static CamFCS_Params Params = new CamFCS_Params();
        

        public static int GetQ(Transform plane, Transform cam)
        {
            int ret = 0;
            Vector3 planeFwd = plane.forward.normalized;
            Vector3 planeUp = plane.up.normalized;
            Vector3 planeRight = plane.right.normalized;

            Vector3 camFwd = cam.forward.normalized;

            // 判断摄像机指向是否在飞机前半区
            float dot = Vector3.Dot(planeFwd, camFwd);

            if (dot <= 0)
            {
                Debug.Log("Camera is in the BACK hemisphere");
                return -1;
            }

            Debug.Log("Camera is in the FRONT hemisphere");

            // 在前半区，判断象限
            float x = Vector3.Dot(camFwd, planeRight); // 右正左负
            float y = Vector3.Dot(camFwd, planeUp);    // 上正下负

            if (x >= 0 && y >= 0) ret = 1; // 前上右
            if (x < 0 && y >= 0) ret = 2;  // 前上左
            if (x < 0 && y < 0) ret = 3;  // 前下左

            Vector2 vector = new Vector2(x, y);

            float length = vector.magnitude;
            ret = 4;


            return ret;
        }

        public static bool Prefix(Pilot pilot, PilotPlayerState __instance)
        {
            // 条件检查
            if (!pilot.aircraft.flightAssist) return true;
            if (CameraStateManager.cameraMode != CameraMode.orbit) return true;

            playerAxisControlsMethod.Invoke(__instance, null);
            ControlInputs inputs =
                (ControlInputs)controlInputsField.GetValue(__instance);
            //这个地方必须备份，因为PID控制器是直接覆写操作量的
            ControlInputs backup = new ControlInputs();
            backup.pitch = inputs.pitch;
            backup.yaw = inputs.yaw;
            backup.roll = inputs.roll;

            //这个Autopilot是每个cockpit都会有的，而且其中的PID参数根据飞机是不同的，存在以后精确修改的可能
            Autopilot autopilot =
                pilot.aircraft.cockpit.GetComponent<Autopilot>();

            if (autopilot == null) return false;

            var forwardFlightController =
                forwardFlightControllerField.GetValue(autopilot);

            if (forwardFlightController == null) return false;

            // 用相机方向算期往姿态
            Transform plane = pilot.aircraft.transform;
            Camera cam = Camera.main;

            Vector3 currentForward = plane.forward;
            Vector3 desiredForward = cam.transform.forward;

            // 俯仰映射
            float pitchErr = TargetCalc.GetAngleOnAxis(
                currentForward, desiredForward, plane.right);

            // 横偏映射
            float yawErr = TargetCalc.GetAngleOnAxis(
                currentForward, desiredForward, plane.up);

            // 滚转映射
            Transform cockpit = pilot.aircraft.cockpit.xform;
            Vector3 currentUp = cockpit.up;
            Vector3 forward = cockpit.forward;
            Vector3 worldUp = Vector3.up;


            float dotForwardUp = Vector3.Dot(forward, Vector3.up);
            Vector3 targetUp;
            if (dotForwardUp > 0.25f)
            {
                targetUp = Vector3.up;
            }
            else
            {
                // 把一部分横偏误差叠加上，做到转向时自动带一点滚转，限制叠加量
                float lateralOffset = Mathf.Clamp(yawErr * Params.yaw2roll_factor, -Params.yaw2roll_limit, Params.yaw2roll_limit);
                targetUp = Vector3.up + lateralOffset * cockpit.right;
            }

            // -forward 轴计算 rollErr
            float rollErr = TargetCalc.GetAngleOnAxis(currentUp, targetUp, -forward);

            rollErr = Mathf.Clamp(rollErr, -Params.rollErr_limit, Params.rollErr_limit);
            Vector3 attitudeError = new Vector3(pitchErr, yawErr, rollErr);

            // 该值用来线性放缩PID控制器的强度，防止高速状态下过度操作
            float airspeed = pilot.aircraft.rb.velocity.magnitude;

            MethodInfo applyInputsMethod =
                forwardFlightController.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m =>
                    {
                        if (m.Name != "ApplyInputs") return false;
                        var p = m.GetParameters();
                        return p.Length == 3 &&
                               p[0].ParameterType == typeof(ControlInputs) &&
                               p[1].ParameterType == typeof(float) &&
                               p[2].ParameterType == typeof(Vector3);
                    });

            applyInputsMethod.Invoke(
                forwardFlightController,
                new object[] { inputs, airspeed, attitudeError }
            );

            inputs.pitch = Mathf.Clamp(inputs.pitch, -Params.PID_pitch_limit, Params.PID_pitch_limit);
            inputs.roll = Mathf.Clamp(inputs.roll,-Params.PID_roll_limit, Params.PID_roll_limit);
            inputs.yaw = Mathf.Clamp(inputs.yaw, -Params.PID_yaw_limit, Params.PID_yaw_limit);

            inputs.pitch = Mathf.Clamp(inputs.pitch + backup.pitch, -1, 1);
            inputs.roll = Mathf.Clamp(inputs.roll + backup.roll , -1, 1);
            inputs.yaw = Mathf.Clamp(inputs.yaw + backup.yaw, -1, 1);

            controlInputsField.SetValue(__instance, inputs);

            //必须执行飞控逻辑再次过滤操作量
            pilot.aircraft.FilterInputs();

            return false;
        }
    }


}
