//----------------------------------------------
// Runtime Curve Editor
// Copyright © 2013-2024 Rus Artur PFA
// center@republicofhandball.com
//----------------------------------------------

using DemoApplication;
using MPE;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace RuntimeCurveEditor
{
    /// <summary>
    /// Curve editor's window,draws the window itself and gradations.
    /// </summary>
    public class CurveWindow : MonoBehaviour
    {
        public CurveLines curveLines;//keep a reference to the component drawing the curves

        public Texture2D close;
        public Texture2D textureNS;
        public Texture2D textureWE;
        public Texture2D textureNWSE;
        public Texture2D textureSWNE;
        public Texture2D textureDefault;
        public RectTransform panel;

        public RectTransform headerPanel;

        public RectTransform horGradations;
        public RectTransform verGradations;

        public GameObject number;

        public RectTransform yMaxRect;

        public MPEManager mPEManager;

        public Slider timeLineSlider;

        // 1. 新增变量：引用文字标签
        public TMP_Text timeCursorLabel;

        public RectTransform timeCursor; // 红色竖线的 Image
        public float currentTargetX = 0f; // 记录当前代表的 X 轴数值（逻辑数值）


        InputField yMaxEditField;

        RectTransform canvas;

        enum ResizeType { No, ResizeNS, ResizeWE, ResizeNWSE, ResizeSWNE };
        bool onLeftEdge;
        bool onRightEdge;
        bool onTopEdge;
        bool onBottomEdge;
        ResizeType mResize = ResizeType.No;

        int screenWidth;
        int screenHeight;

        public static Vector2 hotspot = new Vector2(8, 8);//The offset from the top left of the texture to use as the target point (must be within the bounds of the cursor).

        Vector2 mPrevCursorPos;//(screen coordinates) 

        Rect closeRect;//keeps the rect for the close button

        Rect gridRect;

        public bool IsTouchedBegan() {
            if (Input.touchCount == 0) return false;
            return Input.touches[0].phase == TouchPhase.Began;
        }
        public bool IsDoubleTap() {
            if (Input.touchCount != 1) return false;
            return Input.touches[0].tapCount == 2;
        }
        public bool IsSingleTap() {
            if (Input.touchCount != 1) return false;
            return Input.touches[0].tapCount == 1;
        }

        public bool WindowClosed { set; get; }

        public float horNumberMaxWidth;
        float verNumberMaxHeight;
        float verNumberMaxWidth;

        int colCount;
        int rowCount;

        float panelHeaderPixels;

        float panelBottomPixels;

        float xLeft;
        float xRight;
        float yTop;
        float yBottom;

        Vector2 ratioScreenCanvas;
        Vector2 invRatioScreenCanvas;

        bool checkBottomNumber;

        const float MIDDLE = 0.5f;

        const string DESTROYED = "destroyed";

        public static int MESH_LAYER = 30;//RuntimeCurveEditor layer 
        const string LAYER_NAME = "RuntimeCurveEditor";

        private void OnApplicationQuit() {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
        }

        void Start() {



            timeLineSlider = GameObject.Find("TimeLineSlider").GetComponent<Slider>();
            timeLineSlider.onValueChanged.AddListener(OnSliderChanged);


            mPEManager = GameObject.Find("ScriptsManager").GetComponent<MPEManager>();

            string layerName = LayerMask.LayerToName(MESH_LAYER);
            if (layerName == "")
            {
                Debug.LogWarning("Layer " + MESH_LAYER + " has no name. It should be named " + LAYER_NAME + " like in documentation!");
            }
            else if(layerName != LAYER_NAME)
            {
                Debug.LogWarning("Layer " + MESH_LAYER + " is named " + layerName + " and it might be clashing with the layer used for Runtime Curve Editor. Check the documentation!");
            }

            textureDefault = null;//remove this line, if you want to use a custom icon for default cursor
            Cursor.SetCursor(textureDefault, Vector2.zero, CursorMode.ForceSoftware);

            canvas = panel.parent.GetComponent<RectTransform>();

            curveLines.InitConstantValues();
                        
            curveLines.TextureNS = textureNS;
            curveLines.TextureDefault = textureDefault;

            horNumberMaxWidth = GetNumberMaxWidthPixels(curveLines.GradRect.xMin, curveLines.GradRect.xMax);//TODO this has to be called, if somehow xMin or xMax are changed at some moment
            curveLines.WidthUnit = horNumberMaxWidth;
            verNumberMaxHeight = number.GetComponent<TMP_Text>().fontSize * 2f;

            verNumberMaxWidth = GetNumberMaxWidthPixels(0, curveLines.GradRect.yMax);

            Sprite bkgSprite = GetComponent<Image>().sprite;
            panelHeaderPixels = bkgSprite.border.w;
            panelBottomPixels = bkgSprite.border.y;

            UpdateScreenWindowGrid();

            headerPanel.GetComponent<WindowDragging>().curveWindow = this;//TODO to remove once moving CurveWindow component on CurveEditorPanel
            headerPanel.sizeDelta = new Vector2(panel.sizeDelta.x, headerPanel.sizeDelta.y);

            curveLines.enabled = true;

            yMaxEditField = yMaxRect.GetComponent<InputField>();
            yMaxEditField.text = curveLines.GradRect.yMax.ToString();

            if (Screen.dpi > CurveLines.DEFAULT_DPI) {
                curveLines.HeightUnit = verNumberMaxHeight * ((Screen.dpi / CurveLines.DEFAULT_DPI - 1) * 0.35f + 1f);
            } else {
                curveLines.HeightUnit = verNumberMaxHeight;
            }
        }

        void Update()
        {
            if ((Screen.width != screenWidth) || (Screen.height != screenHeight))
            {
                UpdateScreenWindowGrid();
            }

            //CheckShowWindowResizeCursor();

            // 获取按键状态
            bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            Vector3 mousePos = CursorPos();

            // 计算时间刻度尺区域 (网格下方)
            Rect timeRulerRect = new Rect(gridRect.xMin, yBottom, gridRect.width, gridRect.yMin - yBottom);

            bool isScrubbing = false;

            // === 1. 新增：红线定位逻辑 (必须按住 Ctrl 键) ===
            if (Input.GetMouseButton(0) && isCtrlPressed)
            {
                // 鼠标在网格内或下方刻度尺内，只要按着 Ctrl 就可以任意拖拽定位时间
                if (timeRulerRect.Contains(mousePos) || gridRect.Contains(mousePos))
                {
                    isScrubbing = true;
                }
            }

            if (isScrubbing)
            {
                // 计算比例并映射为逻辑数值
                float t = (mousePos.x - curveLines.EntireRect.x) / curveLines.EntireRect.width;
                float targetX = Mathf.Lerp(curveLines.GradRect.xMin, curveLines.GradRect.xMax, t);
                targetX = Mathf.Clamp(targetX, curveLines.GradRect.xMin, curveLines.GradRect.xMax);

                // 驱动 Slider 更新
                if (timeLineSlider != null)
                {
                    timeLineSlider.value = targetX;
                }

                // 更新底层记录，防止松开 Ctrl 时触发原有的错误拖拽
                if (Input.GetMouseButtonDown(0))
                {
                    mPrevCursorPos = CursorPos();
                }
            }
            else
            {
                // === 2. 原版的曲线编辑逻辑 (添加、拖拽、框选等) ===
                if (Input.GetMouseButtonDown(0))
                {
                    MouseDownOnUpdate();
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    MouseUpOnUpdate();
                }
                else if (Input.GetMouseButton(0))
                {
                    MouseDragOnUpdate();
                }
            }
        }

        public void ResetScreenSize(bool reload = false) {
            screenWidth = 0;
            screenHeight = 0;
            if (!reload) { 
                GetComponent<ZoomBehaviour>().Reset();
                curveLines.zoomRatioX = 0;
                curveLines.zoomRatioY = 0;
            }
            curveLines.zoomDefault.gameObject.SetActive(false);

            curveLines.undo.gameObject.SetActive(false);
            curveLines.redo.gameObject.SetActive(false);
        }

        void UpdateScreenWindowGrid() {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            ratioScreenCanvas.x = screenWidth / canvas.rect.width;
            ratioScreenCanvas.y = screenHeight / canvas.rect.height;
            invRatioScreenCanvas.x = 1f / ratioScreenCanvas.x;
            invRatioScreenCanvas.y = 1f / ratioScreenCanvas.y;
            UpdateWindowAndGrid();
        }

        void UpdateWindowAndGrid() {
            UpdateWindowSizeValues();
            UpdateGrid();

            UpdateTimeCursorPosition();
        }

        void UpdateWindowSizeValues() {
            Vector2 size = panel.sizeDelta;
            size.x *= ratioScreenCanvas.x;
            size.y *= ratioScreenCanvas.y;
            xLeft = panel.localPosition.x * ratioScreenCanvas.x - size.x * 0.5f + screenWidth * 0.5f;
            xRight = xLeft + size.x;
            yBottom = panel.localPosition.y * ratioScreenCanvas.y - size.y * 0.5f + screenHeight * 0.5f;
            yTop = yBottom + size.y;
        }

        /// <summary>
        /// Update the grid size
        /// </param>
        void UpdateGrid() {

            //旧代码
            //gridRect.x = panel.localPosition.x - 0.5f * panel.sizeDelta.x + 3f * horNumberMaxWidth;

            // 将左侧边距倍数从 3f 减小到 1.5f（数值越小，网格越往左靠）
            gridRect.x = panel.localPosition.x - 0.5f * panel.sizeDelta.x + 1.5f * horNumberMaxWidth;

            //旧代码
            //gridRect.width = panel.sizeDelta.x - 4f * horNumberMaxWidth;

            // 相应的，宽度扣除量从 4f 减小到 2.5f (原先是左3+右1=4，现在是左1.5+右1=2.5)
            gridRect.width = panel.sizeDelta.x - 2.5f * horNumberMaxWidth;

            gridRect.y = panel.localPosition.y + GetGridLocalPos();
            gridRect.height = panel.sizeDelta.y - panelBottomPixels - panelHeaderPixels - 2f * verNumberMaxHeight;

            gridRect.x *= ratioScreenCanvas.x;
            gridRect.width *= ratioScreenCanvas.x;
            gridRect.y *= ratioScreenCanvas.y;
            gridRect.height *= ratioScreenCanvas.y;

            Vector2 anchor = (panel.anchorMin + panel.anchorMax) * 0.5f;
            gridRect.x += screenWidth * anchor.x;
            gridRect.y += screenHeight * anchor.y;

            curveLines.UpdateGrid(gridRect);

            float yMin = (panel.localPosition.y - 0.5f * panel.sizeDelta.y) * ratioScreenCanvas.y + screenHeight * anchor.y;
            curveLines.BottomShapesYmin = yMin + panelBottomPixels * ratioScreenCanvas.y * 0.2f;
            curveLines.BottomShapesYmax = yMin + panelBottomPixels * ratioScreenCanvas.y * 0.8f;
        }

        public Vector3 CursorPos() {
            return Input.mousePosition;
        }

        public void OnClose() {
            curveLines.CloseWindow();
        }

        public bool NormalCursorType() {
            return mResize == ResizeType.No;
        }

        void MouseDownOnUpdate() {
            mPrevCursorPos = CursorPos();
        }

        void MouseUpOnUpdate() {
            if (mResize != ResizeType.No) {
                return;
            }

            Vector2 mousePosUp = CursorPos();
            if (closeRect.Contains(mousePosUp)) {
                WindowClosed = true;
                curveLines.WindowClosed = true;
                curveLines.AlterData();
            } else {
                curveLines.MouseUp();
            }
        }

        void MouseDragOnUpdate() {
            Vector2 newCursorPos = CursorPos();
            if (closeRect.Contains(newCursorPos) || (newCursorPos.x == mPrevCursorPos.x && newCursorPos.y == mPrevCursorPos.y)) {
                return;
            }

            if (mResize == ResizeType.No) {
                newCursorPos.x = Mathf.Clamp(newCursorPos.x, gridRect.xMin, gridRect.xMax);
                newCursorPos.y = Mathf.Clamp(newCursorPos.y, gridRect.yMin, gridRect.yMax);
                curveLines.MouseDrag(newCursorPos - mPrevCursorPos);
            } else {
                Vector2 cursorDiff = newCursorPos - mPrevCursorPos;
                cursorDiff.x *= invRatioScreenCanvas.x;
                cursorDiff.y *= invRatioScreenCanvas.y;
                if (ResizeType.ResizeNS == mResize) {
                    panel.sizeDelta += new Vector2(0, RevY(cursorDiff.y));
                    panel.Translate(0, ScaleY(cursorDiff.y) * 0.5f, 0);
                } else if (ResizeType.ResizeWE == mResize) {
                    panel.sizeDelta += new Vector2(RevX(cursorDiff.x), 0);
                    panel.Translate(ScaleX(cursorDiff.x) * 0.5f, 0, 0);
                } else if (ResizeType.ResizeNWSE == mResize || ResizeType.ResizeSWNE == mResize) {
                    panel.sizeDelta += new Vector2(RevX(cursorDiff.x), RevY(cursorDiff.y));
                    panel.Translate(ScaleX(cursorDiff.x) * 0.5f, ScaleY(cursorDiff.y) * 0.5f, 0);
                }
                headerPanel.sizeDelta = new Vector2(panel.sizeDelta.x, headerPanel.sizeDelta.y);

                UpdateWindowAndGrid();
                curveLines.AlterData();
            }
            mPrevCursorPos = newCursorPos;
        }

        float RevX(float diffX) {
            return onLeftEdge ? -diffX : diffX;
        }

        float RevY(float diffY) {
            return onBottomEdge ? -diffY : diffY;
        }

        float ScaleX(float diffX) {
            return diffX * canvas.localScale.x;
        }

        float ScaleY(float diffY) {
            return diffY * canvas.localScale.y;
        }

        float GetNumberMaxWidthPixels(float val1, float val2) {

            int length1 = val1.ToString().Length;
            int length2 = val2.ToString().Length;
            float maxDigits = (length1 >= length2) ? length1 : length2;

            maxDigits += 2;//considering that in between values are no longer than 2 digits(including '.')

            //float numberWidth = number.GetComponent<RectTransform>().sizeDelta.x;//TODO this should be constant, and so should be calculated only once
            return number.GetComponent<TMP_Text>().fontSize * 0.6f * maxDigits;//TODO this should be constant, and so should be calculated only once
        }

        public void OnYmaxUpdate() {
            float value;
            if (float.TryParse(yMaxEditField.text, out value)) {
                curveLines.SetGradRectYMax(value, true);
            }
        }

        void CreateNumber(float value, float anchorY) {
            RectTransform numberRect = Instantiate(number).GetComponent<RectTransform>();
            numberRect.SetParent(verGradations);
            numberRect.SetAsFirstSibling();

            float numberValue = (float)Math.Round(value, 3);

            //numberRect.GetComponent<Text>().text = numberValue.ToString();
            // --- 修改为：智能判断整数 ---
            // 如果 value 和它的整数部分差距极小，就认为是整数
            if (Mathf.Abs(value - Mathf.Round(value)) < 0.001f)
            {
                // 显示为整数 (例如 "5")
                numberRect.GetComponent<TMP_Text>().text = Mathf.Round(value).ToString("0");
            }
            else
            {
                // 保持原样，或者限制小数位数 (例如 "5.1")
                numberRect.GetComponent<TMP_Text>().text = value.ToString("0.##");
            }
            //numberRect.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            //numberRect.GetComponent<TMP_Text>().alignment = TextAlignment.Right;

            numberRect.localPosition = Vector3.zero;
            numberRect.anchorMin = new Vector2(0.5f, anchorY);
            numberRect.anchorMax = numberRect.anchorMin;

            numberRect.sizeDelta = new Vector2(verNumberMaxWidth, numberRect.sizeDelta.y);

            numberRect.localScale = Vector3.one;
            numberRect.gameObject.name = numberValue.ToString();
        }

        public void UpdatePosition(Vector2 cursorDiff) {
            cursorDiff.x *= invRatioScreenCanvas.x;
            cursorDiff.y *= invRatioScreenCanvas.y;
            panel.Translate(ScaleX(cursorDiff.x), ScaleY(cursorDiff.y), 0);
            UpdateWindowAndGrid();
            curveLines.AlterData();
        }

        float GetGridLocalPos() {
            return panelBottomPixels + 1.25f * verNumberMaxHeight - panel.sizeDelta.y * 0.5f;
        }

        public void UpdateVerGradations(int rowCount, float rezid, bool mirrored, bool gradUpdate) {
            if (gradUpdate) {
                yMaxEditField.text = curveLines.GradRect.yMax.ToString();
                verNumberMaxWidth = GetNumberMaxWidthPixels(0, curveLines.GradRect.yMax);
            }
            verGradations.sizeDelta = new Vector2(1.5f * verNumberMaxWidth, curveLines.EntireRect.height * invRatioScreenCanvas.y);
            float displ = (curveLines.EntireRect.yMin - gridRect.yMin) * invRatioScreenCanvas.y;
           //verGradations.localPosition = new Vector2(yMaxRect.sizeDelta.x * 0.75f - panel.sizeDelta.x * 0.5f, GetGridLocalPos() + verGradations.sizeDelta.y * 0.5f + displ);
           // --- 核心修复：彻底解决运行多次后刻度乱跑的问题 ---
            // 1. 获取网格(Grid)的左边缘在 Panel 中的局部坐标 (对应 UpdateGrid 中的设置，保持与 1.5f 同步)
            float gridLeftLocalX = -0.5f * panel.sizeDelta.x + 1.5f * horNumberMaxWidth;
            
            // 2. 精确计算 Y轴容器 的位置，让它内部的数字文本框右边缘，永远距离网格左边缘 8 个像素的间隙
            float spacing = 8f; 
            float textRightEdgeOffset = 0.5f * verNumberMaxWidth; // 因为文本框是居中的，这是它右边缘相对于中心的偏移
            float targetContainerX = gridLeftLocalX - spacing - textRightEdgeOffset;
            
            // 3. 应用动态计算的 X 坐标，让数字永远追随网格
            verGradations.localPosition = new Vector2(targetContainerX, GetGridLocalPos() + verGradations.sizeDelta.y * 0.5f + displ);
            //

            if ((this.rowCount != rowCount) || gradUpdate) {
                this.rowCount = rowCount;

                float normalizedGap = (1f - rezid) / rowCount;
                float gap = curveLines.GradRect.yMax * normalizedGap;
                float normalizedAnchorsGap = normalizedGap * (mirrored ? MIDDLE : 1f);
                foreach (RectTransform numberRect in verGradations) {
                    if (numberRect != yMaxRect) {
                        numberRect.gameObject.name = "DESTROYED";
                        Destroy(numberRect.gameObject);//the destroy takes place at the end of the current frame, not instantly
                    }
                }

                checkBottomNumber = false;
                float anchorStart = mirrored ? MIDDLE : 0f;
                int start = mirrored ? -rowCount : 0;
                if (!Mathf.Approximately(rezid, 0)) {
                    if (mirrored) {
                        CreateNumber(-curveLines.GradRect.yMax, 0);
                        checkBottomNumber = true;
                    }
                } else {
                    rowCount -= 1;
                }

                for (int i = start; i <= rowCount; ++i) {
                    CreateNumber(i * gap, anchorStart + i * normalizedAnchorsGap);
                }
            }

            foreach (RectTransform numberRect in verGradations) {
                if (numberRect.gameObject.name != DESTROYED)
                {
                    if (numberRect == yMaxRect) {
                        numberRect.localPosition = new Vector2(numberRect.localPosition.x, (gridRect.yMax - Screen.height * 0.5f) * invRatioScreenCanvas.y - verGradations.localPosition.y - panel.localPosition.y);
                    } else {
                        float numberPosY = (numberRect.localPosition.y + verGradations.localPosition.y + panel.localPosition.y) * ratioScreenCanvas.y + Screen.height * 0.5f;
                        numberRect.gameObject.SetActive((Mathf.Round(gridRect.yMin) <= Mathf.Round(numberPosY)) && (Mathf.Round(numberPosY) <= Mathf.Round(gridRect.yMax)));
                    }
                }
            }

            if (checkBottomNumber) {//decide if bottom number (not the mirroring of the grad max) should be visible or not, if it's too close to the 'the mirroring of the grad max'                         
                //verGradations
                Transform bottomNumber = verGradations.GetChild(2 * rowCount);
                if (bottomNumber.gameObject.activeSelf) {//if it's hidden already, do nothing
                    float normalizedGap = (1f - rezid) / rowCount;
                    float normalizedAnchorsGap = normalizedGap * MIDDLE;
                    bottomNumber.gameObject.SetActive((MIDDLE - rowCount * normalizedAnchorsGap) * verGradations.sizeDelta.y > verNumberMaxHeight * 0.35f);
                }
            }
        }

        public void UpdateHorGradations(int timeStep)
        {
            horGradations.sizeDelta = new Vector2(curveLines.EntireRect.width * invRatioScreenCanvas.x, horGradations.sizeDelta.y);
            Vector3 posTemp = horGradations.localPosition;

            // 保持之前修正过的居中对齐逻辑
            posTemp.x = 0.25f * horNumberMaxWidth + (curveLines.EntireRect.xMin - gridRect.xMin + curveLines.EntireRect.xMax - gridRect.xMax) * 0.5f * invRatioScreenCanvas.x;
            horGradations.localPosition = posTemp;

            float totalTime = curveLines.GradRect.xMax - curveLines.GradRect.xMin;
            if (totalTime <= 0) return;

            // 基于整数步长计算实际需要的刻度数量
            int tickCount = Mathf.CeilToInt(totalTime / timeStep);

            // 只有当时间步长发生变化（跨越缩放层级）时，才重新生成文字对象，提高性能
            if (this.colCount != timeStep)
            {
                this.colCount = timeStep;

                foreach (RectTransform numberRect in horGradations)
                {
                    Destroy(numberRect.gameObject);
                }

                for (int i = 0; i <= tickCount; ++i)
                {
                    RectTransform numberRect = (Instantiate(number) as GameObject).GetComponent<RectTransform>();
                    numberRect.SetParent(horGradations);

                    // 确保生成的文字数值绝对是整数秒
                    float numberValue = curveLines.GradRect.xMin + i * timeStep;
                    numberRect.GetComponent<TMP_Text>().text = numberValue.ToString("0");
                   // numberRect.GetComponent<TMP_Text>().alignment = TextAnchor.MiddleCenter;

                    numberRect.localPosition = Vector3.zero;

                    float normalizedPos = (numberValue - curveLines.GradRect.xMin) / totalTime;
                    numberRect.anchorMin = new Vector2(normalizedPos, 0.5f);
                    numberRect.anchorMax = numberRect.anchorMin;

                    numberRect.sizeDelta = new Vector2(horNumberMaxWidth, numberRect.sizeDelta.y);
                    numberRect.localScale = Vector3.one;
                    numberRect.gameObject.name = numberValue.ToString();
                }
            }

            foreach (RectTransform numberRect in horGradations)
            {
                float numberPosX = (numberRect.localPosition.x + horGradations.localPosition.x + panel.localPosition.x) * ratioScreenCanvas.x + Screen.width * 0.5f;
                numberRect.gameObject.SetActive((Mathf.Round(gridRect.xMin) <= Mathf.Round(numberPosX)) && (Mathf.Round(numberPosX) <= Mathf.Round(gridRect.xMax)));
            }
        }



        public Rect GetGridRect() {
            return gridRect;
        }

        public void Zoom(float factor, Vector2 mousePos) {
            curveLines.Zoom(factor, mousePos);

            UpdateTimeCursorPosition();
        }

        public void Pan(Vector2 diff) {
            curveLines.Pan(diff);
        }

        //this method gets called each update cycle
        void CheckShowWindowResizeCursor() {           

            //show the cursor if it's over any of the window's edges
            bool touch = Input.touchCount == 1;//in case of touch screen(mobile), the resize cursor could be visible only when the touch is actually very close to edge(there's no hovering feature)

            if (touch && (Input.touches[0].phase == TouchPhase.Ended)) {
                DisableResizeCursor();
            }

            if (!(touch && (Input.touches[0].phase == TouchPhase.Began))) {
                if (Input.GetMouseButton(0)) {
                    return;
                } else if (curveLines.MousePosOverContextMenu(CursorPos())) {
                    DisableResizeCursor();
                    return;
                }
            }

            Vector3 cursorPos = CursorPos();
            onLeftEdge = Mathf.Abs(xLeft - cursorPos.x) < CurveLines.marginPixels;
            onRightEdge = Mathf.Abs(xRight - cursorPos.x) < CurveLines.marginPixels;
            onTopEdge = Mathf.Abs(yTop - cursorPos.y) < CurveLines.marginPixels;
            onBottomEdge = Mathf.Abs(yBottom - cursorPos.y) < CurveLines.marginPixels;
            if ((cursorPos.x < xLeft - CurveLines.marginPixels) || (cursorPos.x > xRight + CurveLines.marginPixels) || (cursorPos.y < yBottom - CurveLines.marginPixels) || (cursorPos.y > yTop + CurveLines.marginPixels) ||
                (!onLeftEdge && !onRightEdge && !onTopEdge && !onBottomEdge)) {
                DisableResizeCursor();
            } else if ((onLeftEdge && onTopEdge) || (onRightEdge && onBottomEdge)) {
                mResize = ResizeType.ResizeNWSE;
                Cursor.SetCursor(textureNWSE, hotspot, CursorMode.ForceSoftware);
            } else if ((onLeftEdge && onBottomEdge) || (onRightEdge && onTopEdge)) {
                mResize = ResizeType.ResizeSWNE;
                Cursor.SetCursor(textureSWNE, hotspot, CursorMode.ForceSoftware);
            } else if (onLeftEdge || onRightEdge) {
                mResize = ResizeType.ResizeWE;
                Cursor.SetCursor(textureWE, hotspot, CursorMode.ForceSoftware);
            } else if (onTopEdge || onBottomEdge) {
                mResize = ResizeType.ResizeNS;
                Cursor.SetCursor(textureNS, hotspot, CursorMode.ForceSoftware);
            }
        }

        void DisableResizeCursor() {
            if (mResize != ResizeType.No) {
                Cursor.SetCursor(textureDefault, Vector2.zero, CursorMode.ForceSoftware);
                mResize = ResizeType.No;
            }
        }




        // Slider 的 MaxValue 应该设置为你曲线的最大 X 范围（比如 0 到 100）
        public void OnSliderChanged(float val)
        {
            currentTargetX = val;
            UpdateTimeCursorPosition();
        }

        public void UpdateTimeCursorPosition()
        {
            // 判空保护
            if (timeCursor == null || curveLines == null) return;

            // 1. 获取 Slider 代表的逻辑数值 (比如 Time = 5.0)
            // 假设你已经在 CurveWindow 定义了 private float currentTargetX;
            float targetX = currentTargetX;

            // 2. 获取当前数据的逻辑范围 (GradRect: Time 0 -> 10)
            float xMinVal = curveLines.GradRect.xMin;
            float xMaxVal = curveLines.GradRect.xMax;

            // 3. 计算目标值在逻辑范围内的比例 t (0.0 -> 1.0)
            float t = Mathf.InverseLerp(xMinVal, xMaxVal, targetX);

            // 4. 获取当前绘制区域的屏幕坐标范围 (EntireRect 受缩放/平移影响)
            Rect viewRect = curveLines.EntireRect;

            // 5. 计算红线在屏幕空间的 X 坐标
            // EntireRect.xMin 是网格左边缘的屏幕坐标，width 是屏幕像素宽度
            float xPosScreen = viewRect.x + (viewRect.width * t);

            // 6. 将屏幕坐标转换为 Panel 的局部坐标 (Canvas Space)
            // 公式：(屏幕坐标 - 屏幕中心) * 缩放比例 - 父物体偏移
            float xPosLocal = (xPosScreen - screenWidth * 0.5f) * invRatioScreenCanvas.x - panel.localPosition.x;

            // 7. 应用位置
            // 注意：Y轴位置通常固定在网格区域内
            float gridCenterY = GetGridLocalPos() + gridRect.height * invRatioScreenCanvas.y * 0.5f;
            timeCursor.localPosition = new Vector2(xPosLocal, gridCenterY);

            // 8. 设置红线高度和显隐
            timeCursor.sizeDelta = new Vector2(timeCursor.sizeDelta.x, gridRect.height * invRatioScreenCanvas.y);

            // 9. 裁剪逻辑：只有当红线在网格可视范围内时才显示
            // 这里使用 CurveLines.marginPixels 替代原来的 marginPixels
            // gridRect 是 CurveWindow 里定义的当前窗口显示区域（Canvas单位）
            float localGridLeft = (gridRect.xMin - screenWidth * 0.5f) * invRatioScreenCanvas.x - panel.localPosition.x;
            float localGridRight = (gridRect.xMax - screenWidth * 0.5f) * invRatioScreenCanvas.x - panel.localPosition.x;

            // 允许一点点误差 (marginPixels)
            float margin = CurveLines.marginPixels * invRatioScreenCanvas.x;

            bool isVisible = (xPosLocal >= localGridLeft - margin) && (xPosLocal <= localGridRight + margin);
            timeCursor.gameObject.SetActive(isVisible);

            // ----------------------------------------------------
            // 3. 新增逻辑：更新文字内容
            // ----------------------------------------------------
            if (timeCursorLabel != null)
            {
                // "F2" 表示保留两位小数，如 "5.23"
                // 如果你想要整数，改成 "F0"
                timeCursorLabel.text = targetX.ToString("F2");
            }
        }

        public void UpdateTimeCursorPosition(float diff)
        {
            if (timeCursor == null) return;

            // 1. 获取当前网格在数值上的最小值和最大值
            float xMinVal = curveLines.GradRect.xMin;
            float xMaxVal = curveLines.GradRect.xMax;

            // 2. 计算当前目标值在整个视图中的百分比位置 (0 to 1)
            // 使用 Mathf.InverseLerp 自动处理超出范围的情况
            float horizontalPercent = Mathf.InverseLerp(xMinVal, xMaxVal, currentTargetX);

            // 3. 将百分比转换为网格内的像素位置
            // gridRect.width 是屏幕像素，需要转回 Canvas 局部单位
            float gridWidthLocal = gridRect.width * invRatioScreenCanvas.x;
            float gridLeftLocal = (gridRect.xMin - screenWidth * 0.5f) * invRatioScreenCanvas.x - panel.localPosition.x;

            float xPos = gridLeftLocal + (gridWidthLocal * horizontalPercent) + diff;

            // 4. 更新 UI 位置与高度
            timeCursor.localPosition = new Vector2(xPos, GetGridLocalPos() + gridRect.height * invRatioScreenCanvas.y * 0.5f);
            timeCursor.sizeDelta = new Vector2(timeCursor.sizeDelta.x, gridRect.height * invRatioScreenCanvas.y);

            // 5. 如果数值超出了当前显示范围，可以隐藏红线
            timeCursor.gameObject.SetActive(horizontalPercent >= 0 && horizontalPercent <= 1);
        }
    }
}