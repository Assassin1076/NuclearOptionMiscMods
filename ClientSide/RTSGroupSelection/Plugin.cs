using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

[BepInPlugin("misc.assassin1076.rtsgroups", "RTS Target Group Mod", "0.0.1")]
public class RTSTargetGroupsPlugin : BaseUnityPlugin
{
    private List<Unit>[] groups = new List<Unit>[10];

    private Func<CombatHUD, List<Unit>> getTargetList;
    private Func<CombatHUD, Dictionary<Unit, HUDUnitMarker>> getMarkerLookup;
    private Func<CombatHUD, AudioClip> getSelectSound;

    private MethodInfo MI_DeselectAll;

    private ConfigEntry<KeyboardShortcut>[] GroupKey { get; set; }
    private ConfigEntry<KeyboardShortcut>[] GroupSaveKey { get; set; }
    private ConfigEntry<KeyboardShortcut>[] GroupAppendKey { get; set; }
    void Awake()
    {
        Logger.LogInfo("RTS Target Group System Loaded.");
        GroupKey = new ConfigEntry<KeyboardShortcut>[10];
        GroupSaveKey = new ConfigEntry<KeyboardShortcut>[10];
        GroupAppendKey = new ConfigEntry<KeyboardShortcut>[10];
        for(int i=0; i<10; i++)
        {
            GroupKey[i] = Config.Bind("Hotkeys", $"Group {i}", new KeyboardShortcut((KeyCode)(KeyCode.Alpha0 + i)));
            GroupSaveKey[i] = Config.Bind("Hotkeys", $"Save Group {i}", new KeyboardShortcut((KeyCode)(KeyCode.Alpha0 + i), KeyCode.LeftControl));
            GroupAppendKey[i] = Config.Bind("Hotkeys", $"Append Group {i}", new KeyboardShortcut((KeyCode)(KeyCode.Alpha0 + i), KeyCode.LeftShift));
        }


        InitReflection();
        InitGroups();
    }

    void InitGroups()
    {
        for (int i = 0; i < 10; i++)
            groups[i] = new List<Unit>();
    }
    void InitReflection()
    {
        Type hudType = typeof(CombatHUD);

        {
            FieldInfo field = hudType.GetField("targetList",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var inst = Expression.Parameter(hudType, "hud");
            var fld = Expression.Field(inst, field);

            getTargetList = Expression.Lambda<Func<CombatHUD, List<Unit>>>(fld, inst).Compile();
        }

        {
            FieldInfo field = hudType.GetField("markerLookup",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var inst = Expression.Parameter(hudType, "hud");
            var fld = Expression.Field(inst, field);

            getMarkerLookup = Expression
                .Lambda<Func<CombatHUD, Dictionary<Unit, HUDUnitMarker>>>(fld, inst)
                .Compile();
        }

        {
            FieldInfo field = hudType.GetField("selectSound",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var inst = Expression.Parameter(hudType, "hud");
            var fld = Expression.Field(inst, field);

            getSelectSound = Expression
                .Lambda<Func<CombatHUD, AudioClip>>(fld, inst)
                .Compile();
        }

        // --- CombatHUD API ---
        MI_DeselectAll = hudType.GetMethod("DeselectAll", BindingFlags.Public | BindingFlags.Instance);
    }

    void Update()
    {
        HandleInput();
    }

    List<Unit> CurrentList =>
        CombatHUD.i ? getTargetList(CombatHUD.i) : null;

    Dictionary<Unit, HUDUnitMarker> MarkerLookup =>
        CombatHUD.i ? getMarkerLookup(CombatHUD.i) : null;

    void HandleInput()
    {

        for (int i = 0; i < 10; i++)
        {
            if (GroupSaveKey[i].Value.IsDown())
            {
                SaveGroup(i);
                break;
            }
            else if (GroupAppendKey[i].Value.IsDown())
            {
                AppendGroup(i);
                break;
            }
            else if (GroupKey[i].Value.IsDown())
            {
                LoadGroup(i);
                break;
            }
        }
    }

    void SelectUnitsBatch(List<Unit> units, List<Unit> savedGroup)
    {
        var hud = CombatHUD.i;
        if (hud == null) return;

        bool anySelected = false;

        Dictionary<Unit, HUDUnitMarker> lookup = MarkerLookup;
        if (lookup == null) return;

        foreach (var u in units.ToList()) // ToList() 防止修改遍历目标
        {
            if (u == null)
            {
                savedGroup.Remove(u);
                continue;
            }

            if (!lookup.ContainsKey(u))
            {
                // 无效单位 → 从保存组剔除
                savedGroup.Remove(u);
                continue;
            }

            // 提示 HUD 标记、加入 targetList
            lookup[u].SelectMarker();
            hud.aircraft.weaponManager.AddTargetList(u);

            anySelected = true;
        }

        // 只播放一次提示音
        if (anySelected)
        {
            var clip = getSelectSound(hud);
            SoundManager.PlayInterfaceOneShot(clip);
        }
    }

    void SaveGroup(int id)
    {
        var list = CurrentList;
        if (list == null) return;

        groups[id] = new List<Unit>(list);
        SceneSingleton<AircraftActionsReport>.i.ReportText("Saved current target list to Group " + id.ToString(), 4f);
    }

    void LoadGroup(int id)
    {
        var hud = CombatHUD.i;
        if (!hud) return;

        // 清空当前
        MI_DeselectAll.Invoke(hud, new object[] { false });

        // 使用批量选择（一次音效）
        SelectUnitsBatch(groups[id], groups[id]);
        SceneSingleton<AircraftActionsReport>.i.ReportText("Loaded saved target list Group " + id.ToString(), 4f);
    }

    void AppendGroup(int id)
    {
        var hud = CombatHUD.i;
        if (!hud) return;

        HashSet<Unit> current = new HashSet<Unit>(CurrentList ?? new List<Unit>());
        List<Unit> saved = groups[id];

        // 只选择当前未选的单位
        List<Unit> toAdd = new List<Unit>();
        foreach (var u in saved)
        {
            if (u != null && !current.Contains(u))
                toAdd.Add(u);
        }

        SelectUnitsBatch(toAdd, saved);
        SceneSingleton<AircraftActionsReport>.i.ReportText("Appended saved target list Group " + id.ToString(), 4f);
    }
}
