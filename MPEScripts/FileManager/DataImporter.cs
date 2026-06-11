/*************************************************************************
 *  Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 *  公司：SWEET
 *  项目：MotionPlatformEditor
 *  文件：DataImporter.cs
 *  作者：LeonLiu
 *  日期：2026/3/12 13:7:32
 *  功能：TXT数据反推关键帧 (内置正运动学数值求解器与DP曲线降噪算法)
*************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using RuntimeCurveEditor;

namespace MPE
{
    public class DataImporter : MonoBehaviour
    {
        [Header("UI & References")]
        public Slider progressBar;
        public MPEManager mpeManager;
        public PlatformModelManager platformModelManager;
        public RTAnimationCurve rtAnimationCurve;

        [Header("Import Settings")]
        [Tooltip("角度曲线容差(度)，越大数据越精简，但细节越少")]
        public float angleTolerance = 0.1f;
        [Tooltip("高度曲线容差(毫米)，越大数据越精简")]
        public float heightTolerance = 0.5f;

        private bool isImporting = false;



        private void OnGUI()
        {
            if(GUILayout.Button("导入最新数据"))
            {
                AutoImportLatestTxt();
            }
        }

        /// <summary>
        /// 一键自动导入最新导出的 TXT 文件
        /// 建议绑定到 UI 的 "导入最新数据" 按钮上
        /// </summary>
        public void AutoImportLatestTxt()
        {
            if (isImporting) return;

            string exeDirectory = Directory.GetParent(Application.dataPath).FullName;
            DirectoryInfo dirInfo = new DirectoryInfo(exeDirectory);

            // 自动寻找目录下的所有 RecordData_ 开头的 txt，并按时间倒序排列
            var files = dirInfo.GetFiles("RecordData_*.txt").OrderByDescending(f => f.LastWriteTime).ToList();

            if (files.Count == 0)
            {
                Debug.LogWarning("未找到任何导出的 RecordData_*.txt 数据文件！");
                return;
            }

            string latestFilePath = files[0].FullName;
            Debug.Log($"找到最新数据，开始反推演算: {latestFilePath}");

            if (progressBar != null)
            {
                progressBar.value = 0;
                progressBar.maxValue = 100f;
            }

            StartCoroutine(ReverseEngineerProcess(latestFilePath));
        }

        IEnumerator ReverseEngineerProcess(string filePath)
        {
            isImporting = true;
            string[] lines = File.ReadAllLines(filePath);

            float baseStrokeM = Global.Instance.stroke / 1000f;
            float maxStrokeM = Global.Instance.maxStroke / 1000f;
            Vector3[] localVertices = platformModelManager.triangleA.GetComponent<MeshFilter>().sharedMesh.vertices;

            List<Vector2> rawPitch = new List<Vector2>(lines.Length);
            List<Vector2> rawRoll = new List<Vector2>(lines.Length);
            List<Vector2> rawYellow = new List<Vector2>(lines.Length);

            Vector3 lastPose = Vector3.zero; // x: pitch, y: roll, z: yellow(meters)
            float time = 0f;
            const float interval = 0.01f;

            // --- 阶段 1：运动学反推 (将液压缸长度逆运算为 角度与高度) ---
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] parts = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                float valA = float.Parse(parts[0]);
                float valB = float.Parse(parts[1]);
                float valC = float.Parse(parts[2]);

                // 将 0-10 的标量还原为物理绝对长度 (米)
                float La = (valA / 10f) * maxStrokeM + baseStrokeM;
                float Lb = (valB / 10f) * maxStrokeM + baseStrokeM;
                float Lc = (valC / 10f) * maxStrokeM + baseStrokeM;

                // 使用数值求解器算出当前的 俯仰、横滚 和 附加高度
                lastPose = SolveFK(La, Lb, Lc, lastPose, localVertices, baseStrokeM);

                rawPitch.Add(new Vector2(time, lastPose.x));
                rawRoll.Add(new Vector2(time, lastPose.y));
                rawYellow.Add(new Vector2(time, lastPose.z * 1000f)); // 存回曲线所需的毫米制

                time += interval;

                // 每处理 200 行刷新一次 UI 防止卡死
                if (i % 200 == 0 && progressBar != null)
                {
                    progressBar.value = ((float)i / lines.Length) * 50f; // 前半段进度
                    yield return null;
                }
            }

            // --- 阶段 2：曲线降噪降维 (12000个点精简为几十个关键帧) ---
            List<Vector2> reducedPitch = DouglasPeucker(rawPitch, angleTolerance);
            List<Vector2> reducedRoll = DouglasPeucker(rawRoll, angleTolerance);
            List<Vector2> reducedYellow = DouglasPeucker(rawYellow, heightTolerance);

            if (progressBar != null) progressBar.value = 80f;
            yield return null;

            // --- 阶段 3：生成 Unity AnimationCurve ---
            AnimationCurve curveRed = CreateSmoothCurve(reducedPitch);
            AnimationCurve curveGreen = CreateSmoothCurve(reducedRoll);
            AnimationCurve curveYellow = CreateSmoothCurve(reducedYellow);

            // --- 阶段 4：注入到编辑器系统 ---
            mpeManager.animCurveRed = curveRed;
            mpeManager.animCurveGreen = curveGreen;
            mpeManager.animCurveYellow = curveYellow;

            // 彻底清空当前曲线窗口并重新加载这三条反推出来的曲线
            rtAnimationCurve.NewWindow();
            rtAnimationCurve.Add(ref mpeManager.animCurveRed, ref mpeManager.animCurveGreen, ref mpeManager.animCurveYellow);

            if (progressBar != null) progressBar.value = 100f;
            Debug.Log($"反推完成！从 {lines.Length} 条底层指令中提取出 {curveRed.length} 个核心关键帧。");
            isImporting = false;
        }

        // =========================================================
        // 正运动学(FK) 数值求解器 (坐标下降法)
        // =========================================================
        private Vector3 SolveFK(float targetLa, float targetLb, float targetLc, Vector3 guess, Vector3[] vertices, float baseStrokeM)
        {
            float step = 1.0f;
            for (int iter = 0; iter < 100; iter++)
            {
                float e = GetError(guess.x, guess.y, guess.z, targetLa, targetLb, targetLc, vertices, baseStrokeM);
                Vector3 bestNext = guess;
                float bestE = e;

                // P, R 是角度(跨度大)，Y 是米(跨度极小)，步长做区分
                float stepY = step * 0.005f;

                Vector3[] dirs = {
                    new Vector3(step,0,0), new Vector3(-step,0,0),
                    new Vector3(0,step,0), new Vector3(0,-step,0),
                    new Vector3(0,0,stepY), new Vector3(0,0,-stepY)
                };

                foreach (var d in dirs)
                {
                    float testE = GetError(guess.x + d.x, guess.y + d.y, guess.z + d.z, targetLa, targetLb, targetLc, vertices, baseStrokeM);
                    if (testE < bestE)
                    {
                        bestE = testE;
                        bestNext = guess + d;
                    }
                }

                if (bestE < e) guess = bestNext;
                else step *= 0.5f; // 找不到更小误差，缩小探测步长

                if (bestE < 0.0001f || step < 0.0001f) break; // 精度达标
            }
            return guess;
        }

        private float GetError(float pitch, float roll, float yellowM, float tLa, float tLb, float tLc, Vector3[] vertices, float baseStrokeM)
        {
            float targetHeightM = baseStrokeM + yellowM;
            Quaternion rot = Quaternion.Euler(pitch, 0, roll);

            // 完全匹配 Center 模式随动补偿
            float maxLocalY = -9999f;
            float minLocalY = 9999f;
            for (int i = 0; i < 3; i++)
            {
                float y = (rot * vertices[i]).y;
                if (y > maxLocalY) maxLocalY = y;
                if (y < minLocalY) minLocalY = y;
            }
            targetHeightM -= (maxLocalY + minLocalY) / 2f;

            Vector3 offset = new Vector3(0, targetHeightM, 0);

            // 注意：DataRecorder 中 C 对应 0, B 对应 1, A 对应 2
            float lc = Vector3.Distance(vertices[0], (rot * vertices[0]) + offset);
            float lb = Vector3.Distance(vertices[1], (rot * vertices[1]) + offset);
            float la = Vector3.Distance(vertices[2], (rot * vertices[2]) + offset);

            return Mathf.Abs(la - tLa) + Mathf.Abs(lb - tLb) + Mathf.Abs(lc - tLc);
        }

        // =========================================================
        // 道格拉斯-普克 (Douglas-Peucker) 曲线特征提取算法
        // =========================================================
        private List<Vector2> DouglasPeucker(List<Vector2> points, float epsilon)
        {
            if (points == null || points.Count < 3) return points;
            List<int> keep = new List<int> { 0, points.Count - 1 };
            DPRecursive(points, 0, points.Count - 1, epsilon, keep);
            keep.Sort();

            List<Vector2> res = new List<Vector2>();
            foreach (int i in keep) res.Add(points[i]);
            return res;
        }

        private void DPRecursive(List<Vector2> points, int first, int last, float epsilon, List<int> keep)
        {
            float maxDist = 0;
            int index = 0;
            Vector2 p1 = points[first];
            Vector2 p2 = points[last];

            for (int i = first + 1; i < last; i++)
            {
                float dist = VerticalDistance(p1, p2, points[i]);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    index = i;
                }
            }

            if (maxDist > epsilon)
            {
                keep.Add(index);
                DPRecursive(points, first, index, epsilon, keep);
                DPRecursive(points, index, last, epsilon, keep);
            }
        }

        // 核心：采用“垂直误差”而非“垂直距离”，免疫时间轴无限放大导致的比例畸形问题
        private float VerticalDistance(Vector2 p1, Vector2 p2, Vector2 p)
        {
            if (Mathf.Approximately(p2.x, p1.x)) return Mathf.Abs(p.y - p1.y);
            float yLine = p1.y + (p2.y - p1.y) * (p.x - p1.x) / (p2.x - p1.x);
            return Mathf.Abs(p.y - yLine);
        }

        // =========================================================
        // 丝滑曲线重组器 (计算平滑切线)
        // =========================================================
        private AnimationCurve CreateSmoothCurve(List<Vector2> points)
        {
            AnimationCurve curve = new AnimationCurve();
            foreach (var p in points) curve.AddKey(new Keyframe(p.x, p.y));

            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].weightedMode = WeightedMode.None;
                if (keys.Length == 1) continue;

                // 使用中心差分法计算出平滑的前后曲率切线 (完美复刻 Unity 的 Auto Tangent 效果)
                if (i == 0)
                {
                    float t = (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time);
                    keys[i].outTangent = t; keys[i].inTangent = t;
                }
                else if (i == keys.Length - 1)
                {
                    float t = (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time);
                    keys[i].outTangent = t; keys[i].inTangent = t;
                }
                else
                {
                    float t = (keys[i + 1].value - keys[i - 1].value) / (keys[i + 1].time - keys[i - 1].time);
                    keys[i].inTangent = t; keys[i].outTangent = t;
                }
            }
            curve.keys = keys;
            return curve;
        }
    }
}


