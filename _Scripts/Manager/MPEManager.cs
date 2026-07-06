/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：MPEManager.cs
 * 作者：LeonLiu (AI Assisted)
 * 日期：2026/2/15
 * 功能：动感平台初始化管理 (完善 3DOF/6DOF 独立量程输入控制)
*************************************************************************/

using DemoApplication;
using MPE;
using RenderHeads.Media.AVProVideo;
using RuntimeCurveEditor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MPE
{
    public class MPEManager : MonoBehaviour
    {
        private bool dofEditState = false;

        // --- 同步与防卡死锁 ---
        private bool isSyncingTime = false;
        private float lastSeekValue = -1f;

        public float timeLinePaddingLength = 3.0f;

        public MediaPlayer mediaPlayer;

        [Header("3DOF Curves")]
        public AnimationCurve animCurveRed;
        public AnimationCurve animCurveGreen;
        public AnimationCurve animCurveYellow;

        [Header("6DOF Additional Curves")]
        public AnimationCurve animCurveYaw;
        public AnimationCurve animCurveSway;
        public AnimationCurve animCurveSurge;

        public TimelineManager timelineManager;
        public RTAnimationCurve rtAnimationCurve;
        public PlatformModelManager DOFPlatform;
        public DataRecorder dataRecorder;

        [Header("System Mode")]
        public bool is6DOFMode = false;

        [Header("Selection UI Panels")]
        public GameObject platformModeSelectPanel;
        public GameObject initializationPanel3DOF;
        public GameObject initializationPanel6DOF;

        [Header("Hardware Calibration UI")]
        public Slider cylinderShiftSlider;
        public Toggle clockwiseToggle;
        public Slider platformYRotationSlider;

        [Header("3DOF Input Fields (3自由度专属面板)")]
        public TMP_InputField triangleAEdgesA;
        public TMP_InputField triangleAEdgesB;
        public TMP_InputField triangleAEdgesC;
        public TMP_InputField stroke3DOF;
        public TMP_InputField maxStroke3DOF;
        public TMP_InputField maxAngle;
        // ✨ 新增：3DOF 专属独立量程输入框
        public TMP_InputField dataScaleInput3DOF;

        [Header("6DOF Input Fields (6自由度专属面板)")]
        public TMP_InputField topLongEdgeInput;
        public TMP_InputField topShortEdgeInput;
        public TMP_InputField baseLongEdgeInput;
        public TMP_InputField baseShortEdgeInput;
        public TMP_InputField stroke6DOF;
        public TMP_InputField maxStroke6DOF;
        public TMP_InputField maxAngle6DOF;
        // ✨ 新增：6DOF 专属独立量程输入框
        public TMP_InputField dataScaleInput6DOF;

        [Header("UI Panels")]
        public GameObject DOFPlatformPanel;
        public GameObject TimeLineSlider;
        public GameObject FileManagerPanel;

        public TMP_Text playPauseButtonText;

        private void Start()
        {
 

            if (cylinderShiftSlider != null)
                cylinderShiftSlider.onValueChanged.AddListener(OnCylinderShiftChanged);

            if (clockwiseToggle != null)
                clockwiseToggle.onValueChanged.AddListener(OnClockwiseToggled);
            
            if (platformYRotationSlider != null)
                platformYRotationSlider.onValueChanged.AddListener(OnPlatformRotationChanged);

            LoadInitParameters();
            UpdateUIForPlatformMode();
        }



        private void SaveInitParameters()
        {
            if (triangleAEdgesA != null) PlayerPrefs.SetString("MPE_3DOF_EdgeA", triangleAEdgesA.text);
            if (triangleAEdgesB != null) PlayerPrefs.SetString("MPE_3DOF_EdgeB", triangleAEdgesB.text);
            if (triangleAEdgesC != null) PlayerPrefs.SetString("MPE_3DOF_EdgeC", triangleAEdgesC.text);
            if (stroke3DOF != null) PlayerPrefs.SetString("MPE_3DOF_Stroke", stroke3DOF.text);
            if (maxStroke3DOF != null) PlayerPrefs.SetString("MPE_3DOF_MaxStroke", maxStroke3DOF.text);
            if (maxAngle != null) PlayerPrefs.SetString("MPE_3DOF_MaxAngle", maxAngle.text);
            if (dataScaleInput3DOF != null) PlayerPrefs.SetString("MPE_3DOF_DataScale", dataScaleInput3DOF.text);

            if (topLongEdgeInput != null) PlayerPrefs.SetString("MPE_6DOF_TopLong", topLongEdgeInput.text);
            if (topShortEdgeInput != null) PlayerPrefs.SetString("MPE_6DOF_TopShort", topShortEdgeInput.text);
            if (baseLongEdgeInput != null) PlayerPrefs.SetString("MPE_6DOF_BaseLong", baseLongEdgeInput.text);
            if (baseShortEdgeInput != null) PlayerPrefs.SetString("MPE_6DOF_BaseShort", baseShortEdgeInput.text);
            if (stroke6DOF != null) PlayerPrefs.SetString("MPE_6DOF_Stroke", stroke6DOF.text);
            if (maxStroke6DOF != null) PlayerPrefs.SetString("MPE_6DOF_MaxStroke", maxStroke6DOF.text);
            if (maxAngle6DOF != null) PlayerPrefs.SetString("MPE_6DOF_MaxAngle", maxAngle6DOF.text);
            if (dataScaleInput6DOF != null) PlayerPrefs.SetString("MPE_6DOF_DataScale", dataScaleInput6DOF.text);

            if (cylinderShiftSlider != null) PlayerPrefs.SetFloat("MPE_CylShift", cylinderShiftSlider.value);
            if (clockwiseToggle != null) PlayerPrefs.SetInt("MPE_Clockwise", clockwiseToggle.isOn ? 1 : 0);
            if (platformYRotationSlider != null) PlayerPrefs.SetFloat("MPE_YRot", platformYRotationSlider.value);

            PlayerPrefs.SetInt("MPE_Is6DOFMode", is6DOFMode ? 1 : 0);

            PlayerPrefs.Save();
            Debug.Log("[参数持久化] 当前配置参数已成功保存。");
        }

        private void LoadInitParameters()
        {
            if (!PlayerPrefs.HasKey("MPE_Is6DOFMode")) return;

            if (triangleAEdgesA != null) triangleAEdgesA.text = PlayerPrefs.GetString("MPE_3DOF_EdgeA", triangleAEdgesA.text);
            if (triangleAEdgesB != null) triangleAEdgesB.text = PlayerPrefs.GetString("MPE_3DOF_EdgeB", triangleAEdgesB.text);
            if (triangleAEdgesC != null) triangleAEdgesC.text = PlayerPrefs.GetString("MPE_3DOF_EdgeC", triangleAEdgesC.text);
            if (stroke3DOF != null) stroke3DOF.text = PlayerPrefs.GetString("MPE_3DOF_Stroke", stroke3DOF.text);
            if (maxStroke3DOF != null) maxStroke3DOF.text = PlayerPrefs.GetString("MPE_3DOF_MaxStroke", maxStroke3DOF.text);
            if (maxAngle != null) maxAngle.text = PlayerPrefs.GetString("MPE_3DOF_MaxAngle", maxAngle.text);
            if (dataScaleInput3DOF != null) dataScaleInput3DOF.text = PlayerPrefs.GetString("MPE_3DOF_DataScale", dataScaleInput3DOF.text);

            if (topLongEdgeInput != null) topLongEdgeInput.text = PlayerPrefs.GetString("MPE_6DOF_TopLong", topLongEdgeInput.text);
            if (topShortEdgeInput != null) topShortEdgeInput.text = PlayerPrefs.GetString("MPE_6DOF_TopShort", topShortEdgeInput.text);
            if (baseLongEdgeInput != null) baseLongEdgeInput.text = PlayerPrefs.GetString("MPE_6DOF_BaseLong", baseLongEdgeInput.text);
            if (baseShortEdgeInput != null) baseShortEdgeInput.text = PlayerPrefs.GetString("MPE_6DOF_BaseShort", baseShortEdgeInput.text);
            if (stroke6DOF != null) stroke6DOF.text = PlayerPrefs.GetString("MPE_6DOF_Stroke", stroke6DOF.text);
            if (maxStroke6DOF != null) maxStroke6DOF.text = PlayerPrefs.GetString("MPE_6DOF_MaxStroke", maxStroke6DOF.text);
            if (maxAngle6DOF != null) maxAngle6DOF.text = PlayerPrefs.GetString("MPE_6DOF_MaxAngle", maxAngle6DOF.text);
            if (dataScaleInput6DOF != null) dataScaleInput6DOF.text = PlayerPrefs.GetString("MPE_6DOF_DataScale", dataScaleInput6DOF.text);

            if (cylinderShiftSlider != null) cylinderShiftSlider.value = PlayerPrefs.GetFloat("MPE_CylShift", cylinderShiftSlider.value);
            if (clockwiseToggle != null) clockwiseToggle.isOn = PlayerPrefs.GetInt("MPE_Clockwise", clockwiseToggle.isOn ? 1 : 0) == 1;
            if (platformYRotationSlider != null) platformYRotationSlider.value = PlayerPrefs.GetFloat("MPE_YRot", platformYRotationSlider.value);

            /*
            is6DOFMode = PlayerPrefs.GetInt("MPE_Is6DOFMode", is6DOFMode ? 1 : 0) == 1;
            if (is6DOFMode)
            {
                OnSelect6DOFMode();
            }
            else
            {
                OnSelect3DOFMode();
            }*/
        }

        // ✨ 核心重构：智能量程路由分发器
        public float GetDataScale()
        {
            // 根据当前激活的模式，自动决定选择哪一个输入框作为数据源
            TMP_InputField targetInput = is6DOFMode ? dataScaleInput6DOF : dataScaleInput3DOF;

            if (targetInput != null && float.TryParse(targetInput.text, out float scale))
            {
                return scale > 0 ? scale : 10f; // 防呆保护
            }
            return 10f; // 默认回退值
        }

        private void UpdateUIForPlatformMode()
        {
            if (cylinderShiftSlider != null)
            {
                cylinderShiftSlider.minValue = 0;
                cylinderShiftSlider.maxValue = is6DOFMode ? 5f : 2f;

                if (!is6DOFMode && cylinderShiftSlider.value > 2f)
                {
                    cylinderShiftSlider.value = 0f;
                    if (DOFPlatform != null) DOFPlatform.cylinderIndexShift = 0;
                }
            }

            if (clockwiseToggle != null)
            {
                clockwiseToggle.gameObject.SetActive(true);
            }
        }

        public void OnCylinderShiftChanged(float value)
        {
            if (DOFPlatform != null)
            {
                DOFPlatform.cylinderIndexShift = Mathf.RoundToInt(value);
            }
        }

        public void OnClockwiseToggled(bool isOn)
        {
            if (DOFPlatform != null)
            {
                DOFPlatform.isClockwise = isOn;
            }
        }

        public void OnPlatformRotationChanged(float value)
        {
            if (DOFPlatform != null)
            {
                DOFPlatform.transform.localRotation = Quaternion.Euler(0, value, 0);
            }
        }

        public void OnSelect3DOFMode()
        {
            is6DOFMode = false;
            UpdateUIForPlatformMode();
            if (initializationPanel3DOF != null) initializationPanel3DOF.SetActive(true);
            if (initializationPanel6DOF != null) initializationPanel6DOF.SetActive(false);
            if (platformModeSelectPanel != null) platformModeSelectPanel.SetActive(false);
        }

        public void OnSelect6DOFMode()
        {
            is6DOFMode = true;
            UpdateUIForPlatformMode();
            if (initializationPanel3DOF != null) initializationPanel3DOF.SetActive(false);
            if (initializationPanel6DOF != null) initializationPanel6DOF.SetActive(true);
            if (platformModeSelectPanel != null) platformModeSelectPanel.SetActive(false);
        }

        // ✨ 核心修改：将原来的同步初始化改为协程启动器，以便等待视频异步加载
        public void InitializationTime()
        {
            StartCoroutine(LoadVideoAndInitializeCoroutine());
        }

        private System.Collections.IEnumerator LoadVideoAndInitializeCoroutine()
        {
            SaveInitParameters();

            // 1. 根据当前选择的模式，动态拼接视频的绝对路径
            // System.Environment.CurrentDirectory 指向：编辑器下为项目根目录，打包后为 .exe 所在目录
            string videoSubPath = is6DOFMode ? "Video/6DOF/0.mp4" : "Video/3DOF/0.mp4";
            string videoPath = System.IO.Path.Combine(System.Environment.CurrentDirectory, videoSubPath);

            if (!System.IO.File.Exists(videoPath))
            {
                Debug.LogError($"[视频加载警告] 找不到视频文件，请确保路径存在: {videoPath}");
                // 如果没有找到视频文件，不中断后续操作，仅报红提示
            }
            else
            {
                if (mediaPlayer != null)
                {
                    // 2. 命令 AVProVideo 使用绝对路径加载视频，且不自动播放
                    mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, videoPath, false);

                    // 3. 阻塞等待视频加载就绪 (极其重要！否则 GetDuration 会返回 0，导致时间轴长度和曲线范围崩溃)
                    float timeout = 5f; // 加入 5 秒超时保护，防止视频格式损坏导致死循环卡死
                    float timer = 0f;
                    while ((mediaPlayer.Info == null || double.IsNaN(mediaPlayer.Info.GetDuration()) || mediaPlayer.Info.GetDuration() <= 0) && timer < timeout)
                    {
                        timer += Time.deltaTime;
                        yield return null; // 挂起当前逻辑，等待下一帧
                    }

                    if (timer >= timeout)
                    {
                        Debug.LogError("[视频加载超时] 无法解析视频时长，请检查视频格式或系统解码器！");
                    }
                }
            }

            // 4. 等待视频就绪后，执行原本所有的 UI 和参数初始化逻辑
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
                Global.Instance.maxAngle = float.Parse(maxAngle6DOF.text);
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

            // 如果之前合并了播放/暂停按钮，这里初始化一下其 UI 状态为“播放”
            UpdatePlayPauseButtonUI(false);
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

        // ✨ 核心重构：规范曲线颜色与平台控制姿态的绝对统一
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
            float angleLimit = is6DOFMode ? float.Parse(maxAngle6DOF.text) : float.Parse(maxAngle.text);
            string currentMaxStrokeText = is6DOFMode ? maxStroke6DOF.text : maxStroke3DOF.text;
            float heaveLimit = float.Parse(currentMaxStrokeText);

            // =========================================================
            // 第一部分：3DOF 和 6DOF 共享的核心三轴（红、绿、黄）
            // =========================================================

            // 1. 🔴 红线：永远代表 Pitch（俯仰角）
            if (rtAnimationCurve.Add(ref animCurveRed))
            {
                rtAnimationCurve.SetGradYRange(-angleLimit, angleLimit);
                rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
            }

            // 2. 🟢 绿线：永远代表 Roll（滚转角）
            if (rtAnimationCurve.Add(ref animCurveGreen))
            {
                rtAnimationCurve.SetGradYRange(-angleLimit, angleLimit);
                rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
            }

            // 3. 🟡 黄线：永远代表 Heave（垂直升降行程 mm）
            if (rtAnimationCurve.Add(ref animCurveYellow))
            {
                rtAnimationCurve.SetGradYRange(0f, heaveLimit);
                rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
            }

            // =========================================================
            // 第二部分：仅 6DOF 独有的扩展三轴（蓝、紫、棕）
            // =========================================================
            if (is6DOFMode)
            {
                // 4. 🔵 蓝线（原Yaw）：代表 Yaw（偏航角）
                if (rtAnimationCurve.Add(ref animCurveYaw))
                {
                    rtAnimationCurve.SetGradYRange(-angleLimit, angleLimit);
                    rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
                }

                float displacementLimit = float.Parse(maxStroke6DOF.text);

                // 5. 🟣 紫线（原Sway）：代表 Sway（水平横移 mm）
                if (rtAnimationCurve.Add(ref animCurveSway))
                {
                    rtAnimationCurve.SetGradYRange(-displacementLimit, displacementLimit);
                    rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
                }

                // 6. 🟤 棕线（原Surge）：代表 Surge（水平纵移 mm）
                if (rtAnimationCurve.Add(ref animCurveSurge))
                {
                    rtAnimationCurve.SetGradYRange(-displacementLimit, displacementLimit);
                    rtAnimationCurve.SetGradXRange(0, rtAnimationCurve.gradXRangeMax);
                }
            }
        }

        private void SetDOFPlatform()
        {
            UpdateUIForPlatformMode();

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

                    if (cylinderShiftSlider != null) DOFPlatform.cylinderIndexShift = Mathf.RoundToInt(cylinderShiftSlider.value);
                    if (clockwiseToggle != null) DOFPlatform.isClockwise = clockwiseToggle.isOn;
                    if (platformYRotationSlider != null) DOFPlatform.transform.localRotation = Quaternion.Euler(0, platformYRotationSlider.value, 0);

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

                    if (cylinderShiftSlider != null) DOFPlatform.cylinderIndexShift = Mathf.RoundToInt(cylinderShiftSlider.value);
                    if (platformYRotationSlider != null) DOFPlatform.transform.localRotation = Quaternion.Euler(0, platformYRotationSlider.value, 0);

                    DOFPlatform.Cleanup();
                    DOFPlatform.InitializeSystem();

                    float strokeM = float.Parse(stroke3DOF.text) / 1000f;
                    DOFPlatform.height = strokeM;

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
        /*
        public void PlayVideo()
        {
            if (mediaPlayer != null && mediaPlayer.Control != null) mediaPlayer.Control.Play();
        }

        public void PauseVideo()
        {
            if (mediaPlayer != null && mediaPlayer.Control != null) mediaPlayer.Control.Pause();
        }
        */

        public void TogglePlayPause()
        {
            if (mediaPlayer != null && mediaPlayer.Control != null)
            {
                if (mediaPlayer.Control.IsPlaying())
                {
                    mediaPlayer.Control.Pause();
                    UpdatePlayPauseButtonUI(false);
                }
                else
                {
                    mediaPlayer.Control.Play();
                    UpdatePlayPauseButtonUI(true);
                }
            }
        }

        public void UpdatePlayPauseButtonUI(bool isPlaying)
        {
            if(playPauseButtonText!=null)
            {
                playPauseButtonText.text = isPlaying ? "暂停" : "播放";
            }
        }

        private void Update()
        {
            // =================================================================
            // 1. 视频播放进度与时间轴 UI 针头同步
            // =================================================================
            if (mediaPlayer != null && mediaPlayer.Control != null && mediaPlayer.Control.IsPlaying())
            {
                if (!Input.GetMouseButton(0))
                {
                    isSyncingTime = true;
                    timelineManager._slider.value = (float)mediaPlayer.Control.GetCurrentTime();
                    isSyncingTime = false;
                }
            }

            // =================================================================
            // 2. 动感平台实时姿态解算驱动
            // =================================================================
            if (dofEditState)
            {
                // 从规范好的曲线上，获取纯粹的物理目标值
                float pitchVal = animCurveRed.Evaluate(timelineManager._slider.value);   // 🔴 俯仰角 (Pitch)
                float rollVal = animCurveGreen.Evaluate(timelineManager._slider.value); // 🟢 滚转角 (Roll)
                float yawVal = is6DOFMode ? animCurveYaw.Evaluate(timelineManager._slider.value) : 0f; // 🔵 偏航角 (Yaw)

                Quaternion targetRot;

                if (is6DOFMode)
                {
                    // ✨ 6DOF 模型空间特性（车头朝向+X）：本地X轴控制滚转，本地Z轴控制俯仰
                    targetRot = Quaternion.Euler(rollVal, yawVal, pitchVal);
                }
                else
                {
                    // ✨ 3DOF 模型空间特性（车头朝向+Z）：本地X轴控制俯仰，本地Z轴控制滚转 (标准Unity规范)
                    targetRot = Quaternion.Euler(pitchVal, yawVal, rollVal);
                }

                DOFPlatform.triangleA.transform.localRotation = targetRot;

                // --- 基础缸长与高度解算 ---
                string currentStrokeText = is6DOFMode ? stroke6DOF.text : stroke3DOF.text;
                float baseCylinderM = float.Parse(currentStrokeText) / 1000f;

                float baseHeightM = DOFPlatform.GetInitialHeightFromCylinderLength(baseCylinderM);
                float yellowCurveM = animCurveYellow.Evaluate(timelineManager._slider.value) / 1000f; // 🟡 升降行程 (Heave)
                float targetCylinderLengthM = baseCylinderM + yellowCurveM;
                float targetHeightM = DOFPlatform.GetHeightFromCylinderLength(targetCylinderLengthM);

                // --- 平台倾斜旋转带来的网格中心点高度动态补偿 ---
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

                // --- 水平位移解算 (Sway 横移 & Surge 纵移) ---
                float targetSwayM = is6DOFMode ? (animCurveSway.Evaluate(timelineManager._slider.value) / 1000f) : 0f;  // 🟣 Sway
                float targetSurgeM = is6DOFMode ? (animCurveSurge.Evaluate(timelineManager._slider.value) / 1000f) : 0f; // 🟤 Surge

                DOFPlatform.height = targetHeightM;

                if (is6DOFMode)
                {
                    // ✨ 避坑彩蛋：因为 6DOF 模型转了90度（朝+X），
                    // 它的“向前纵移Surge”对应Unity世界的 X轴，“向右横移Sway”对应 Z轴！
                    DOFPlatform.triangleA.transform.localPosition = new Vector3(targetSurgeM, targetHeightM, targetSwayM);
                }
                else
                {
                    // ✨ 3DOF 标准空间（朝+Z）：X为横移Sway，Z为纵移Surge（3DOF下这两个值全为0，安全兜底）
                    DOFPlatform.triangleA.transform.localPosition = new Vector3(targetSwayM, targetHeightM, targetSurgeM);
                }
            }
        }
    }
}