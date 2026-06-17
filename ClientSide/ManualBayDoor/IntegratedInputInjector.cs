using HarmonyLib;
using System;
using Rewired;
using System.Collections.Generic;
using System.Linq;

/*由原来的InputFramework改进而来的集成式输入注入器，移除了原来的跨插件注册/统一注入功能，使得组件更加精简，可以集成到各种插件中

原理：

Rewired输入系统使用一个序列化的InputManager_Base类来存储开发者在开发阶段定义的各种输入Action、Category等
Rewired启动时将根据这个类实例来创建输入缓存，反查缓存等等
因此最恰当的时机是在InputManager_Base.Awake中注入新的输入Action定义，这样Rewired就会自动识别并创建对应的缓存
Harmony的Prefix补丁可以在InputManager_Base.Awake之前执行我们的代码，完成输入

闲聊环节-我是如何找到这个方法的：

任何一个程序，它接受输入，进行处理，输出结果，虽然看起来很废话，但是这是解决这个问题的核心。
观察游戏脚本，它使用了player.GetButton("Brake")这样的调用来获取输入，注意这里的“Break”，这是并不是用户的输入，那么程序是从硬盘中获取了某种表达输入定义的资源。
且观察Rewired公开API，它提供基于字符串的查询，但也提供基于ID的查询，那么必然有一个从字符串到ID的映射表。因此必然有一个反查表，输入字符串->输入ID->输入状态
反编译追踪，发现后续全部被混淆处理，但是无需被混淆吓住，既然有如此的推断，我们的核心就是追踪actionName参数的传递流程（请注意，混淆器对方法名称或字段名称的处理可能具有随机性，以下仅为示例）：

public bool GetButton(string actionName) 
 - rNtzliEWdgrwPzriDiulIMgvniqt.FzDXiVfWQGBbvSZYSGEyuqjBPIRJ(SgfgGTXhZuLFEEiaAmSzXWHeATao, actionName, true)
 - RXMjhfWyksUjXSyiqzSTpElqqAbe.xeSdWVLrYfumzENJYZSlWGmGqRnv(actionName, P_2);
 - emCdXiOfeACKvZPiGXIdmXXkQghA.TryGetValue(actionName, out var value)

传递流如上，虽然有很多混淆，但是最后注意到，actionName被传递到了一个TryGetValue方法，这很明显是字典的用法，因此我们可以推断，这个字典应该是反差表，但是不确定是运行时缓存还是原始资源定义
没关系，我们继续利用反编译器对字典的读写进行追踪，发现字典是它所属的类的构造方法中创建的，创建相关的流程如下：
    emCdXiOfeACKvZPiGXIdmXXkQghA = new ADictionary<string, papvQOccxyThfqJWVFURjgoEkihp>(LFulpJQtsinvDEJJbdrUSfNkxmUT, StringComparer.OrdinalIgnoreCase);
	for (int k = 0; k < LFulpJQtsinvDEJJbdrUSfNkxmUT; k++)
	{
		InputAction inputAction2 = VJdAASMhcumEgddjDAsETDSsMViv[k];
		try
		{
			emCdXiOfeACKvZPiGXIdmXXkQghA.Add(inputAction2.name, FFJJVeJePultrasvYXHtwgjebdvx[inputAction2.id]);
		}
		catch
		{
			Logger.LogError("Duplicate Action name \"" + inputAction2.name + "\" found in Action list. Duplicate Action names are not allowed. If you have edited the data manually outside the Rewired Input Manager, remove any duplicate Actions.");
		}
	}

可以注意到，这个字典的创建是基于VJdAASMhcumEgddjDAsETDSsMViv，这个变量是一个InputAction的列表。而它的创建就在同一个函数的靠上方位置：
    VJdAASMhcumEgddjDAsETDSsMViv = P_0.ToArray();，P_0是构造函数的参数
将该参数标记为trackTarget
追踪构造函数调用：

fiWouytnwIfBzbOqhAfwbbLIofSn(trackTarget.GetActions_Copy()) 
 <- doHiQkZdIsdBPbOzlFIvQzTcqhegA(this, KYAfzhgqhHqzcTbFsdDwPuevXnUt, _userData.ConfigVars, _controllerDataFiles, trackTarget, exsgyFjngpBQLaMXmvXDsLFAawPE, dMQrSaQUovWuUMWshTXtFPtztZxm);

定位到InputManager_Base.dOYeOiAezuIIqDngKpkpeRjXHSsnB()中，调用了doHiQkZdIsdBPbOzlFIvQzTcqhegA，传递了跟踪参数，参数为this._userData
观察该成员定义，发现它是[SerializeField]的，且只读不写，因此可以确定，这就是我们要找的输入定义资源，修改它就可以注入新的输入定义了

*/

namespace ManualBayDoor;
[HarmonyPatch(typeof(InputManager_Base), "Awake")]
    public static class RewiredActionInjector
    {
        public class ModActionDefinition
        {
            public string Name;
            public InputActionType Type;
            public string Category;

            public int AssignedId = -1;

            public ModActionDefinition(string name, InputActionType type, string category = null)
            {
                Name = name;
                Type = type;
                Category = category;
            }
        }

        private static readonly List<ModActionDefinition> pendingActions = new();

        public static void RegisterAction(string name, InputActionType type, string category = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Action name cannot be null or empty.");

            var modAction = new ModActionDefinition(name, type, category);
            pendingActions.Add(modAction);

        }

        static void Prefix(InputManager_Base __instance)
        {
            InjectActions(__instance);
        }



        private static void InjectActions(InputManager_Base manager)
        {
            var userData = manager._userData;
            if (userData == null) return;

            var actions = userData.actions;
            if (actions == null) return;

            var categories = userData.actionCategories;
            if (categories == null) return;

            var debugCategory = categories.FirstOrDefault(c => c.name == "Debug");

            int nextId = GetNextActionId(actions);

            foreach (var modAction in pendingActions)
            {
                if (actions.Any(a => a.name == modAction.Name))
                    continue;

                var action = new InputAction
                {
                    id = nextId++,
                    name = modAction.Name,
                    type = modAction.Type,
                    descriptiveName = modAction.Name,
                };

                action.categoryId = categories.FirstOrDefault(c => c.name == modAction.Category)?.id ?? debugCategory?.id ?? 0;

                action._userAssignable = true;

                actions.Add(action);

                userData.actionCategoryMap.AddAction(categories.FirstOrDefault(c => c.name == modAction.Category)?.id ?? debugCategory?.id ?? 0, action.id);

                modAction.AssignedId = action.id;
            }
        }

        private static int GetNextActionId(List<InputAction> actions)
        {
            if (actions.Count == 0)
                return 1000;

            return actions.Max(a => a.id) + 1;
        }
    }