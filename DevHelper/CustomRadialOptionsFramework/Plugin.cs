using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RadialMenuAction;

namespace CustomRadialOptions
{
    [BepInPlugin("Experimental.assassin1076.radialmenuExtra", "Radial Menu Extension", "1.0.0")]
    public class RadialMenuExtPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony harmony = new Harmony("Experimental.assassin1076.radialmenuExtra");
            harmony.PatchAll();

            Logger.LogInfo("Radial Menu Extension loaded");
            /*
            // 注册一个示例 Mod Action
            ModRadialActionRegistry.Register(
                1000,
                new TestModAction()
            );
            */
        }
    }

    class TestModAction : IModRadialAction
    {
        public string Name => "Test Action";
        public Sprite Icon => null; // 可加载资源

        public bool Allowed(Aircraft aircraft)
        {
            return aircraft != null;
        }

        public void Execute(Aircraft aircraft)
        {
            Debug.Log("Test Mod Action Triggered!");
        }
    }


    public interface IModRadialAction
    {
        string Name { get; }
        Sprite Icon { get; }

        bool Allowed(Aircraft aircraft);
        void Execute(Aircraft aircraft);
    }

    //本质上是利用了Enum类型的底层为int，通过引入不合法的Enum id来扩充可行的操作集，
    //插件将自动化注册你的操作类型并分配不合法id，并在执行时拦截不合法id执行你的操作类型中规定的操作
    public static class ModRadialActionRegistry
    {
        private static readonly Dictionary<int, IModRadialAction> actions = new();
        private static readonly Dictionary<int, RadialMenuAction> soCache = new();

        public static void Register(int id, IModRadialAction action)
        {
            actions[id] = action;
        }

        public static IEnumerable<RadialMenuAction> GetRadialMenuActions()
        {
            foreach (var kv in actions)
            {
                if (!soCache.TryGetValue(kv.Key, out var so))
                {
                    so = ModRadialActionFactory.Create(kv.Key, kv.Value);
                    soCache[kv.Key] = so;
                }
                yield return so;
            }
        }

        public static bool Allowed(int id, Aircraft aircraft)
        {
            return actions.TryGetValue(id, out var a) && a.Allowed(aircraft);
        }

        public static void Execute(int id, Aircraft aircraft)
        {
            if (actions.TryGetValue(id, out var a))
                a.Execute(aircraft);
        }
    }

    public static class ModRadialActionFactory
    {
        public static RadialMenuAction Create(int id, IModRadialAction action)
        {
            var so = ScriptableObject.CreateInstance<RadialMenuAction>();
            so.name = $"ModRadialAction_{id}";

            // enum 当 int 用
            SetPrivateField(so, "actionType", (ActionType)id);

            // 如果有显示字段
            SetPrivateField(so, "DisplayName", action.Name);

            return so;
        }

        static void SetPrivateField<T>(object obj, string field, T value)
        {
            AccessTools.Field(obj.GetType(), field).SetValue(obj, value);
        }
    }

    [HarmonyPatch(typeof(RadialMenuMain), "SetupMain")]
    class Patch_RadialMenuMain_BuildMenu
    {
        static void Prefix(RadialMenuMain __instance)
        {
            InjectModActions(__instance);
        }

        static void InjectModActions(RadialMenuMain menu)
        {
            var field = AccessTools.Field(typeof(RadialMenuMain), "actionsMain");
            var original = (RadialMenuAction[])field.GetValue(menu);

            var list = original.ToList();

            foreach (var modAction in ModRadialActionRegistry.GetRadialMenuActions())
            {
                int id = (int)GetPrivateField<ActionType>(modAction, "actionType");
                if (!list.Any(a => (int)GetPrivateField<ActionType>(a, "actionType") == id))
                    list.Add(modAction);
            }

            field.SetValue(menu, list.ToArray());
        }

        static T GetPrivateField<T>(object obj, string field)
        {
            return (T)AccessTools.Field(obj.GetType(), field).GetValue(obj);
        }
    }


    [HarmonyPatch(typeof(RadialMenuAction), nameof(RadialMenuAction.AllowedOnAircraft))]
    class Patch_RadialMenuAction_Allowed
    {
        static bool Prefix(
            RadialMenuAction __instance,
            Aircraft aircraft,
            ref bool __result)
        {
            int id = (int)GetActionType(__instance);

            if (id >= 1000)
            {
                __result = ModRadialActionRegistry.Allowed(id, aircraft);
                return false;
            }
            return true;
        }

        static ActionType GetActionType(RadialMenuAction a)
            => (ActionType)AccessTools.Field(a.GetType(), "actionType").GetValue(a);
    }

    [HarmonyPatch(typeof(RadialMenuAction), nameof(RadialMenuAction.TriggerAction))]
    class Patch_RadialMenuAction_Trigger
    {
        static bool Prefix(RadialMenuAction __instance, Aircraft aircraft)
        {
            int id = (int)GetActionType(__instance);

            if (id >= 1000)
            {
                ModRadialActionRegistry.Execute(id, aircraft);
                return false;
            }
            return true;
        }

        static ActionType GetActionType(RadialMenuAction a)
            => (ActionType)AccessTools.Field(a.GetType(), "actionType").GetValue(a);
    }




}
