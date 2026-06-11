/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：DataRecorder.cs
 * 作者：LeonLiu (AI Assisted)
 * 日期：2026/2/7 20:50:9
 * 功能：数据导出 (包含高阶曲线平滑拟合功能)
*************************************************************************/
using MPE;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace MPE
{
    public class DataRecorder : MonoBehaviour
    {
        [Header("UI Components")]
        public Slider progressBar;

        [Header("Settings")]
        public float maxValue;
        public float duration;

        [Header("Speed Settings")]
        [Tooltip("每一帧处理多少次10ms步进。")]
        public int stepsPerFrame = 100;

        // ✨ 新增：曲线平滑/拟合设置
        [Header("Smoothing Settings (平滑拟合)")]
        [Tooltip("是否在导出前对数据曲线进行平滑处理")]
        public bool enableSmoothing = true;
        [Tooltip("平滑采样窗口大小：数值越大，曲线越平滑，但可能会略微损失一些尖锐的极值动作 (推荐: 10~20)")]
        [Range(1, 100)]
        public int smoothWindowSize = 15;
        [Tooltip("平滑迭代次数：多次迭代能达到接近【高斯模糊】的高级丝滑拟合效果 (推荐: 3)")]
        [Range(1, 10)]
        public int smoothPasses = 3;

        private bool isRecording = false;
        private string currentFilePath;

        public PlatformModelManager platformModelManager;
        public MPEManager mpeManager;

        public void Initialization()
        {
            maxValue = progressBar.maxValue;
            duration = maxValue;
            progressBar.value = 0;
        }

        public void StartProcess()
        {
            if (!isRecording)
            {
                string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"RecordData_{timeStamp}.txt";

                string exeDirectory = Directory.GetParent(Application.dataPath).FullName+ "/RecordData";
                currentFilePath = Path.Combine(exeDirectory, fileName);

                StartCoroutine(RecordAndProgressFast());
            }
        }

        IEnumerator RecordAndProgressFast()
        {
            isRecording = true;
            const float recordInterval = 0.01f;
            float simulatedElapsed = 0f;

            Vector3[] localVertices = platformModelManager.triangleA.GetComponent<MeshFilter>().sharedMesh.vertices;

            // 1. ✨ 使用 List 收集所有原始数据，而不是直接写文件
            List<Vector3> rawDataList = new List<Vector3>();

            while (simulatedElapsed <= duration)
            {
                for (int i = 0; i < stepsPerFrame; i++)
                {
                    if (simulatedElapsed > duration) break;

                    float targetPitch = mpeManager.animCurveRed.Evaluate(simulatedElapsed);
                    float targetRoll = mpeManager.animCurveGreen.Evaluate(simulatedElapsed);
                    float yellowCurveM = mpeManager.animCurveYellow.Evaluate(simulatedElapsed) / 1000f;

                    float targetHeightM = Global.Instance.stroke + yellowCurveM;
                    Quaternion reqRot = Quaternion.Euler(targetPitch, 0, targetRoll);

                    // Center 模式高度随动补偿
                    float maxLocalY = -9999f;
                    float minLocalY = 9999f;
                    foreach (Vector3 vertex in localVertices)
                    {
                        Vector3 rotatedVertex = reqRot * vertex;
                        if (rotatedVertex.y > maxLocalY) maxLocalY = rotatedVertex.y;
                        if (rotatedVertex.y < minLocalY) minLocalY = rotatedVertex.y;
                    }
                    float localCenterY = (maxLocalY + minLocalY) / 2f;
                    targetHeightM -= localCenterY;

                    // 计算真实液压缸距离
                    float[] lengths = new float[3];
                    for (int leg = 0; leg < 3; leg++)
                    {
                        Vector3 baseVert = localVertices[leg];
                        Vector3 movedVert = (reqRot * baseVert) + new Vector3(0, targetHeightM, 0);
                        lengths[leg] = Vector3.Distance(baseVert, movedVert);
                    }

                    float valA = Mathf.Clamp(((lengths[2] - Global.Instance.stroke) / Global.Instance.maxStroke) * 10f, 0f, 10f);
                    float valB = Mathf.Clamp(((lengths[1] - Global.Instance.stroke) / Global.Instance.maxStroke) * 10f, 0f, 10f);
                    float valC = Mathf.Clamp(((lengths[0] - Global.Instance.stroke) / Global.Instance.maxStroke) * 10f, 0f, 10f);

                    // 存入 List 内存中
                    rawDataList.Add(new Vector3(valA, valB, valC));

                    simulatedElapsed += recordInterval;
                }

                progressBar.value = simulatedElapsed;
                yield return null;
            }

            // 2. ✨ 执行曲线平滑拟合算法
            if (enableSmoothing)
            {
                Debug.Log("正在执行数据曲线高斯平滑降噪...");
                rawDataList = ApplyGaussianSmoothing(rawDataList, smoothWindowSize, smoothPasses);
            }

            // 3. ✨ 将平滑后的数据一次性写入 TXT
            using (StreamWriter writer = new StreamWriter(currentFilePath, false, new UTF8Encoding(false)))
            {
                foreach (Vector3 data in rawDataList)
                {
                    writer.WriteLine($"{data.x:F6} {data.y:F6} {data.z:F6}");
                }
            }

            progressBar.value = maxValue;
            Debug.Log($"极速无损导出(含平滑)成功！已保存至: {currentFilePath}");
            isRecording = false;
        }

        // =========================================================
        // 核心曲线拟合与平滑算法 (多次滑动平均逼近高斯滤波)
        // =========================================================
        private List<Vector3> ApplyGaussianSmoothing(List<Vector3> input, int windowSize, int passes)
        {
            // 如果数据量极小或者未开启迭代，则直接返回
            if (input == null || input.Count < 3 || windowSize <= 0 || passes <= 0)
                return input;

            List<Vector3> result = new List<Vector3>(input);
            List<Vector3> temp = new List<Vector3>(input.Count);

            // 初始化临时数组
            for (int i = 0; i < input.Count; i++) temp.Add(Vector3.zero);

            // 多次迭代以达到完美曲线拟合
            for (int p = 0; p < passes; p++)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    Vector3 sum = Vector3.zero;
                    int count = 0;

                    // 以当前点为中心，向前后扩展 windowSize 进行平均采样
                    for (int j = -windowSize; j <= windowSize; j++)
                    {
                        int idx = i + j;

                        // 边界处理：超出边界的索引使用边缘的值进行镜像延伸，防止首尾数据突变跌落
                        if (idx < 0) idx = 0;
                        else if (idx >= result.Count) idx = result.Count - 1;

                        sum += result[idx];
                        count++;
                    }
                    temp[i] = sum / (float)count;
                }

                // 将本次平滑的结果覆盖回 result，以供下一次深度迭代
                for (int i = 0; i < result.Count; i++)
                {
                    result[i] = temp[i];
                }
            }

            return result;
        }
    }
}