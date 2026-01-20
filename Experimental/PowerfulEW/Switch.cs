using BepInEx;
using BepInEx.Logging;
using CustomRadialOptions;
using UnityEngine;
namespace ActiveMirageSwitch
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("Experimental.assassin1076.radialmenuExtra", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("fun.assassin1076.powerfulEW", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            ModRadialActionRegistry.Register(
                1000,
                new TestModAction()
            );
        }
        public class TestModAction : IModRadialAction
        {
            public string Name => "Switch Active Mirage";
            public Sprite Icon => null;

            public bool Allowed(Aircraft aircraft)
            {
                return aircraft != null && aircraft.gameObject.GetComponent<PowerfulEW.ActiveMirageSwitch>() != null;
            }

            public void Execute(Aircraft aircraft)
            {
                aircraft.gameObject.GetComponent<PowerfulEW.ActiveMirageSwitch>().Switch();
            }
        }
    }
}
