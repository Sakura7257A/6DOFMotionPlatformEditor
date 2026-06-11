/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：MPEManager.cs
 * 作者：LeonLiu
 * 日期：2026/2/5 16:59:24
 * 功能：动感平台初始化管理
*************************************************************************/

using DemoApplication;
using MPE;
using RenderHeads.Media.AVProVideo;
using RuntimeCurveEditor;

using TMPro;
using UnityEngine;


namespace MPE
{
    public class MPEManager : MonoBehaviour
    {
        private bool dofEditState = false;

        // --- 同步与防卡死锁 ---
        private bool isSyncingTime = false;
        private float lastSeekValue = -1f;

        public float timeLinePaddingLength = 3.0f;

        public MediaPlayer mediaPlayer;//影片

        public AnimationCurve animCurveRed;//animation curve that we can edit at run time
        public AnimationCurve animCurveGreen;
        public AnimationCurve animCurveYellow;

        public TimelineManager timelineManager;//时间轴 
        public RTAnimationCurve rtAnimationCurve;//曲线
        public PlatformModelManager DOFPlatform;//3自由度平台
        public DataRecorder dataRecorder;//数据导出

        public TMP_InputField triangleAEdgesA;
        public TMP_InputField triangleAEdgesB;
        public TMP_InputField triangleAEdgesC;
        public TMP_InputField stroke;
        public TMP_InputField maxAngle;
        public TMP_InputField maxStroke;

        public GameObject DOFPlatformPanel;
        public GameObject TimeLineSlider;
        public GameObject FileManagerPanel;

        public void InitializationTime()
        {
            SetTimeLineLength();
            SetCurveLenget();
            SetDOFPlatform();
            ShowCurveEditor();
            DOFPlatformPanel.SetActive(false);
            FileManagerPanel.SetActive(true);

            // 将UI输入的毫米(mm)转换为系统标准的米(m)
            Global.Instance.stroke = float.Parse(stroke.text) / 1000f;
            Global.Instance.maxStroke = float.Parse(maxStroke.text) / 1000f;
            Global.Instance.maxAngle = float.Parse(maxAngle.text); // 角度单位保持不变

            dataRecorder.Initialization();

            // 注册时间轴拖拽事件以同步视频
            timelineManager._slider.onValueChanged.RemoveListener(OnTimelineScrubbed);
            timelineManager._slider.onValueChanged.AddListener(OnTimelineScrubbed);
        }

        // === 处理用户手动拖拽时间轴 ===
        private void OnTimelineScrubbed(float value)
        {
            if (isSyncingTime) return; // 如果是播放器自己推着走，就忽略

            if (mediaPlayer != null && mediaPlayer.Control != null)
            {
                // 防频繁刷新卡死机制：只有时间变动足够大，或者鼠标松开时才 Seek
                if (Mathf.Abs(lastSeekValue - value) > 0.05f || !Input.GetMouseButton(0))
                {
                    mediaPlayer.Control.Seek(value);
                    lastSeekValue = value;
                }
            }
        }

        public void ShowCurveEditor()
        {
            if (rtAnimationCurve.IsCurveEditorClosed())
            {
                rtAnimationCurve.ShowCurveEditor();
            }
            AddCurve();
        }

        public void AddCurve()
        {
            if (rtAnimationCurve.Add(ref animCurveRed))
            {
                rtAnimationCurve.SetGradYRange(-float.Parse(maxAngle.text), float.Parse(maxAngle.text));
                rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
            }

            if (rtAnimationCurve.Add(ref animCurveGreen))
            {
                rtAnimationCurve.SetGradYRange(-float.Parse(maxAngle.text), float.Parse(maxAngle.text));
                rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
            }

            if (rtAnimationCurve.Add(ref animCurveYellow))
            {
                // 核心修改：让曲线编辑器的 Y 轴范围直接使用毫米数值，方便操作人员看图和编辑
                rtAnimationCurve.SetGradYRange(0f, float.Parse(maxStroke.text));
                rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
            }
        }

        private void SetDOFPlatform()
        {
            if (triangleAEdgesA.text != "" && triangleAEdgesB.text != "" && triangleAEdgesC.text != "" && stroke.text != "")
            {
                // 将输入的边长(毫米)转化为米
                DOFPlatform.a = float.Parse(triangleAEdgesA.text) / 1000f;
                DOFPlatform.b = float.Parse(triangleAEdgesB.text) / 1000f;
                DOFPlatform.c = float.Parse(triangleAEdgesC.text) / 1000f;

                float strokeM = float.Parse(stroke.text) / 1000f;
                float maxStrokeM = float.Parse(maxStroke.text) / 1000f;

                DOFPlatform.height = strokeM;


                DOFPlatform.Cleanup();
                DOFPlatform.InitializeSystem();

                dofEditState = true;
            }
        }

