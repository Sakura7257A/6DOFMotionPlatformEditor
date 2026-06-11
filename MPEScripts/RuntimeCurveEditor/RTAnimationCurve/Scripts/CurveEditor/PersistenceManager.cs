/*************************************************************************
 * 功能：曲线持久化管理 (已重构为 JSON 实体文件存储)
*************************************************************************/
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace RuntimeCurveEditor
{
    // ================== 定义 JSON 序列化的数据结构 ==================
    [System.Serializable]
    public class KeyData
    {
        public float time;
        public float value;
        public float inTangent;
        public float inWeight;
        public float outTangent;
        public float outWeight;
        public int weightedMode;
    }

    [System.Serializable]
    public class MyCurveData
    {
        public string name;
        public List<KeyData> keys = new List<KeyData>();
        public List<string> contextMenus = new List<string>();
    }

    [System.Serializable]
    public class MyCurveFormData
    {
        public int curveCount;
        public MyCurveData curve1 = new MyCurveData();
        public MyCurveData curve2 = new MyCurveData();
        public MyCurveData curve3 = new MyCurveData();
        public bool isActiveCurve;
        public bool firstCurveSelected;
        public Rect gradRect;
    }

    [System.Serializable]
    public class EditorSaveData
    {
        public Vector3 windowPos;
        public Vector2 windowSize;
        public bool windowClosed;
        public float zoomLevel;
        public float zoomRatioX;
        public float zoomRatioY;

        public List<MyCurveFormData> forms = new List<MyCurveFormData>();
        public List<MyCurveData> freeCurves = new List<MyCurveData>();
    }

    // ================== 核心存取管理器 ==================
    static class PersistenceManager
    {
        const string lastFileLoaded = "lastFileLoaded";

        // 获取保存路径 (和你的 DataRecorder 导出逻辑保持一致，保存在可执行文件同级的 SavedCurves 文件夹内)
        public static string GetSaveDirectory()
        {
            string exeDirectory = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(exeDirectory, "SavedCurves");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static string GetFilePath(string configName)
        {
            return Path.Combine(GetSaveDirectory(), configName + ".json");
        }

        // --- 保存数据为 JSON 文件 ---
        public static void SaveData(string configName, Object obj, CurveWindow curveWindow, List<CurveForm> curveFormList,
                                      CurveForm activeCurveForm, Dictionary<AnimationCurve, List<ContextMenu>> dictCurvesContextMenus)
        {
            EditorSaveData saveData = new EditorSaveData();

            // 1. 保存 UI 与视口状态
            saveData.windowPos = curveWindow.transform.localPosition;
            saveData.windowSize = curveWindow.GetComponent<RectTransform>().sizeDelta;
            saveData.windowClosed = curveWindow.WindowClosed;

            var entireRect = curveWindow.curveLines.EntireRect;
            var gridRect = curveWindow.curveLines.GridRect;
            saveData.zoomLevel = curveWindow.GetComponent<ZoomBehaviour>().Level;
            saveData.zoomRatioX = (gridRect.x - entireRect.x) / gridRect.width;
            saveData.zoomRatioY = (gridRect.y - entireRect.y) / gridRect.height;

            FieldInfo[] fields = obj.GetType().GetFields();

            // 2. 保存激活的曲线组数据
            foreach (CurveForm curveForm in curveFormList)
            {
                MyCurveFormData formData = new MyCurveFormData();
                formData.curveCount = 1;
                if (curveForm.curve2 != null) formData.curveCount++;
                if (curveForm.curve3 != null) formData.curveCount++;

                formData.curve1 = ExtractCurveData(curveForm.curve1, fields, obj, dictCurvesContextMenus);
                if (curveForm.curve2 != null) formData.curve2 = ExtractCurveData(curveForm.curve2, fields, obj, dictCurvesContextMenus);
                if (curveForm.curve3 != null) formData.curve3 = ExtractCurveData(curveForm.curve3, fields, obj, dictCurvesContextMenus);

                formData.isActiveCurve = (curveForm.curve1 == activeCurveForm.curve1);
                formData.firstCurveSelected = curveForm.firstCurveSelected;
                formData.gradRect = curveForm.gradRect;

                saveData.forms.Add(formData);
            }

            // 3. 保存尚未加入窗口的“自由曲线”
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof(AnimationCurve))
                {
                    AnimationCurve curve = field.GetValue(obj) as AnimationCurve;
                    if (curveFormList.Find(x => x.curve1 == curve || x.curve2 == curve || x.curve3 == curve) == null)
                    {
                        MyCurveData freeData = new MyCurveData();
                        freeData.name = field.Name;
                        foreach (Keyframe key in curve.keys) freeData.keys.Add(ExtractKeyData(key));
                        saveData.freeCurves.Add(freeData);
                    }
                }
            }

            // 4. 写入 JSON 实体文件
            string json = JsonUtility.ToJson(saveData, true); // true表示带缩进格式化，方便人类阅读
            File.WriteAllText(GetFilePath(configName), json);

            PlayerPrefs.SetString(lastFileLoaded, configName);
            PlayerPrefs.Save();
        }

        // --- 读取 JSON 文件 ---
        public static void LoadData(string configName, Object obj, CurveWindow curveWindow, CurveLines curveLines, List<CurveForm> curveFormList,
                                         Dictionary<AnimationCurve, List<ContextMenu>> dictCurvesContextMenus)
        {
            string filePath = GetFilePath(configName);
            if (!File.Exists(filePath)) return;

            string json = File.ReadAllText(filePath);
            EditorSaveData saveData = JsonUtility.FromJson<EditorSaveData>(json);

            if (saveData == null) return;

            // 1. 还原 UI 与视口状态
            curveWindow.transform.localPosition = saveData.windowPos;
            curveWindow.GetComponent<RectTransform>().sizeDelta = saveData.windowSize;
            curveWindow.WindowClosed = saveData.windowClosed;
            curveWindow.GetComponent<ZoomBehaviour>().Level = saveData.zoomLevel;
            curveLines.zoomRatioX = saveData.zoomRatioX;
            curveLines.zoomRatioY = saveData.zoomRatioY;

            AnimationCurve activeCurve = null;

            // 2. 还原曲线组数据
            foreach (MyCurveFormData formData in saveData.forms)
            {
                AnimationCurve curve1 = RestoreCurve(formData.curve1, obj);
                if (formData.isActiveCurve) activeCurve = curve1;

                AnimationCurve curve2 = null;
                if (formData.curveCount >= 2) curve2 = RestoreCurve(formData.curve2, obj);

                AnimationCurve curve3 = null;
                if (formData.curveCount >= 3) curve3 = RestoreCurve(formData.curve3, obj);

                curveLines.AddRestoredCurveForm(curve1, curve2, curve3, formData.gradRect);

                // 还原上下文菜单
                RestoreContextMenus(curve1, formData.curve1.contextMenus, dictCurvesContextMenus);
                if (curve2 != null) RestoreContextMenus(curve2, formData.curve2.contextMenus, dictCurvesContextMenus);
                if (curve3 != null) RestoreContextMenus(curve3, formData.curve3.contextMenus, dictCurvesContextMenus);

                if (!formData.firstCurveSelected)
                {
                    CurveForm cf = curveFormList.Find(x => x.curve1 == curve1);
                    if (cf != null) cf.firstCurveSelected = false;
                }
            }

            curveLines.RestoreActiveCurve(activeCurve);
            Reset(curveWindow, curveLines, true);

            // 3. 还原未绑定在 CurveForm 的剩余自由曲线
            foreach (MyCurveData freeData in saveData.freeCurves)
            {
                RestoreCurve(freeData, obj);
            }

            PlayerPrefs.SetString(lastFileLoaded, configName);
            PlayerPrefs.Save();
        }

        // ================== 辅助方法 ==================

        static MyCurveData ExtractCurveData(AnimationCurve curve, FieldInfo[] fields, Object obj, Dictionary<AnimationCurve, List<ContextMenu>> dict)
        {
            MyCurveData data = new MyCurveData();
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof(AnimationCurve) && (field.GetValue(obj) as AnimationCurve == curve))
                {
                    data.name = field.Name;
                    break;
                }
            }
            foreach (Keyframe key in curve.keys)
            {
                data.keys.Add(ExtractKeyData(key));
            }

            if (dict != null && dict.ContainsKey(curve))
            {
                foreach (ContextMenu cm in dict[curve])
                {
                    data.contextMenus.Add(cm.ToString());
                }
            }
            return data;
        }

        static KeyData ExtractKeyData(Keyframe key)
        {
            return new KeyData
            {
                time = key.time,
                value = key.value,
                inTangent = key.inTangent,
                inWeight = key.inWeight,
                outTangent = key.outTangent,
                outWeight = key.outWeight,
                weightedMode = (int)key.weightedMode
            };
        }

        static AnimationCurve RestoreCurve(MyCurveData data, Object obj)
        {
            AnimationCurve curve = null;
            FieldInfo fi = null;

            if (!string.IsNullOrEmpty(data.name))
            {
                fi = obj.GetType().GetField(data.name);
                if (fi != null) curve = fi.GetValue(obj) as AnimationCurve;
            }

            if (curve == null) curve = new AnimationCurve();

            while (curve.length > 0) curve.RemoveKey(0);

            foreach (KeyData k in data.keys)
            {
                Keyframe key = new Keyframe(k.time, k.value, k.inTangent, k.outTangent, k.inWeight, k.outWeight);
                key.weightedMode = (WeightedMode)k.weightedMode;
                curve.AddKey(key);
            }

            if (fi != null) fi.SetValue(obj, curve);
            return curve;
        }

        static void RestoreContextMenus(AnimationCurve curve, List<string> menuStrings, Dictionary<AnimationCurve, List<ContextMenu>> dict)
        {
            if (!dict.ContainsKey(curve)) dict[curve] = new List<ContextMenu>();
            dict[curve].Clear();

            foreach (string str in menuStrings)
            {
                ContextMenu cm = new ContextMenu();
                cm.UnpackData(str);
                dict[curve].Add(cm);
            }
        }

        // --- 核心改造：通过扫描文件夹返回所有 .json 文件列表 ---
        public static List<string> GetNamesList()
        {
            List<string> list = new List<string>();
            string dir = GetSaveDirectory();
            if (Directory.Exists(dir))
            {
                string[] files = Directory.GetFiles(dir, "*.json");
                foreach (string file in files)
                {
                    // 只返回文件名供 UI 显示，隐藏 .json 后缀
                    list.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            return list;
        }

        // --- 核心改造：删除对应的实体文件 ---
        public static void DeleteFile(string name)
        {
            string path = GetFilePath(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (PlayerPrefs.GetString(lastFileLoaded) == name)
            {
                PlayerPrefs.DeleteKey(lastFileLoaded);
            }
            PlayerPrefs.Save();
        }

        public static string GetLastFile()
        {
            if (PlayerPrefs.HasKey(lastFileLoaded))
            {
                return PlayerPrefs.GetString(lastFileLoaded);
            }
            return null;
        }

        public static void RemoveLastFileKey()
        {
            PlayerPrefs.DeleteKey(lastFileLoaded);
        }

        public static void Reset(CurveWindow curveWindow, CurveLines curveLines, bool reload = false)
        {
            curveWindow.ResetScreenSize(reload);
            curveLines.ResetRect();
        }
    }
}