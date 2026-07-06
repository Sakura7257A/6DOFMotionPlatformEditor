/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：DataRecorder.cs
 * 作者：LeonLiu (AI Assisted)
 * 功能：数据导出 (完全正序规范版：1号缸~N号缸严格按列顺序输出)
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
        public int stepsPerFrame = 100;

        [Header("Smoothing Settings")]
        public bool enableSmoothing = true;
        [Range(1, 100)]
        public int smoothWindowSize = 15;
        [Range(1, 10)]
        public int smoothPasses = 3;

        private bool isRecording = false;
        private string currentFilePath;

        public PlatformModelManager platformModelManager;
        public MPEManager mpeManager;

        public static bool EnableLegacyHardwareCompat = false;

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
                string modePrefix = mpeManager.is6DOFMode ? "6DOF" : "3DOF";
                string fileName = $"{modePrefix}_{timeStamp}.txt";

                string baseDirectory = Directory.GetParent(Application.dataPath).FullName + "/RecordData";
                string targetDirectory = Path.Combine(baseDirectory, modePrefix);

                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                currentFilePath = Path.Combine(targetDirectory, fileName);
                StartCoroutine(RecordAndProgressFast());
            }
        }

        IEnumerator RecordAndProgressFast()
        {
            isRecording = true;
            const float recordInterval = 0.01f;
            float simulatedElapsed = 0f;

            Vector3[] topVertices = platformModelManager.triangleA.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3[] baseVertices = platformModelManager.triangleB.GetComponent<MeshFilter>().sharedMesh.vertices;

            int axisCount = mpeManager.is6DOFMode ? 6 : 3;

            float minSafeLength = Global.Instance.stroke;
            float maxSafeLength = Global.Instance.stroke + Global.Instance.maxStroke;

            // ✨ 1. 获取全局动态量程 (如 10, 100, 350 等)
            float dataScale = mpeManager.GetDataScale();

            List<float[]> rawDataList = new List<float[]>();

            while (simulatedElapsed <= duration)
            {
                for (int i = 0; i < stepsPerFrame; i++)
                {
                    if (simulatedElapsed > duration) break;

                    float targetPitch = mpeManager.animCurveRed.Evaluate(simulatedElapsed);
                    float targetRoll = mpeManager.animCurveGreen.Evaluate(simulatedElapsed);
                    float targetYaw = (mpeManager.is6DOFMode && mpeManager.animCurveYaw != null) ? mpeManager.animCurveYaw.Evaluate(simulatedElapsed) : 0f;

                    float swayM = (mpeManager.is6DOFMode && mpeManager.animCurveSway != null) ? (mpeManager.animCurveSway.Evaluate(simulatedElapsed) / 1000f) : 0f;
                    float surgeM = (mpeManager.is6DOFMode && mpeManager.animCurveSurge != null) ? (mpeManager.animCurveSurge.Evaluate(simulatedElapsed) / 1000f) : 0f;

                    // ✨ 2. 空间几何非线性高度补偿 (修复了高度增量与缸长增量不匹配的 BUG)
                    float yellowCurveM = mpeManager.animCurveYellow.Evaluate(simulatedElapsed) / 1000f;
                    float targetCylinderLengthM = Global.Instance.stroke + yellowCurveM;
                    float targetHeightM = platformModelManager.GetHeightFromCylinderLength(targetCylinderLengthM);

                    Quaternion reqRot = Quaternion.Euler(targetPitch, targetYaw, targetRoll);

                    // Center 模式高度随动补偿
                    float maxLocalY = -9999f;
                    float minLocalY = 9999f;
                    foreach (Vector3 vertex in topVertices)
                    {
                        Vector3 rotatedVertex = reqRot * vertex;
                        if (rotatedVertex.y > maxLocalY) maxLocalY = rotatedVertex.y;
                        if (rotatedVertex.y < minLocalY) minLocalY = rotatedVertex.y;
                    }
                    float localCenterY = (maxLocalY + minLocalY) / 2f;
                    targetHeightM -= localCenterY;

                    Vector3 platformTranslation = new Vector3(swayM, targetHeightM, surgeM);
                    float[] currentFrameData = new float[axisCount];

                    for (int leg = 0; leg < axisCount; leg++)
                    {
                        // ✨ 3. 自适应缸号偏移映射 (通过取余数 % axisCount 完美兼容 3DOF 和 6DOF)
                        int mappedLeg = (leg + platformModelManager.cylinderIndexShift) % axisCount;

                        Vector3 localTop = topVertices[mappedLeg];
                        Vector3 worldTop = (reqRot * localTop) + platformTranslation;

                        int baseIndex = mappedLeg;
                        if (mpeManager.is6DOFMode)
                        {
                            baseIndex = (mappedLeg + 1) % 6; // 6DOF 的斯图尔特交叉连杆
                        }
                        Vector3 worldBase = baseVertices[baseIndex];

                        float rawDist = Vector3.Distance(worldTop, worldBase);

                        // 物理层面的安全软限位 (防顶缸/拉扯)
                        float safeDist = Mathf.Clamp(rawDist, minSafeLength, maxSafeLength);

                        // ✨ 4. 使用全局 dataScale 动态缩放输出量级
                        currentFrameData[leg] = Mathf.Clamp(((safeDist - Global.Instance.stroke) / Global.Instance.maxStroke) * dataScale, 0f, dataScale);
                    }

                    // ✨ 5. 彻底的正序规范映射：物理空间的1~N号缸，严格对应文件导出的第1~N列
                    float[] mappedData = new float[axisCount];
                    for (int j = 0; j < axisCount; j++)
                    {
                        mappedData[j] = currentFrameData[j];
                    }


                    rawDataList.Add(mappedData);
                    simulatedElapsed += recordInterval;
                }

                progressBar.value = simulatedElapsed;
                yield return null;
            }

            // 执行数据平滑降噪处理
            if (enableSmoothing)
            {
                rawDataList = ApplyGaussianSmoothing(rawDataList, smoothWindowSize, smoothPasses, axisCount);
            }

            // 写入本地 TXT 文件
            using (StreamWriter writer = new StreamWriter(currentFilePath, false, new UTF8Encoding(false)))
            {
                foreach (float[] data in rawDataList)
                {
                    if (mpeManager.is6DOFMode)
                    {
                        writer.WriteLine($"{data[0]:F6} {data[1]:F6} {data[2]:F6} {data[3]:F6} {data[4]:F6} {data[5]:F6}");
                    }
                    else
                    {
                        writer.WriteLine($"{data[0]:F6} {data[1]:F6} {data[2]:F6}");
                    }
                }
            }

            progressBar.value = maxValue;
            Debug.Log($"动作数据已按照 {dataScale} 量程规范正序导出成功！路径: {currentFilePath}");
            isRecording = false;
        }

        private List<float[]> ApplyGaussianSmoothing(List<float[]> input, int windowSize, int passes, int axisCount)
        {
            if (input == null || input.Count < 3 || windowSize <= 0 || passes <= 0)
                return input;

            List<float[]> result = new List<float[]>();
            foreach (var arr in input) result.Add((float[])arr.Clone());

            List<float[]> temp = new List<float[]>(input.Count);
            for (int i = 0; i < input.Count; i++) temp.Add(new float[axisCount]);

            for (int p = 0; p < passes; p++)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    float[] sum = new float[axisCount];
                    int count = 0;

                    for (int j = -windowSize; j <= windowSize; j++)
                    {
                        int idx = i + j;
                        if (idx < 0) idx = 0;
                        else if (idx >= result.Count) idx = result.Count - 1;

                        for (int axis = 0; axis < axisCount; axis++)
                        {
                            sum[axis] += result[idx][axis];
                        }
                        count++;
                    }

                    for (int axis = 0; axis < axisCount; axis++)
                    {
                        temp[i][axis] = sum[axis] / (float)count;
                    }
                }

                for (int i = 0; i < result.Count; i++)
                {
                    for (int axis = 0; axis < axisCount; axis++)
                    {
                        result[i][axis] = temp[i][axis];
                    }
                }
            }
            return result;
        }
    }
}