        private void SetTimeLineLength()
        {
            TimeLineSlider.SetActive(true);
            double videoLength = mediaPlayer.Info.GetDuration();
            timelineManager.totalSeconds = (float)videoLength + timeLinePaddingLength;
            timelineManager.InitializationTimeLine();
        }

        private void SetCurveLenget()
        {
            double videoLength = mediaPlayer.Info.GetDuration();
            float curveLength = (float)videoLength + timeLinePaddingLength;
            rtAnimationCurve.gradXRangeMax = curveLength;
            rtAnimationCurve.SetGradXRange(0, curveLength);
        }

        public void DataExport()
        {
            dataRecorder.StartProcess();
        }

        /// <summary>
        /// 供 UI 按钮调用的：播放视频
        /// </summary>
        public void PlayVideo()
        {
            if (mediaPlayer != null && mediaPlayer.Control != null)
            {
                mediaPlayer.Control.Play();
            }
        }

        /// <summary>
        /// 供 UI 按钮调用的：暂停视频
        /// </summary>
        public void PauseVideo()
        {
            if (mediaPlayer != null && mediaPlayer.Control != null)
            {
                mediaPlayer.Control.Pause();
            }
        }

        /// <summary>
        /// 供 UI 按钮调用的：切换 播放/暂停 状态（绑定一个按钮即可）
        /// </summary>
        public void TogglePlayPause()
        {
            if (mediaPlayer != null && mediaPlayer.Control != null)
            {
                if (mediaPlayer.Control.IsPlaying())
                {
                    mediaPlayer.Control.Pause();
                }
                else
                {
                    mediaPlayer.Control.Play();
                }
            }
        }

        private void Update()
        {
            // 1. 同步逻辑：如果视频正在播放，将视频进度赋予 Slider
            if (mediaPlayer != null && mediaPlayer.Control != null && mediaPlayer.Control.IsPlaying())
            {
                // 只要检测到用户按住了鼠标左键（可能正在拖拽定位），就立刻暂停把视频进度覆盖给 Slider
                if (!Input.GetMouseButton(0))
                {
                    isSyncingTime = true;
                    timelineManager._slider.value = (float)mediaPlayer.Control.GetCurrentTime();
                    isSyncingTime = false;
                }
            }

            if (dofEditState)
            {
                // 1. 获取目标角度并应用旋转
                float targetPitch = animCurveRed.Evaluate(timelineManager._slider.value);
                float targetRoll = animCurveGreen.Evaluate(timelineManager._slider.value);
                Quaternion targetRot = Quaternion.Euler(targetPitch, 0, targetRoll);
                DOFPlatform.triangleA.transform.localRotation = targetRot;

                // 2. 获取基础目标高度 (毫米转米)
                float baseStrokeM = float.Parse(stroke.text) / 1000f;
                float yellowCurveM = animCurveYellow.Evaluate(timelineManager._slider.value) / 1000f;
                float targetHeightM = baseStrokeM + yellowCurveM;

                // 3. ✨ 核心修改：模拟 Unity "Center" 模式的完美随动算法
                MeshFilter mf = DOFPlatform.triangleA.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    float maxLocalY = -9999f;
                    float minLocalY = 9999f;

                    // 遍历顶部的三个顶点，找到由于倾斜产生的最高点和最低点
                    foreach (Vector3 vertex in mf.sharedMesh.vertices)
                    {
                        Vector3 rotatedVertex = targetRot * vertex;
                        if (rotatedVertex.y > maxLocalY) maxLocalY = rotatedVertex.y;
                        if (rotatedVertex.y < minLocalY) minLocalY = rotatedVertex.y;
                    }

                    // Center 模式的核心定义：Y轴中心点 = (最高点 + 最低点) / 2
                    float localCenterY = (maxLocalY + minLocalY) / 2f;

                    // 扣除这个偏移量，强制让平台的包围盒中心(Center)稳稳停留在黄线设定的高度上
                    targetHeightM -= localCenterY;
                }

                // 4. 应用最终的随动高度
                DOFPlatform.height = targetHeightM;
                DOFPlatform.triangleA.transform.localPosition = new Vector3(0, targetHeightM, 0);
            }
        }
    }
}