/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：MPEManager.cs
 * 作者：LeonLiu (AI Assisted)
 * 日期：2026/2/5 
 * 功能：动感平台初始化管理 (新增3DOF/6DOF模式初始选择面板控制)
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

        [Header("3DOF Curves")]
        public AnimationCurve animCurveRed;    // Pitch 俯仰
        public AnimationCurve animCurveGreen;  // Roll  滚转
        public AnimationCurve animCurveYellow; // Heave 升降

        [Header("6DOF Additional Curves")]
        public AnimationCurve animCurveYaw;    // Yaw   偏航 (角度)
        public AnimationCurve animCurveSway;   // Sway  横移 (毫米)
        public AnimationCurve animCurveSurge;  // Surge 纵移 (毫米)

        public TimelineManager timelineManager;//时间轴 
        public RTAnimationCurve rtAnimationCurve;//曲线
        public PlatformModelManager DOFPlatform;//3自由度/6自由度平台
        public DataRecorder dataRecorder;//数据导出

        [Header("System Mode")]
        public bool is6DOFMode = false;

        [Header("✨ New Selection UI Panels (新增选择面板控制)")]
        [Tooltip("新建的包含【选择3DOF】和【选择6DOF】两个按钮的父物体面板")]
        public GameObject platformModeSelectPanel;
        [Tooltip("3自由度专属参数初始化面板 (包含A/B/C边长输入框等)")]
        public GameObject initializationPanel3DOF;
        [Tooltip("6自由度专属参数初始化面板 (包含动静长短边输入框等)")]
        public GameObject initializationPanel6DOF;

        [Header("3DOF Input Fields (3自由度专属)")]
        public TMP_InputField triangleAEdgesA;
        public TMP_InputField triangleAEdgesB;
        public TMP_InputField triangleAEdgesC;
        public TMP_InputField stroke3DOF;
        public TMP_InputField maxStroke3DOF;
        public TMP_InputField maxAngle;

        [Header("6DOF Input Fields (6自由度专属)")]
        public TMP_InputField topLongEdgeInput;
        public TMP_InputField topShortEdgeInput;
        public TMP_InputField baseLongEdgeInput;
        public TMP_InputField baseShortEdgeInput;
        public TMP_InputField stroke6DOF;
        public TMP_InputField maxStroke6DOF;

        [Header("UI Panels")]
        public GameObject DOFPlatformPanel;
        public GameObject TimeLineSlider;
        public GameObject FileManagerPanel;

        // =========================================================
        // ✨ 新增：模式选择按钮绑定的公有方法
        // =========================================================

        /// <summary>
        /// 当点击【选择3DOF】按钮时绑定此方法
        /// </summary>
        public void OnSelect3DOFMode()
        {
            is6DOFMode = false;

            // 切换初始化面板的显隐
            if (initializationPanel3DOF != null) initializationPanel3DOF.SetActive(true);
            if (initializationPanel6DOF != null) initializationPanel6DOF.SetActive(false);

            // 关闭选择按钮自身的面板
            if (platformModeSelectPanel != null) platformModeSelectPanel.SetActive(false);

            Debug.Log("系统已切换至：3自由度(3DOF) 参数配置模式。");
        }

        /// <summary>
        /// 当点击【选择6DOF】按钮时绑定此方法
        /// </summary>
        public void OnSelect6DOFMode()
        {
            is6DOFMode = true;

            // 切换初始化面板的显隐
            if (initializationPanel3DOF != null) initializationPanel3DOF.SetActive(false);
            if (initializationPanel6DOF != null) initializationPanel6DOF.SetActive(true);

            // 关闭选择按钮自身的面板
            if (platformModeSelectPanel != null) platformModeSelectPanel.SetActive(false);

            Debug.Log("系统已切换至：6自由度(6DOF) 参数配置模式。");
        }

        // =========================================================

        public void InitializationTime()
        {
            SetTimeLineLength();
            SetCurveLenget();
            SetDOFPlatform();
            ShowCurveEditor();
            DOFPlatformPanel.SetActive(false);
            FileManagerPanel.SetActive(true);

            if (is6DOFMode)
            {
                Global.Instance.stroke = float.Parse(stroke6DOF.text) / 1000f;
                Global.Instance.maxStroke = float.Parse(maxStroke6DOF.text) / 1000f;
                Global.Instance.maxAngle = 90f;
            }
            else
            {
                Global.Instance.stroke = float.Parse(stroke3DOF.text) / 1000f;
                Global.Instance.maxStroke = float.Parse(maxStroke3DOF.text) / 1000f;
                Global.Instance.maxAngle = float.Parse(maxAngle.text);
            }

            dataRecorder.Initialization();

            timelineManager._slider.onValueChanged.RemoveListener(OnTimelineScrubbed);
            timelineManager._slider.onValueChanged.AddListener(OnTimelineScrubbed);
        }

        private void OnTimelineScrubbed(float value)
        {
            if (isSyncingTime) return;

            if (mediaPlayer != null && mediaPlayer.Control != null)
            {
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
            float angleLimit = is6DOFMode ? 45f : float.Parse(maxAngle.text);

            if (rtAnimationCurve.Add(ref animCurveRed))
            {
                rtAnimationCurve.SetGradYRange(-angleLimit, angleLimit);
                rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
            }

            if (rtAnimationCurve.Add(ref animCurveGreen))
            {
                rtAnimationCurve.SetGradYRange(-angleLimit, angleLimit);
                rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
            }

            if (rtAnimationCurve.Add(ref animCurveYellow))
            {
                string currentMaxStrokeText = is6DOFMode ? maxStroke6DOF.text : maxStroke3DOF.text;
                rtAnimationCurve.SetGradYRange(0f, float.Parse(currentMaxStrokeText));
                rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
            }

            if (is6DOFMode)
            {
                if (rtAnimationCurve.Add(ref animCurveYaw))
                {
                    rtAnimationCurve.SetGradYRange(-angleLimit, angleLimit);
                    rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
                }

                float displacementLimit = float.Parse(maxStroke6DOF.text);

                if (rtAnimationCurve.Add(ref animCurveSway))
                {
                    rtAnimationCurve.SetGradYRange(-displacementLimit, displacementLimit);
                    rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
                }

                if (rtAnimationCurve.Add(ref animCurveSurge))
                {
                    rtAnimationCurve.SetGradYRange(-displacementLimit, displacementLimit);
                    rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
                }
            }
        }

        private void SetDOFPlatform()
        {
            if (is6DOFMode)
            {
                if (topLongEdgeInput.text != "" && topShortEdgeInput.text != "" &&
                    baseLongEdgeInput.text != "" && baseShortEdgeInput.text != "" && stroke6DOF.text != "")
                {
                    DOFPlatform.currentPlatformType = PlatformModelManager.PlatformType.DOF6;

                    DOFPlatform.topLongEdge = float.Parse(topLongEdgeInput.text);
                    DOFPlatform.topShortEdge = float.Parse(topShortEdgeInput.text);
                    DOFPlatform.baseLongEdge = float.Parse(baseLongEdgeInput.text);
                    DOFPlatform.baseShortEdge = float.Parse(baseShortEdgeInput.text);

                    DOFPlatform.Cleanup();
                    DOFPlatform.InitializeSystem();

                    float strokeM = float.Parse(stroke6DOF.text) / 1000f;
                    DOFPlatform.height = DOFPlatform.GetInitialHeightFromCylinderLength(strokeM);

                    if (DOFPlatform.triangleA != null)
                    {
                        DOFPlatform.triangleA.transform.localPosition = new Vector3(0, DOFPlatform.height, 0);
                    }

                    dofEditState = true;
                }
            }
            else
            {
                if (triangleAEdgesA.text != "" && triangleAEdgesB.text != "" && triangleAEdgesC.text != "" && stroke3DOF.text != "")
                {
                    DOFPlatform.currentPlatformType = PlatformModelManager.PlatformType.DOF3;

                    DOFPlatform.a = float.Parse(triangleAEdgesA.text) / 1000f;
                    DOFPlatform.b = float.Parse(triangleAEdgesB.text) / 1000f;
                    DOFPlatform.c = float.Parse(triangleAEdgesC.text) / 1000f;

                    float strokeM = float.Parse(stroke3DOF.text) / 1000f;
                    DOFPlatform.height = strokeM;

                    DOFPlatform.Cleanup();
                    DOFPlatform.InitializeSystem();

                    dofEditState = true;
                }
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

        public void PlayVideo()
        {
            if (mediaPlayer != null && mediaPlayer.Control != null)
            {
                mediaPlayer.Control.Play();
            }
        }

        public void PauseVideo()
        {
            if (mediaPlayer != null && mediaPlayer.Control != null)
            {
                mediaPlayer.Control.Pause();
            }
        }

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
            if (mediaPlayer != null && mediaPlayer.Control != null && mediaPlayer.Control.IsPlaying())
            {
                if (!Input.GetMouseButton(0))
                {
                    isSyncingTime = true;
                    timelineManager._slider.value = (float)mediaPlayer.Control.GetCurrentTime();
                    isSyncingTime = false;
                }
            }

            if (dofEditState)
            {
                float targetPitch = animCurveRed.Evaluate(timelineManager._slider.value);
                float targetRoll = animCurveGreen.Evaluate(timelineManager._slider.value);
                float targetYaw = is6DOFMode ? animCurveYaw.Evaluate(timelineManager._slider.value) : 0f;

                Quaternion targetRot = Quaternion.Euler(targetPitch, targetYaw, targetRoll);
                DOFPlatform.triangleA.transform.localRotation = targetRot;

                string currentStrokeText = is6DOFMode ? stroke6DOF.text : stroke3DOF.text;
                float baseCylinderM = float.Parse(currentStrokeText) / 1000f;

                float baseHeightM = DOFPlatform.GetInitialHeightFromCylinderLength(baseCylinderM);
                float yellowCurveM = animCurveYellow.Evaluate(timelineManager._slider.value) / 1000f;
                float targetHeightM = baseHeightM + yellowCurveM;

                MeshFilter mf = DOFPlatform.triangleA.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    float maxLocalY = -9999f;
                    float minLocalY = 9999f;

                    foreach (Vector3 vertex in mf.sharedMesh.vertices)
                    {
                        Vector3 rotatedVertex = targetRot * vertex;
                        if (rotatedVertex.y > maxLocalY) maxLocalY = rotatedVertex.y;
                        if (rotatedVertex.y < minLocalY) minLocalY = rotatedVertex.y;
                    }

                    float localCenterY = (maxLocalY + minLocalY) / 2f;
                    targetHeightM -= localCenterY;
                }

                float targetSwayM = is6DOFMode ? (animCurveSway.Evaluate(timelineManager._slider.value) / 1000f) : 0f;
                float targetSurgeM = is6DOFMode ? (animCurveSurge.Evaluate(timelineManager._slider.value) / 1000f) : 0f;

                DOFPlatform.height = targetHeightM;
                DOFPlatform.triangleA.transform.localPosition = new Vector3(targetSwayM, targetHeightM, targetSurgeM);
            }
        }
    }
}