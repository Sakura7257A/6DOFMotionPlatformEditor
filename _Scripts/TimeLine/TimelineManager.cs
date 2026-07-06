/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：TimelineManager.cs
 * 作者：LeonLiu
 * 日期：2026/2/4 (修正版)
 * 功能：时间轴滑动控制与刻度尺统一管理
*************************************************************************/

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MPE
{
    public class TimelineManager : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        [Header("--- 时间与显示设置 ---")]
        public float totalSeconds = 120f;
        public TMP_Text timeDisplayText;

        [Header("--- 刻度尺设置 (Ruler) ---")]
        public int secondsPerTick = 1;
        [Tooltip("当前刻度间隔/每秒的像素宽度")]
        public float tickInterval = 20f;
        public int majorTickStep = 10;
        public GameObject tickPrefab;
        public GameObject labelPrefab;
        public RectTransform rulerContentRect;

        [Header("--- 缩放设置 (Zoom) ---")]
        public bool enableZoom = true;
        public float minInterval = 5f;
        public float maxInterval = 100f;
        public float zoomSpeed = 5f;

        [Header("--- 拖拽设置 (Drag) ---")]
        public bool enableDrag = true;

        // --- 内部组件缓存 ---
        public Slider _slider;
        private RectTransform _sliderRect;
        private Canvas _canvas;

        private List<RectTransform> tickRects = new List<RectTransform>();
        private List<RectTransform> labelRects = new List<RectTransform>();
        private List<GameObject> allSpawnedObjects = new List<GameObject>();
        private int totalTicks;

        void Awake()
        {
            _sliderRect = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.rootCanvas != null) _canvas = _canvas.rootCanvas;
            _slider = GetComponent<Slider>();
        }

        public void InitializationTimeLine()
        {
            // 核心修复 1：强制将 Anchor 和 Pivot 都统一设置为左边缘对齐。
            // 这样一来，X坐标为 0 就代表完美贴紧父物体的最左侧。
            if (_sliderRect != null)
            {
                _sliderRect.anchorMin = new Vector2(0f, 0.5f);
                _sliderRect.anchorMax = new Vector2(0f, 0.5f);
                _sliderRect.pivot = new Vector2(0f, 0.5f);
            }
            if (rulerContentRect != null)
            {
                rulerContentRect.anchorMin = new Vector2(0f, 0.5f);
                rulerContentRect.anchorMax = new Vector2(0f, 0.5f);
                rulerContentRect.pivot = new Vector2(0f, 0.5f);
            }

            RefreshSliderSettings();
            GenerateRuler();
        }

        public void RefreshSliderSettings()
        {
            if (_slider == null) return;

            // === 新增：彻底禁用 Slider 的自带键盘导航，防止方向键误拖动时间轴 ===
            Navigation nav = _slider.navigation;
            nav.mode = Navigation.Mode.None;
            _slider.navigation = nav;

            _slider.minValue = 0;
            _slider.maxValue = totalSeconds;

            // 核心修复 2：初始归位直接设置为 0，不再计算负数偏移。
            Vector2 resetPos = _sliderRect.anchoredPosition;
            resetPos.x = 0f;
            _sliderRect.anchoredPosition = resetPos;

            if (rulerContentRect != null)
            {
                Vector2 rulerPos = rulerContentRect.anchoredPosition;
                rulerPos.x = 0f;
                rulerContentRect.anchoredPosition = rulerPos;
            }

            _slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            _slider.onValueChanged.AddListener(OnSliderValueChanged);
            OnSliderValueChanged(_slider.value);
        }

        public void OnSliderValueChanged(float value)
        {
            if (timeDisplayText != null)
            {
                int minutes = Mathf.FloorToInt(value / 60F);
                int seconds = Mathf.FloorToInt(value % 60F);

                // ✨ 新增：提取毫秒部分 (取1的余数得到小数部分，再乘以1000)
                int milliseconds = Mathf.FloorToInt((value % 1F) * 1000F);

                // ✨ 修改：将格式改为 分:秒:毫秒 (例如 01:23:450)
                timeDisplayText.text = string.Format("{0:00}:{1:00}:{2:000}", minutes, seconds, milliseconds);
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!enableZoom) return;

            float scrollInput = eventData.scrollDelta.y;
            if (Mathf.Abs(scrollInput) > 0)
            {
                float newInterval = tickInterval + (scrollInput * zoomSpeed);
                newInterval = Mathf.Clamp(newInterval, minInterval, maxInterval);

                if (Mathf.Abs(newInterval - tickInterval) > 0.001f)
                {
                    tickInterval = newInterval;
                    UpdateLayout();
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (!enableDrag) return;

            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
                float deltaX = eventData.delta.x / scaleFactor;

                Vector2 newPos = _sliderRect.anchoredPosition;
                newPos.x += deltaX;
                _sliderRect.anchoredPosition = newPos;

                ClampPosition();
            }
        }

        // ================== 核心限制逻辑 (彻底解决错位和溢出) ==================
        private void ClampPosition()
        {
            if (_sliderRect == null) return;

            float finalWidth = _sliderRect.rect.width;

            // 核心修复 3：自动获取父级容器的实际宽度，不再依赖手动填写的 ViewWidth
            float parentViewWidth = ((RectTransform)_sliderRect.parent).rect.width;

            // 因为对齐点在最左侧(0)，所以 maxX 永远是 0（绝不允许超过左侧边界）
            float maxX = 0f;
            float minX = 0f;

            if (finalWidth > parentViewWidth)
            {
                // 如果总宽度大于屏幕，最多只能向左拖拽 (宽度的差值)
                minX = parentViewWidth - finalWidth;
            }

            Vector2 pos = _sliderRect.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            _sliderRect.anchoredPosition = pos;

            // 同步刻度尺位置
            if (rulerContentRect != null)
            {
                Vector2 rulerPos = rulerContentRect.anchoredPosition;
                rulerPos.x = pos.x;
                rulerContentRect.anchoredPosition = rulerPos;
            }
        }

        [ContextMenu("生成刻度尺")]
        public void GenerateRuler()
        {
            ClearOldObjects();

            if (tickPrefab == null || rulerContentRect == null) return;

            totalTicks = Mathf.CeilToInt(totalSeconds / secondsPerTick);

            tickRects = new List<RectTransform>(totalTicks + 1);
            labelRects = new List<RectTransform>((totalTicks / majorTickStep) + 1);

            for (int i = 0; i <= totalTicks; i++)
            {
                GameObject tick = Instantiate(tickPrefab, rulerContentRect);
                RectTransform tickRt = tick.GetComponent<RectTransform>();

                bool isMajor = (i % majorTickStep == 0);
                if (isMajor)
                {
                    tickRt.sizeDelta = new Vector2(2, 60);

                    if (labelPrefab != null)
                    {
                        GameObject label = Instantiate(labelPrefab, rulerContentRect);
                        RectTransform labelRt = label.GetComponent<RectTransform>();

                        int currentTimeInSeconds = i * secondsPerTick;
                        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
                        if (tmp != null) tmp.text = FormatTime(currentTimeInSeconds);
                        else
                        {
                            Text leg = label.GetComponent<Text>();
                            if (leg) leg.text = FormatTime(currentTimeInSeconds);
                        }

                        allSpawnedObjects.Add(label);
                        labelRects.Add(labelRt);
                    }
                }
                else
                {
                    tickRt.sizeDelta = new Vector2(1, 30);
                }

                allSpawnedObjects.Add(tick);
                tickRects.Add(tickRt);
            }

            UpdateLayout();
        }

        private void UpdateLayout()
        {
            for (int i = 0; i < tickRects.Count; i++)
            {
                if (tickRects[i] != null)
                    tickRects[i].anchoredPosition = new Vector2((i * tickInterval) + 1, 0);
            }

            for (int i = 0; i < labelRects.Count; i++)
            {
                if (labelRects[i] != null)
                {
                    int tickIndex = i * majorTickStep;
                    labelRects[i].anchoredPosition = new Vector2(tickIndex * tickInterval, 40);
                }
            }

            float totalWidth = totalSeconds * (tickInterval / (float)secondsPerTick);

            if (rulerContentRect != null)
            {
                rulerContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
            }

            if (_sliderRect != null)
            {
                _sliderRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
            }

            // 尺寸变化后，重新计算一下边界约束
            ClampPosition();
        }

        string FormatTime(int totalSec)
        {
            int minutes = totalSec / 60;
            int seconds = totalSec % 60;
            return string.Format("{0:D2}:{1:D2}", minutes, seconds);
        }

        void ClearOldObjects()
        {
            foreach (var obj in allSpawnedObjects)
            {
                if (obj != null)
                {
                    if (Application.isPlaying) Destroy(obj);
                    else DestroyImmediate(obj);
                }
            }
            allSpawnedObjects.Clear();
            tickRects.Clear();
            labelRects.Clear();
        }

        public void SetTotalTime(float seconds)
        {
            totalSeconds = seconds;
            RefreshSliderSettings();
            GenerateRuler();
        }
    }
}