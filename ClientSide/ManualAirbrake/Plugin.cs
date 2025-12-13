using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace AirbrakeMod
{
    [BepInPlugin("com.example.airbrakepatch", "Airbrake Brake-Threshold Patch", "1.0.0")]
    public class AirbrakePatchPlugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        private void Awake()
        {
            _harmony = new Harmony("com.example.airbrakepatch");
            try
            {
                // 找到目标类型和方法（按名字查找，适应大多数情况）
                Type airbrakeType = AccessTools.TypeByName("Airbrake");
                if (airbrakeType == null)
                {
                    Logger.LogError("Airbrake type not found. Make sure the target assembly is loaded and the class name is exactly 'Airbrake'.");
                    return;
                }

                MethodInfo updateMethod = AccessTools.Method(airbrakeType, "Update");
                if (updateMethod == null)
                {
                    Logger.LogError("Airbrake.Update method not found.");
                    return;
                }

                var transpiler = new HarmonyMethod(typeof(AirbrakePatchPlugin).GetMethod(nameof(UpdateTranspiler), BindingFlags.Static | BindingFlags.NonPublic));
                _harmony.Patch(updateMethod, transpiler: transpiler);
                Logger.LogInfo("Patched Airbrake.Update with transpiler.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to patch: " + ex);
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        // Transpiler：将 throttle == 0f 的比较替换为 brake > 0.9f
        // 注意：Harmony 的 CodeInstruction 用来操作 IL 指令流
        private static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            // 尝试找到 ControlInputs 类型以及字段 throttle 和 brake
            Type controlInputsType = AccessTools.TypeByName("ControlInputs");
            FieldInfo throttleField = null;
            FieldInfo brakeField = null;
            if (controlInputsType != null)
            {
                throttleField = AccessTools.Field(controlInputsType, "throttle");
                brakeField = AccessTools.Field(controlInputsType, "brake");
            }
            // 备用：如果上面没找到，通过字符串匹配也可以（最后手段）
            // 但优先使用反射得到的 FieldInfo，因为 CodeInstruction.operand 是 FieldInfo 对象
            if (throttleField == null || brakeField == null)
            {
                Debug.Log($"Cannot find throttleField or brakeField");
            }

            for (int i = 0; i < codes.Count; i++)
            {
                // 安全检查：需要有后面两个指令存在以匹配模式（ldfld throttle; ldc.r4 0; beq.s target）
                if (i + 2 < codes.Count)
                {
                    var inst0 = codes[i];
                    var inst1 = codes[i + 1];
                    var inst2 = codes[i + 2];

                    bool isLdfldThrottle = false;
                    if (inst0.opcode == OpCodes.Ldfld && throttleField != null && inst0.operand is FieldInfo fi0)
                    {
                        isLdfldThrottle = fi0 == throttleField || fi0.Name == "throttle";
                    }
                    else if (inst0.opcode == OpCodes.Ldfld && inst0.operand is FieldInfo fiName)
                    {
                        // 容错：仅靠名字判定
                        isLdfldThrottle = fiName.Name == "throttle";
                    }

                    bool isLdcR4Zero = inst1.opcode == OpCodes.Ldc_R4 && inst1.operand is float f1 && Math.Abs(f1 - 0f) < 1e-6f;
                    bool isBeq = inst2.opcode == OpCodes.Beq || inst2.opcode == OpCodes.Beq_S;

                    if (isLdfldThrottle && isLdcR4Zero && isBeq)
                    {
                        // 记录原分支目标，直接复用
                        var branchTarget = inst2.operand;

                        // 替换 ldfld throttle -> ldfld brake (保持栈上 controlInputs 引用)
                        FieldInfo finalBrakeField = brakeField;
                        if (finalBrakeField == null)
                        {
                            // 如果没有通过类型反射到 FieldInfo，尝试动态寻找同名 FieldInfo（容错）
                            // 从 inst0.operand 得到 FieldInfo 并替换字段名
                            if (inst0.operand is FieldInfo fallbackFi)
                            {
                                // fallback: 如果所属类型有 brake 字段，尝试获取
                                var owner = fallbackFi.DeclaringType;
                                var bf = owner?.GetField("brake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (bf != null) finalBrakeField = bf;
                            }
                        }

                        if (finalBrakeField == null)
                        {
                            // 如果实在找不到 brake 字段，就保留原指令，避免破坏代码
                            Debug.Log($"Cannot find brakeField");
                            yield return inst0;
                            continue;
                        }

                        // emit: ldfld ControlInputs::brake
                        yield return new CodeInstruction(OpCodes.Ldfld, finalBrakeField);

                        // emit: ldc.r4 0.9
                        yield return new CodeInstruction(OpCodes.Ldc_R4, 0.9f);

                        // emit: cgt.un   (compare greater-than for floats -> pushes 0/1)
                        yield return new CodeInstruction(OpCodes.Cgt_Un);

                        // emit: brtrue target  (如果比较结果为真，就跳转到原 target)
                        // 使用 brtrue (non-short) 以提高兼容性，Harmony 会在需要时修正短/长形式
                        yield return new CodeInstruction(OpCodes.Brtrue, branchTarget);

                        // 跳过被替换的后两条指令（ldc.r4 0 ; beq.s target）
                        i += 2;
                        continue;
                    }
                }

                // 默认：照常输出当前指令
                yield return codes[i];
            }
        }
    }
}
