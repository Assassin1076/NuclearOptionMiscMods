using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using BepInEx;
namespace AirfoilDataMod
{
    [BepInPlugin("misc.assassin1076.aeropartanalyzer", "AeroPart Analyzer", "1.0.0")]
    public class AeroPartAnalyzerPlugin : BaseUnityPlugin
    {
        public static AeroPartAnalyzerPlugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Debug.Log("[AeroPartAnalyzer] 插件已加载");
        }
        public void AnalyzeAll()
        {

            AeroPartAnalyzer.LogAllAircraftWingAreas();
        }
        public void Analyze(GameObject rootObject)
        {
            if (rootObject == null)
            {
                Debug.LogWarning("[AeroPartAnalyzer] Analyze 调用失败: rootObject 为 null");
                return;
            }

            AeroPartAnalyzer.LogWingAreas(rootObject);
        }
    }
    public static class AeroPartAnalyzer
    {
        public static void LogAllAircraftWingAreas()
        {
            if (Encyclopedia.i == null || Encyclopedia.i.aircraft == null)
            {
                Debug.LogError("[GlobalAeroStats] Encyclopedia.i.aircraft 为 null");
                return;
            }

            foreach (var aircraftDef in Encyclopedia.i.aircraft)
            {
                if (aircraftDef == null || aircraftDef.unitPrefab == null)
                {
                    Debug.LogWarning("[GlobalAeroStats] AircraftDefinition 或 unitPrefab 为 null，跳过");
                    continue;
                }

                GameObject prefab = aircraftDef.unitPrefab;
                Debug.Log($"Aircraft {aircraftDef.unitName} :");
                LogWingAreas( prefab );
            }

        }
        public static void LogWingAreas(GameObject root)
        {
            if (root == null)
            {
                Debug.LogError("Root GameObject is null!");
                return;
            }

            // 用于统计每个 airfoilID 的总面积
            Dictionary<int, float> airfoilAreaTotals = new Dictionary<int, float>();
            float totalWingArea = 0f;

            // 遍历所有子对象，包括根对象
            AeroPart[] aeroParts = root.GetComponentsInChildren<AeroPart>(true); // true 表示包括 inactive 对象
            foreach (var part in aeroParts)
            {
                if (part == null) continue;

                Type partType = part.GetType();

                // 获取私有字段 wingArea 和 airfoilID
                FieldInfo wingAreaField = partType.GetField("wingArea", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo airfoilIDField = partType.GetField("airfoilID", BindingFlags.NonPublic | BindingFlags.Instance);

                if (wingAreaField == null || airfoilIDField == null)
                {
                    Debug.LogWarning($"AeroPart {part.name} 缺少 wingArea 或 airfoilID 字段！");
                    continue;
                }

                float wingArea = (float)wingAreaField.GetValue(part);
                int airfoilID = (int)airfoilIDField.GetValue(part);

                // 累加每种翼型的面积
                if (!airfoilAreaTotals.ContainsKey(airfoilID))
                {
                    airfoilAreaTotals[airfoilID] = 0f;
                }
                airfoilAreaTotals[airfoilID] += wingArea;

                totalWingArea += wingArea;
            }

            // 打印每种翼型总面积
            foreach (var kvp in airfoilAreaTotals)
            {
                Debug.Log($"AirfoilID {kvp.Key} 总面积: {kvp.Value}");
            }

            Debug.Log($"飞行器总翼面积: {totalWingArea}");
        }
    }

    //AirfoilImportExportSystem Start
    public static class AirfoilImportExportSystem
    {
        // 导出所有翼面为可读的JSON格式
        public static string ExportAllAirfoils()
        {
            List<Airfoil> airfoilsList = GetAllAirfoils();
            List<AirfoilData> exportData = new List<AirfoilData>();

            for (int i = 0; i < airfoilsList.Count; i++)
            {
                Airfoil af = airfoilsList[i];
                var airfoilData = new AirfoilData
                {
                    id = i,
                    name = af.name,
                    liftCurve = CurveToData(af.liftCoef),
                    dragCurve = CurveToData(af.dragCoef)
                };
                exportData.Add(airfoilData);
            }

            // 使用Newtonsoft.Json进行格式化输出，便于人类阅读[citation:2][citation:4]
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(new AirfoilCollection { airfoils = exportData }, settings);
        }

        // 导入并更新翼面数据
        public static ImportResult ImportAirfoils(string importData)
        {
            try
            {
                var collection = JsonConvert.DeserializeObject<AirfoilCollection>(importData);
                var allAirfoils = GetAllAirfoils();
                var result = new ImportResult();

                if (collection?.airfoils == null)
                {
                    result.success = false;
                    result.errorMessage = "无效的导入数据格式";
                    return result;
                }

                foreach (var importedData in collection.airfoils)
                {
                    // 根据名称查找匹配的翼面
                    var targetAirfoil = allAirfoils.Find(af => af.name == importedData.name);
                    if (targetAirfoil != null)
                    {
                        if (importedData.liftCurve != null)
                        {
                            UpdateCurve(targetAirfoil.liftCoef, importedData.liftCurve);
                            if (!result.updatedAirfoils.Contains(importedData.name))
                                result.updatedAirfoils.Add(importedData.name);
                        }
                        if (importedData.dragCurve != null)
                        {
                            UpdateCurve(targetAirfoil.dragCoef, importedData.dragCurve);
                            if (!result.updatedAirfoils.Contains(importedData.name))
                                result.updatedAirfoils.Add(importedData.name);
                        }
                    }
                    else
                    {
                        result.missingAirfoils.Add(importedData.name);
                    }
                }

                result.success = true;
                return result;
            }
            catch (Exception e)
            {
                return new ImportResult
                {
                    success = false,
                    errorMessage = $"导入失败: {e.Message}"
                };
            }
        }

        // 获取所有翼面
        private static List<Airfoil> GetAllAirfoils()
        {
            List<Airfoil> airfoilsList = new List<Airfoil>();
            foreach (var ac in Encyclopedia.i.aircraft)
            {
                ac.aircraftParameters.AddAirfoils(ref airfoilsList);
            }
            return airfoilsList;
        }

        // 将AnimationCurve转换为可序列化数据
        private static CurveData CurveToData(AnimationCurve curve)
        {
            if (curve == null || curve.keys == null || curve.keys.Length == 0)
                return null;

            var curveData = new CurveData
            {
                preWrapMode = curve.preWrapMode.ToString(),
                postWrapMode = curve.postWrapMode.ToString(),
                keys = new List<KeyframeData>()
            };

            foreach (var key in curve.keys)
            {
                curveData.keys.Add(new KeyframeData
                {
                    time = key.time,
                    value = key.value,
                    inTangent = key.inTangent,
                    outTangent = key.outTangent,
                    inWeight = key.inWeight,
                    outWeight = key.outWeight
                });
            }

            return curveData;
        }

        // 用导入的数据更新曲线
        private static void UpdateCurve(AnimationCurve targetCurve, CurveData sourceData)
        {
            if (targetCurve == null || sourceData == null) return;

            // 清空原有关键帧
            while (targetCurve.length > 0)
            {
                targetCurve.RemoveKey(0);
            }

            // 设置WrapMode
            if (Enum.TryParse<WrapMode>(sourceData.preWrapMode, out var preWrap))
                targetCurve.preWrapMode = preWrap;
            if (Enum.TryParse<WrapMode>(sourceData.postWrapMode, out var postWrap))
                targetCurve.postWrapMode = postWrap;

            // 添加新的关键帧
            foreach (var keyData in sourceData.keys)
            {
                var keyframe = new Keyframe(
                    keyData.time,
                    keyData.value,
                    keyData.inTangent,
                    keyData.outTangent,
                    keyData.inWeight,
                    keyData.outWeight
                );
                targetCurve.AddKey(keyframe);
            }
        }

        // 这个方法可以导出更方便Matlab读取的格式（其实是因为我不想重新写matlab脚本了（逃
        public static string ExportAllAirfoilsLegacy()
        {
            List<Airfoil> airfoilsList = GetAllAirfoils();
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < airfoilsList.Count; i++)
            {
                Airfoil af = airfoilsList[i];
                af.id = i;

                sb.AppendLine($"[id: {af.id} : {af.name}]");

                sb.AppendLine("LIFT:");
                AppendCurve(sb, af.liftCoef);

                sb.AppendLine("DRAG:");
                AppendCurve(sb, af.dragCoef);

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void AppendCurve(StringBuilder sb, AnimationCurve curve)
        {
            if (curve == null || curve.keys == null)
                return;

            sb.AppendLine($"PreWrapMode: {curve.preWrapMode}");
            sb.AppendLine($"PostWrapMode: {curve.postWrapMode}");

            foreach (var k in curve.keys)
            {
                sb.AppendLine(
                    $"{k.time}, {k.value}, {k.inTangent}, {k.outTangent}, {k.inWeight}, {k.outWeight};"
                );
            }
        }
    }

    // 数据类定义
    [System.Serializable]
    [JsonObject(MemberSerialization.Fields)]
    public class AirfoilCollection
    {
        public List<AirfoilData> airfoils;
    }

    [System.Serializable]
    [JsonObject(MemberSerialization.Fields)]
    public class AirfoilData
    {
        public int id;
        public string name;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public CurveData liftCurve;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public CurveData dragCurve;
    }

    [System.Serializable]
    [JsonObject(MemberSerialization.Fields)]
    public class CurveData
    {
        public string preWrapMode;
        public string postWrapMode;
        public List<KeyframeData> keys;
    }

    [System.Serializable]
    [JsonObject(MemberSerialization.Fields)]
    public class KeyframeData
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;
        public float inWeight;
        public float outWeight;
    }

    // 导入结果类
    public class ImportResult
    {
        public bool success;
        public string errorMessage;
        public List<string> updatedAirfoils = new List<string>();
        public List<string> missingAirfoils = new List<string>();
    }


    //AirfoilImportExportSystem End




}
