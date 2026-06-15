/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：DataRecorder.cs
 * 作者：LeonLiu (AI Assisted)
 * 功能：数据导出 (包含高阶曲线平滑拟合功能，全面兼容6自由度与软限位)
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

                string exeDirectory = Directory.GetParent(Application.dataPath).FullName + "/RecordData";

                if (!Directory.Exists(exeDirectory))
                {
                    Directory.CreateDirectory(exeDirectory);
                }

                currentFilePath = Path.Combine(exeDirectory, fileName);

                StartCoroutine(RecordAndProgressFast());
            }
        }

        IEnumerator RecordAndProgressFast()
        {
            isRecording = true;
            const float recordInterval = 0.01f;
            float simulatedElapsed = 0f;

            // 获取上下平台的顶点数据
            Vector3[] topVertices = platformModelManager.triangleA.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3[] baseVertices = platformModelManager.triangleB.GetComponent<MeshFilter>().sharedMesh.vertices;

            int axisCount = mpeManager.is6DOFMode ? 6 : 3;

            float minSafeLength = Global.Instance.stroke;
            float maxSafeLength = Global.Instance.stroke + Global.Instance.maxStroke;

            List<float[]> rawDataList = new List<float[]>();

            while (simulatedElapsed <= duration)
            {
                for (int i = 0; i < stepsPerFrame; i++)
                {
                    if (simulatedElapsed > duration) break;

                    // ✨ 读取 6 个通道的曲线数据
                    float targetPitch = mpeManager.animCurveRed.Evaluate(simulatedElapsed);
                    float targetRoll = mpeManager.animCurveGreen.Evaluate(simulatedElapsed);
                    float targetYaw = (mpeManager.is6DOFMode && mpeManager.animCurveYaw != null) ? mpeManager.animCurveYaw.Evaluate(simulatedElapsed) : 0f;

                    float yellowCurveM = mpeManager.animCurveYellow.Evaluate(simulatedElapsed) / 1000f;
                    float swayM = (mpeManager.is6DOFMode && mpeManager.animCurveSway != null) ? (mpeManager.animCurveSway.Evaluate(simulatedElapsed) / 1000f) : 0f;
                    float surgeM = (mpeManager.is6DOFMode && mpeManager.animCurveSurge != null) ? (mpeManager.animCurveSurge.Evaluate(simulatedElapsed) / 1000f) : 0f;

                    // ✨ 修复离线导出时的基础高度 BUG (调用你在物理层写的几何算法)
                    float baseHeightM = platformModelManager.GetInitialHeightFromCylinderLength(Global.Instance.stroke);
                    float targetHeightM = baseHeightM + yellowCurveM;

                    // 组合目标旋转
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

                    // ✨ 组合包含横移、纵移和升降的终极空间坐标
                    Vector3 platformTranslation = new Vector3(swayM, targetHeightM, surgeM);

                    float[] currentFrameData = new float[axisCount];

                    for (int leg = 0; leg < axisCount; leg++)
                    {
                        Vector3 localTop = topVertices[leg];
                        // 旋转加上偏移，计算世界空间位置
                        Vector3 worldTop = (reqRot * localTop) + platformTranslation;

                        int baseIndex = leg;
                        if (mpeManager.is6DOFMode)
                        {
                            baseIndex = (leg + 1) % 6; // 6自由度交叉连线
                        }
                        Vector3 worldBase = baseVertices[baseIndex];

                        float rawDist = Vector3.Distance(worldTop, worldBase);

                        // 物理层面的安全软限位 (防顶缸)
                        float safeDist = Mathf.Clamp(rawDist, minSafeLength, maxSafeLength);

                        currentFrameData[leg] = Mathf.Clamp(((safeDist - Global.Instance.stroke) / Global.Instance.maxStroke) * 10f, 0f, 10f);
                    }

                    // 兼容旧数据的反向索引映射
                    float[] mappedData = new float[axisCount];
                    if (!mpeManager.is6DOFMode)
                    {
                        mappedData[0] = currentFrameData[2];
                        mappedData[1] = currentFrameData[1];
                        mappedData[2] = currentFrameData[0];
                    }
                    else
                    {
                        for (int j = 0; j < 6; j++) mappedData[j] = currentFrameData[j];
                    }

                    rawDataList.Add(mappedData);
                    simulatedElapsed += recordInterval;
                }

                progressBar.value = simulatedElapsed;
                yield return null;
            }

            // 执行曲线平滑拟合算法
            if (enableSmoothing)
            {
                Debug.Log("正在执行数据曲线高斯平滑降噪...");
                rawDataList = ApplyGaussianSmoothing(rawDataList, smoothWindowSize, smoothPasses, axisCount);
            }

            // ✨ 核心修改 4：将平滑后的数据动态写入 TXT (支持6列)
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
            Debug.Log($"极速无损导出(含平滑)成功！已保存至: {currentFilePath}");
            isRecording = false;
        }

        // =========================================================
        // ✨ 升级版：兼容多轴数组的曲线拟合与平滑算法
        // =========================================================
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