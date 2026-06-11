//----------------------------------------------
// Runtime Curve Editor
// Copyright © 2013-2024 Rus Artur PFA
// center@republicofhandball.com
//----------------------------------------------
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;

namespace RuntimeCurveEditor
{

    /// <summary>
    /// Draws lines inside curve editor and manage user interaction with curves and keys
    /// </summary>
    public class CurveLines : MonoBehaviour, InterfaceContextMenuListener, InterfaceKeyEditListener//, InterfacePostRenderer
    {
        public Material lineMaterial;//material used for drawing lines
        public RenderParams RenderParams {  get; private set; }

        public CurveWindow curveWindow;

        public Texture2D TextureDefault { set; private get; }

        public float BottomShapesYmin { set; private get; }
        public float BottomShapesYmax { set; private get; }

        //unit in world coordinates(constant in pixels), used for calculating the number of lines that should be visible in the grid
        public float WidthUnit {  get; set; }
        public float HeightUnit { private get; set; }

        public static float DEFAULT_DPI = 96f;

        public KeyHandling KeyHandling { get; private set; }

        public List<CurveForm> curveFormList = new List<CurveForm>();

        //possible colors when drawing more curves 
        Color[] colors = { Color.red, Color.green, Color.yellow, Color.blue, Color.magenta, Color.cyan };

        //list with the colors currently used
        List<Color> usedColorList = new List<Color>();

        //grid rect in world coordinates
        public Rect GridRect { get; private set; }
        Rect adjGridRect;

        public Rect EntireRect { get; private set; }//on zooming GridRect remains unchanged, but EntireRect keeps its size multiplied by the factor of zooming

        public float zoomRatioX;//it has meaning only to restore zooming state
        public float zoomRatioY;//it has meaning only to restore zooming state

        float prevEntireRectXMin;
        float prevEntireRectYMin;
        float prevEntireRectHeight;
        float prevEntireRectWidth;

        //rezidual value (normalized), used when calculating the number of horyzontal lines to be displayed
        float mRezid;

        int mHorLines = 2;//have to know how many lines are displayed for the current size of the grid (actually lines+1 will be total number of displayed lines)

        static Color lineColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);

        //Rect given by the gradations limits
        Rect gradRect;
        Rect prevGradRect;
        public Rect GradRect {
            get {
                return gradRect;
            }
            set {
                prevGradRect = gradRect;
                gradRect = value;
            }
        }

        // keeps the form with the active curve (it shouldn't ever be null)
        public CurveForm ActiveCurveForm { get; private set; }

        bool lineDragged;//line touched/pressed, for moving that line
        bool keyDragged;//key touched/pressed, for moving that key
        int selectedKeyIndex = KeyHandling.UNSELECTED;//the index of the selected key, UNSELECTED if none is selected (if keySelected is true, than this is the key whose's moved by the user)
        bool isTangentSelected;//true if the user's now selecting a tangent
        bool leftTangentSelected;//if true the left tangent is selected, else the right tangent is selected(this is used only when tangentSelected is true)
        bool multipleKeysMove;


        // 新增：用于记录按住 Ctrl 拖拽时的轴向锁定状态
        enum AxisLock { None, Horizontal, Vertical }
        AxisLock currentAxisLock = AxisLock.None;


        ResizePart multipleKeysResize;
        Vector2 selectedKeyStartingPos;//case of single key movement(pos in time/value space)
        List<float> lineDragKeysStartingValue;//case of curve movement
        List<Vector2> selectedKeysStartingPos;//case of more keys movement(pos in time/value space)

        public Texture2D TextureNS { private get; set; }//the cursor used when draging the whole line(curve)

        public GameObject contextMenuKeyObject;
        public GameObject keyValueObject;

        RectTransform contextMenuKey;

        KeyValue keyValue;

        bool mMidHor;//particular use when choosing how dense the grid horyzontal lines will be displayed

        Vector2 DEFAULT_WINDOW_SIZE = new Vector2(1280, 350);

        const float TANG_LENGTH_REF = 50f;
        const float MARGIN_PIXELS_REF = 10f;
        const float BASIC_SHAPE_WIDTH_PIXELS_REF = 35f;
        const float BASIC_SHAPE_SPACE_PIXELS_REF = 6f;

        public static float tangFloat = TANG_LENGTH_REF;//the length of tangents when the respective key is selected 
        public static float marginPixels = MARGIN_PIXELS_REF;//needed when mouse selecting lines, points, tangents...
        static float sqrMarginPixels = marginPixels * marginPixels;

        const float marginErr = 1E-5f;

        float basicShapeWidthPixels = BASIC_SHAPE_WIDTH_PIXELS_REF;//the width of the rectangles keeping the curves basic shapes	
        float basicShapesSpacePixels = BASIC_SHAPE_SPACE_PIXELS_REF;//the space in pixels between two consecutive basic shapes	

        const int SHAPE_COUNT = 9;
        AnimationCurve[] basicShapes = new AnimationCurve[SHAPE_COUNT];
        Rect[] basicShapesRect = new Rect[SHAPE_COUNT];
        Rect normalRect = new Rect(0, 0, 1, 1);//defines basic animation curves in this rect
        float[] basicShapeClips = new float[SHAPE_COUNT];

        bool showCursorNormal = true;

        ContextMenuManager contextMenuManager = new ContextMenuManager();

        ContextMenuUI contextMenuUI;

        Vector2 addKeyPos;

        public bool WindowClosed { get; set; } = true;

        enum ContextOptions { clamped, auto, freesmooth, broken }
        ContextOptions lastSelectedOption = ContextOptions.freesmooth;

        public bool AlteredData { get; private set; }

        int vertLines;
        int vertLineStones;
        float vertLinesAlpha;
        float vertLinesGap;

        float mAlpha;

        bool mMirroredHor;
        int mSegmentsHor;
        float mSampleHor;
        float mStartHor;

        MultipleKeySelection multipleKeySelection;

        UndoRedo undoRedo;

        public RectTransform undo;
        public RectTransform redo;

        public RectTransform zoomDefault;

        System.Action onAlterAction;

        Camera currentCamera;

        Mesh meshGridLines;
        Mesh meshBasicCurveQuads;

        const float Z_LINE_POS = Curves.Z_POS_HALF;

        Color HALF_GRAY = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        Color LIGHT_GRAY = new Color(0.5f, 0.5f, 0.5f, 0.75f);

        public static float PREFERRED_DELTA_TIME = 1f / 120f;//while the drawing of all lines will by done at the application FPS, we prefer the related calculations to be limited (120FPS in this case)
        public static float deltaTime;

        void Awake()
        {
            meshGridLines = new Mesh();
            meshBasicCurveQuads = new Mesh();
            var renderParamsTemp = new RenderParams(lineMaterial);
            renderParamsTemp.layer = CurveWindow.MESH_LAYER;
            RenderParams = renderParamsTemp;

           GradRect = CurveForm.defaultRect;
        }

        public void InitConstantValues() {
            float adjCurves = 0.6f;
            if (Screen.dpi != 0) {
                float adj = Screen.dpi / DEFAULT_DPI;
                marginPixels = adj * MARGIN_PIXELS_REF;
                sqrMarginPixels = marginPixels * marginPixels;
                if (adj > 1) {
                    adj *= 0.5f;
                    if (adj < 1) {
                        adj = 1;
                    }
                    adjCurves /= adj;
                    tangFloat = adj * TANG_LENGTH_REF;
                    adj *= 0.75f;
                    basicShapeWidthPixels = adj * BASIC_SHAPE_WIDTH_PIXELS_REF;
                    basicShapesSpacePixels = adj * BASIC_SHAPE_SPACE_PIXELS_REF;
                }
            }
            Curves.margin = marginPixels * adjCurves;
        }

        void FillListColor() {
            foreach (Color color in colors) {
                usedColorList.Add(color);
            }
        }

        void Start() {

            if (usedColorList.Count == 0) {
                FillListColor();
            }

            SetupBasicShapes();

            Curves.dictCurvesContextMenus = contextMenuManager.dictCurvesContextMenus;
            Curves.lineMaterial = lineMaterial;
            Curves.renderParams = RenderParams;

            if (ActiveCurveForm == null) {
                ActiveCurveForm = new CurveForm();
            }

            contextMenuKey = Instantiate(contextMenuKeyObject).GetComponent<RectTransform>();
            keyValue = Instantiate(keyValueObject).GetComponent<KeyValue>();
            contextMenuUI = contextMenuKey.GetComponent<ContextMenuUI>();

            multipleKeySelection = GetComponent<MultipleKeySelection>();

            KeyHandling = new KeyHandling(this);
            undoRedo = new UndoRedo(undo, redo);  
            currentCamera = GetComponent<Camera>();
            Curves.camera = currentCamera;

            StartCoroutine(CheckExternalCurveChanges());
        }

        void SetupBasicShapes() {

            //create the animation curves coresponding the basic shapes
            basicShapes[0] = new AnimationCurve();
            basicShapes[0].AddKey(0f, 0.5f);

            basicShapes[1] = new AnimationCurve();
            basicShapes[1].AddKey(0f, 0f);
            basicShapes[1].AddKey(1f, 1f);

            basicShapes[2] = new AnimationCurve();
            basicShapes[2].AddKey(0f, 1f);
            basicShapes[2].AddKey(1f, 0f);

            basicShapes[3] = new AnimationCurve();
            Keyframe keyframe = new Keyframe(0, 0);
            keyframe.outTangent = 0;
            basicShapes[3].AddKey(keyframe);
            keyframe = new Keyframe(1, 1);
            keyframe.inTangent = 2;
            basicShapes[3].AddKey(keyframe);

            basicShapes[4] = new AnimationCurve();
            keyframe = new Keyframe(0, 1);
            keyframe.outTangent = -2;
            basicShapes[4].AddKey(keyframe);
            keyframe = new Keyframe(1, 0);
            keyframe.inTangent = 0;
            basicShapes[4].AddKey(keyframe);

            basicShapes[5] = new AnimationCurve();
            keyframe = new Keyframe(0, 0);
            keyframe.outTangent = 2;
            basicShapes[5].AddKey(keyframe);
            keyframe = new Keyframe(1, 1);
            keyframe.inTangent = 0;
            basicShapes[5].AddKey(keyframe);

            basicShapes[6] = new AnimationCurve();
            keyframe = new Keyframe(0, 1);
            keyframe.outTangent = 0;
            basicShapes[6].AddKey(keyframe);
            keyframe = new Keyframe(1, 0);
            keyframe.inTangent = -2;
            basicShapes[6].AddKey(keyframe);

            basicShapes[7] = new AnimationCurve();
            keyframe = new Keyframe(0, 0);
            keyframe.outTangent = 0;
            basicShapes[7].AddKey(keyframe);
            keyframe = new Keyframe(1, 1);
            keyframe.inTangent = 0;
            basicShapes[7].AddKey(keyframe);

            basicShapes[8] = new AnimationCurve();
            keyframe = new Keyframe(0, 1);
            keyframe.outTangent = 0;
            basicShapes[8].AddKey(keyframe);
            keyframe = new Keyframe(1, 0);
            keyframe.inTangent = 0;
            basicShapes[8].AddKey(keyframe);
        }

        IEnumerator CheckExternalCurveChanges()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                //check if new keys have been added/deleted outside of curve
                if (ActiveCurveForm.curve1 != null)
                {
                    if (ActiveCurveForm.firstCurveSelected ? (ActiveCurveForm.curve1.length != ActiveCurveForm.curve1KeysCount) :
                        (ActiveCurveForm.curve2.length != ActiveCurveForm.curve2KeysCount))
                    {
                        selectedKeyIndex = KeyHandling.UNSELECTED;//just be sure that selected key is not an out of range key
                        UpdateCurveKeys(ActiveCurveForm.curve1);
                    }
                }
                //update the list of context menus, when key has been added/deleted
                for (int i = 0; i < curveFormList.Count; ++i)
                {
                    CurveForm curveForm = curveFormList[i];
                    if (curveForm.curve1.length != curveForm.curve1KeysCount)
                    {
                        contextMenuManager.UpdateDictContextMenu(curveForm.curve1, curveForm.curve1.length - curveForm.curve1KeysCount);
                        curveForm.curve1KeysCount = curveForm.curve1.length;
                        curveFormList.RemoveAt(i);
                        curveFormList.Insert(i, curveForm);
                        if (curveForm.curve1 == ActiveCurveForm.curve1)
                        {
                            ActiveCurveForm = curveForm;
                        }
                        AlterCurveData(curveForm.curve1);
                    }

                    if (curveForm.curve2 != null && curveForm.curve2.length != curveForm.curve2KeysCount)
                    {
                        contextMenuManager.UpdateDictContextMenu(curveForm.curve2, curveForm.curve2.length - curveForm.curve2KeysCount);
                        curveForm.curve2KeysCount = curveForm.curve2.length;
                        curveFormList.RemoveAt(i);
                        curveFormList.Insert(i, curveForm);
                        if (curveForm.curve2 == ActiveCurveForm.curve2)
                        {
                            ActiveCurveForm = curveForm;
                        }
                        AlterCurveData(curveForm.curve2);
                    }
                }
            }
        }


        /// <summary>
        /// On each update, check if the mouse is clicked, if so check what (a basic shape, a tangent, key or a whole line).
        /// Also make updates of the mouse context menus list, when new keys are added/deleted.
        /// </summary>
        void Update() {

            //now check user interaction
            if (curveWindow.IsTouchedBegan() || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) {
                if (keyValue.IsKeyEditVisible() && !keyValue.FocusOnInputFields()) {
                    keyValue.SetKeyEditEnabled(false);
                }
                Vector2 mousePos = curveWindow.CursorPos();
                if (!keyValue.IsKeyEditVisible() && !contextMenuUI.Hover(mousePos)) {
                    //check first if the user tries to drag the tangent of the selected key (these should be selectable even if they are outside of the grid)
                    CheckMouseTangentSelection(mousePos);
                    if (!isTangentSelected) 
                    {/*
                        for (int i = 0; i < SHAPE_COUNT; ++i) 
                        {
                            if (basicShapesRect[i].Contains(mousePos)) 
                            {
                                if (ActiveCurveForm.curve1 != null) 
                                {
                                    undoRedo.AddOperation(new BasicShapeOperation(this));
                                    ReplaceActiveCurve(basicShapes[i]);
                                    selectedKeyIndex = KeyHandling.UNSELECTED;
                                }
                                break;
                            }
                        }*/
                        if ((ActiveCurveForm.curve1 != null) && (GridRect.xMin - marginPixels < mousePos.x) && (GridRect.xMax + marginPixels > mousePos.x) &&
                            (GridRect.yMin - marginPixels < mousePos.y) && (GridRect.yMax + marginPixels > mousePos.y)) {

                            bool anyCurveSelected = false;
                            if (CheckMouseSelection(mousePos, ActiveCurveForm)) {
                                anyCurveSelected = true;
                            } else {
                                foreach (CurveForm curveForm in curveFormList)
                                {
                                    if (curveForm.curve1 == ActiveCurveForm.curve1) continue;
                                    if (CheckMouseSelection(mousePos, curveForm))
                                    {
                                        anyCurveSelected = true;
                                        break;
                                    }
                                }
                            }

                            if (multipleKeySelection.MultipleKeysAreSelected()) {
                                if (Input.GetMouseButtonDown(0))
                                {
                                    if (multipleKeySelection.InsideSelectedKeys())
                                    {
                                        multipleKeysMove = true;
                                    } else {
                                        multipleKeysResize = multipleKeySelection.OnResizingLines();
                                    }
                                }

                                if (multipleKeysMove || (multipleKeysResize != ResizePart.None))
                                {
                                    PrepareSelectedKeysForMovement();
                                } else {
                                    multipleKeySelection.ClearMultipleKeySelection();
                                }
                            }
                            // --- 核心修改：必须按住 Shift 键，点击空白处才会触发多选(框选)逻辑 ---
                            if (!multipleKeysMove && (multipleKeysResize == ResizePart.None) && !anyCurveSelected && Input.GetMouseButtonDown(0))
                            {
                                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                                {
                                    multipleKeySelection.StartMultipleKeySelection();
                                }
                            }
                        }
                    }
                }
            } else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) {
                if (Input.GetKeyUp(KeyCode.Z)) {
                    Undo();
                } else if (Input.GetKeyUp(KeyCode.Y)) {
                    Redo();
                }
            }
            // ---------------- 修改：按空格或 Shift+空格 在红线处添加关键帧 ----------------
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // 检查是否同时按住了左 Shift 或 右 Shift
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    AddKeysToAllCurvesAtTimeCursor(); // Shift + 空格：给所有(三条)曲线加帧
                }
                else
                {
                    AddKeyAtTimeCursor(); // 仅按空格：只给当前高亮的曲线加帧
                }
            }
            // --------------------------------------------------------------------------------------------------------------------------------------------

            // === 新增：分离的键盘快捷键处理 ===
            HandleKeyboardDelete();      // 专门处理删除
            HandleKeyboardNavigation();  // 专门处理移动和切换
            HandleKeyboardEdit();        // 专门处理打开编辑面板 (新增)
            HandleKeyboardCurveSelection(); // 专门处理数字键切换曲线 (新增)
            DrawGridAndLines();
        }

        // === 新增：用于记录连续键盘移动的撤销状态 ===
        private bool isKeyboardMoving = false;
        private Vector2 keyboardMoveStartPos;

        // === 新增：用左右方向键切换，或者 Ctrl+方向键 连续移动关键帧 ===
        private void HandleKeyboardNavigation()
        {
            // 只有当存在选中的关键帧，且有激活的曲线时才执行
            if (selectedKeyIndex == KeyHandling.UNSELECTED || ActiveCurveForm == null || ActiveCurveForm.SelectedCurve() == null)
                return;

            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();

            // 检测是否按住了 Ctrl 键
            bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (isCtrlPressed)
            {
                // --- 1. 记录按下瞬间的初始位置（用于撤销/重做） ---
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                    Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    if (!isKeyboardMoving)
                    {
                        isKeyboardMoving = true;
                        keyboardMoveStartPos = new Vector2(activeCurve[selectedKeyIndex].time, activeCurve[selectedKeyIndex].value);
                    }
                }

                // --- 2. 处理连续移动逻辑 ---
                Vector2 moveDelta = Vector2.zero;

                // 将步长乘以 Time.deltaTime，确保连续移动速度丝滑且不受电脑帧率影响
                float timeStep = GradRect.width * 0.1f * Time.deltaTime;
                float valueStep = GradRect.height * 0.1f * Time.deltaTime;

                // 按住 Shift 键，移动速度放大 5 倍
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    timeStep *= 5f;
                    valueStep *= 5f;
                }

                // 核心修改：使用 GetKey 替代 GetKeyDown，实现按住连续移动
                if (Input.GetKey(KeyCode.LeftArrow)) moveDelta.x -= timeStep;
                if (Input.GetKey(KeyCode.RightArrow)) moveDelta.x += timeStep;
                if (Input.GetKey(KeyCode.UpArrow)) moveDelta.y += valueStep;
                if (Input.GetKey(KeyCode.DownArrow)) moveDelta.y -= valueStep;

                if (moveDelta != Vector2.zero)
                {
                    Keyframe keyframe = activeCurve[selectedKeyIndex];
                    keyframe.time += moveDelta.x;
                    keyframe.value += moveDelta.y;

                    int newIndex = KeyHandling.MoveKey(activeCurve, selectedKeyIndex, keyframe);

                    if (newIndex != KeyHandling.UNSELECTED)
                    {
                        selectedKeyIndex = newIndex;

                        // 刷新黑色的 UI 提示标签
                        Vector2 keyframePos = new Vector2(keyframe.time, keyframe.value);
                        Vector2 keyScreenPos = Utils.Convert(keyframePos, EntireRect, ActiveCurveForm.gradRect);
                        keyValue.SetTimeValueText(keyframe.time, keyframe.value);
                        keyValue.SetLabelPos(keyScreenPos);

                        // 触发曲线重绘
                        AlterCurveData(activeCurve);
                    }
                }

                // --- 3. 当所有方向键都松开时，将这段连续的移动统一计入 1 次撤销历史中 ---
                if (isKeyboardMoving && !Input.GetKey(KeyCode.LeftArrow) && !Input.GetKey(KeyCode.RightArrow) &&
                    !Input.GetKey(KeyCode.UpArrow) && !Input.GetKey(KeyCode.DownArrow))
                {
                    isKeyboardMoving = false;
                    Vector2 currentPos = new Vector2(activeCurve[selectedKeyIndex].time, activeCurve[selectedKeyIndex].value);
                    Vector2 totalDiff = currentPos - keyboardMoveStartPos;
                    if (totalDiff != Vector2.zero)
                    {
                        undoRedo.AddOperation(new MoveOperation(this, selectedKeyIndex, totalDiff));
                    }
                }
            }
            else
            {
                // 如果中途松开了 Ctrl 键，也要结束记录状态，防止撤销逻辑卡死
                if (isKeyboardMoving)
                {
                    isKeyboardMoving = false;
                    Vector2 currentPos = new Vector2(activeCurve[selectedKeyIndex].time, activeCurve[selectedKeyIndex].value);
                    Vector2 totalDiff = currentPos - keyboardMoveStartPos;
                    if (totalDiff != Vector2.zero)
                    {
                        undoRedo.AddOperation(new MoveOperation(this, selectedKeyIndex, totalDiff));
                    }
                }

                // --- 模式 2：仅方向键 -> 切换选中的关键帧 (保持 GetKeyDown 防止切得太快) ---
                bool selectionChanged = false;
                int newIndex = selectedKeyIndex;

                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    if (selectedKeyIndex > 0)
                    {
                        newIndex = selectedKeyIndex - 1;
                        selectionChanged = true;
                    }
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    if (selectedKeyIndex < activeCurve.length - 1)
                    {
                        newIndex = selectedKeyIndex + 1;
                        selectionChanged = true;
                    }
                }

                if (selectionChanged)
                {
                    selectedKeyIndex = newIndex;
                    Keyframe keyframe = activeCurve[newIndex];

                    Vector2 keyframePos = new Vector2(keyframe.time, keyframe.value);
                    Vector2 keyScreenPos = Utils.Convert(keyframePos, EntireRect, ActiveCurveForm.gradRect);

                    keyValue.SetLabelEnabled(true, this);
                    keyValue.SetTimeValueText(keyframe.time, keyframe.value);
                    keyValue.SetLabelPos(keyScreenPos);

                    if (multipleKeySelection != null && multipleKeySelection.MultipleKeysAreSelected())
                    {
                        multipleKeySelection.ClearMultipleKeySelection();
                    }

                    AlterCurveData(activeCurve);
                }
            }
        }


        //按一下移动一下
        /*
          // === 新增：用左右方向键切换，或者 Ctrl+方向键 移动关键帧 ===
        private void HandleKeyboardNavigation()
        {
            // 只有当存在选中的关键帧，且有激活的曲线时才执行
            if (selectedKeyIndex == KeyHandling.UNSELECTED || ActiveCurveForm == null || ActiveCurveForm.SelectedCurve() == null)
                return;

            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            
            // 检测是否按住了 Ctrl 键
            bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (isCtrlPressed)
            {
                // --- 模式 1：Ctrl + 方向键 -> 微调关键帧位置 ---
                Vector2 moveDelta = Vector2.zero;
                
                // 动态计算步长：每次点击移动曲线整体范围的 0.2%，这样不论什么尺寸的曲线移动手感都一致
                float timeStep = GradRect.width * 0.002f;  
                float valueStep = GradRect.height * 0.002f; 

                // 如果同时按住 Shift 键，可以使移动步长放大 10 倍进行快速移动
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    timeStep *= 10f;
                    valueStep *= 10f;
                }

                // 收集按键移动增量
                if (Input.GetKeyDown(KeyCode.LeftArrow)) moveDelta.x -= timeStep;
                if (Input.GetKeyDown(KeyCode.RightArrow)) moveDelta.x += timeStep;
                if (Input.GetKeyDown(KeyCode.UpArrow)) moveDelta.y += valueStep;
                if (Input.GetKeyDown(KeyCode.DownArrow)) moveDelta.y -= valueStep;

                if (moveDelta != Vector2.zero)
                {
                    Keyframe keyframe = activeCurve[selectedKeyIndex];
                    keyframe.time += moveDelta.x;
                    keyframe.value += moveDelta.y;

                    // 调用底层的移动逻辑，它会自动处理边界和节点交错重新排序的问题
                    int newIndex = KeyHandling.MoveKey(activeCurve, selectedKeyIndex, keyframe);
                    
                    if (newIndex != KeyHandling.UNSELECTED)
                    {
                        selectedKeyIndex = newIndex;
                        
                        // 计入撤销/重做系统 (Ctrl+Z 可以撤回)
                        undoRedo.AddOperation(new MoveOperation(this, selectedKeyIndex, moveDelta));

                        // 刷新黑色的 UI 提示标签
                        Vector2 keyframePos = new Vector2(keyframe.time, keyframe.value);
                        Vector2 keyScreenPos = Utils.Convert(keyframePos, EntireRect, ActiveCurveForm.gradRect);
                        keyValue.SetTimeValueText(keyframe.time, keyframe.value);
                        keyValue.SetLabelPos(keyScreenPos);

                        // 触发曲线重绘
                        AlterCurveData(activeCurve);
                    }
                }
            }
            else
            {
                // --- 模式 2：仅方向键 -> 切换选中的关键帧 ---
                bool selectionChanged = false;
                int newIndex = selectedKeyIndex;

                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    if (selectedKeyIndex > 0)
                    {
                        newIndex = selectedKeyIndex - 1;
                        selectionChanged = true;
                    }
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    if (selectedKeyIndex < activeCurve.length - 1)
                    {
                        newIndex = selectedKeyIndex + 1;
                        selectionChanged = true;
                    }
                }

                if (selectionChanged)
                {
                    selectedKeyIndex = newIndex;
                    Keyframe keyframe = activeCurve[newIndex];
                    
                    Vector2 keyframePos = new Vector2(keyframe.time, keyframe.value);
                    Vector2 keyScreenPos = Utils.Convert(keyframePos, EntireRect, ActiveCurveForm.gradRect);

                    keyValue.SetLabelEnabled(true, this);
                    keyValue.SetTimeValueText(keyframe.time, keyframe.value);
                    keyValue.SetLabelPos(keyScreenPos);

                    if (multipleKeySelection != null && multipleKeySelection.MultipleKeysAreSelected())
                    {
                        multipleKeySelection.ClearMultipleKeySelection();
                    }

                    AlterCurveData(activeCurve);
                }
            }
        }
         */


        // === 新增：专门处理键盘删除关键帧的方法 ===
        private void HandleKeyboardDelete()
        {
            // 如果没有选中任何关键帧，直接返回
            if (selectedKeyIndex == KeyHandling.UNSELECTED || ActiveCurveForm == null || ActiveCurveForm.SelectedCurve() == null)
                return;

            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                DeleteKey(); // 调用自带撤销支持的删除逻辑
                keyValue.SetLabelEnabled(false); // 隐藏黑色的数值提示框
            }
        }

        // === 新增：专门处理键盘呼出关键帧编辑面板的方法 ===
        private void HandleKeyboardEdit()
        {
            // 如果没有选中任何关键帧，直接返回
            if (selectedKeyIndex == KeyHandling.UNSELECTED || ActiveCurveForm == null || ActiveCurveForm.SelectedCurve() == null)
                return;

            // 如果编辑面板已经处于打开状态，则不重复触发，防止与确认输入的 Enter 键冲突
            if (keyValue != null && keyValue.IsKeyEditVisible())
                return;

            // 检测是否按住了 Ctrl 键
            bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            // 监听 Ctrl + Enter (兼容主键盘回车 KeyCode.Return 和数字小键盘回车 KeyCode.KeypadEnter)
            if (isCtrlPressed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                EditKey(); // 调用底层原有的逻辑，瞬间将编辑面板定位到当前关键帧并显示
            }
        }

        // === 新增：专门处理使用数字键切换激活曲线的方法 ===
        private void HandleKeyboardCurveSelection()
        {
            // 确保曲线列表存在且已加载
            if (curveFormList == null || curveFormList.Count == 0) return;

            // 如果编辑面板或者输入框正在被占用（比如正在输入数值），则屏蔽快捷键，防止输入数字时误切曲线
            if (keyValue != null && keyValue.FocusOnInputFields()) return;

            // 检测数字键 1 (兼容小键盘1 和 主键盘1)
            if (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (curveFormList.Count > 0)
                {
                    SelectCurveByIndex(0);
                }
            }
            // 检测数字键 2 (兼容小键盘2 和 主键盘2)
            else if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Alpha2))
            {
                if (curveFormList.Count > 1)
                {
                    SelectCurveByIndex(1);
                }
            }
            // 检测数字键 3 (兼容小键盘3 和 主键盘3)
            else if (Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.Alpha3))
            {
                if (curveFormList.Count > 2)
                {
                    SelectCurveByIndex(2);
                }
            }
        }

        /// <summary>
        /// 在时间轴红线与当前激活曲线的交叉点处添加关键帧，并强制设置为 freeSmooth 模式
        /// </summary>
        public void AddKeyAtTimeCursor()
        {
            if (ActiveCurveForm == null || ActiveCurveForm.SelectedCurve() == null) return;

            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            float currentTime = curveWindow.currentTargetX;

            // ---------------- 安全检查 1：如果曲线是空的 ----------------
            if (activeCurve.length == 0)
            {
                ContextMenu freeSmoothMenu = new ContextMenu();
                freeSmoothMenu.freeSmooth = true;

                // 空曲线直接在当前时间点创建默认值为 0 的关键帧
                selectedKeyIndex = activeCurve.AddKey(currentTime, 0f);
                contextMenuManager.dictCurvesContextMenus[activeCurve].Insert(selectedKeyIndex, freeSmoothMenu);
                undoRedo.AddOperation(new AddOperation(this, new Vector2(currentTime, 0f)));
                AlterCurveData(activeCurve);
                return;
            }

            // ---------------- 安全检查 2：如果当前时间已存在关键帧 ----------------
            for (int i = 0; i < activeCurve.length; i++)
            {
                if (Mathf.Abs(activeCurve[i].time - currentTime) < 1E-5f)
                {
                    // 如果刚好在同一个时间点，直接选中它，不再重复添加
                    selectedKeyIndex = i;
                    AlterCurveData(activeCurve);
                    return;
                }
            }

            // 正常流程：计算值并添加新关键帧
            float currentValue = activeCurve.Evaluate(currentTime);
            Vector2 newKeyPos = new Vector2(currentTime, currentValue);

            ContextMenu newMenu = new ContextMenu();
            newMenu.freeSmooth = true;

            AddKey(newKeyPos, newMenu);
            undoRedo.AddOperation(new AddOperation(this, newKeyPos));
            AlterCurveData(activeCurve);
        }

        /// <summary>
        /// 在时间轴红线处，给编辑器里的所有曲线都添加关键帧 (强制 freeSmooth)
        /// </summary>
        public void AddKeysToAllCurvesAtTimeCursor()
        {
            if (curveFormList == null || curveFormList.Count == 0) return;

            CurveForm originalActiveForm = ActiveCurveForm;
            int originalSelectedIndex = selectedKeyIndex;
            float currentTime = curveWindow.currentTargetX;

            foreach (CurveForm form in curveFormList)
            {
                ActiveCurveForm = form;
                AnimationCurve currentCurve = form.SelectedCurve();

                if (currentCurve != null)
                {
                    // ---------------- 安全检查 1：如果曲线是空的 ----------------
                    if (currentCurve.length == 0)
                    {
                        ContextMenu freeSmoothMenu = new ContextMenu();
                        freeSmoothMenu.freeSmooth = true;
                        int newIdx = currentCurve.AddKey(currentTime, 0f);
                        contextMenuManager.dictCurvesContextMenus[currentCurve].Insert(newIdx, freeSmoothMenu);
                        undoRedo.AddOperation(new AddOperation(this, new Vector2(currentTime, 0f)));
                        AlterCurveData(currentCurve);
                        continue; // 这条曲线处理完毕，继续下一条
                    }

                    // ---------------- 安全检查 2：如果当前时间已存在关键帧 ----------------
                    bool keyExists = false;
                    for (int i = 0; i < currentCurve.length; i++)
                    {
                        if (Mathf.Abs(currentCurve[i].time - currentTime) < 1E-5f)
                        {
                            keyExists = true;
                            break;
                        }
                    }
                    if (keyExists)
                    {
                        continue; // 已有节点，直接跳过这条曲线的添加，避免报错
                    }

                    // 正常流程
                    float currentValue = currentCurve.Evaluate(currentTime);
                    Vector2 newKeyPos = new Vector2(currentTime, currentValue);

                    ContextMenu newMenu = new ContextMenu();
                    newMenu.freeSmooth = true;

                    AddKey(newKeyPos, newMenu);
                    undoRedo.AddOperation(new AddOperation(this, newKeyPos));
                    AlterCurveData(currentCurve);
                }
            }

            // 完美还原用户之前的选中状态
            ActiveCurveForm = originalActiveForm;
            selectedKeyIndex = originalSelectedIndex;
            Curves.TriggerUpdateCurves();
        }

        void PrepareSelectedKeysForMovement()
        {
            AnimationCurve curve = ActiveCurveForm.SelectedCurve();
            List<int> selectedKeyIndices = multipleKeySelection.SelectedKeyIndices();
            selectedKeysStartingPos = new List<Vector2>(selectedKeyIndices.Count);
            foreach (int index in selectedKeyIndices)
            {
                Keyframe keyframe = curve[index];
                selectedKeysStartingPos.Add(new Vector2(keyframe.time, keyframe.value));
            }
        }

        public void Undo() {
            undoRedo.Undo();
            AlterData();
        }

        public void Redo() {
            undoRedo.Redo(); 
            AlterData();
        }

        public void ZoomDefault()
        {
            curveWindow.GetComponent<ZoomBehaviour>().Reset();
            EntireRect = GridRect;
            zoomDefault.gameObject.SetActive(false);
            AlterData();
        }

        public void ShowWindow() {
            if (WindowClosed) {
                EnableWindow(true);
            }
        }

        public void CloseWindow() {
            if (!WindowClosed) {
                EnableWindow(false);
                if ((multipleKeySelection != null) && multipleKeySelection.MultipleKeysAreSelected()) {
                    multipleKeySelection.ClearMultipleKeySelection();
                }
            }
        }

        void EnableWindow(bool enable) {
            WindowClosed = !enable;
            curveWindow.WindowClosed = WindowClosed;
            enabled = enable;
            curveWindow.transform.parent.gameObject.SetActive(enable);
            AlterData();
        }

        void UpdateCurveKeys(AnimationCurve animCurve) {
            float ratio = gradRect.height * prevGradRect.width / (gradRect.width * prevGradRect.height);
            for (int i = 0; i < animCurve.length; ++i) {
                Keyframe keyframe = animCurve[i];
                keyframe.value = (keyframe.value - prevGradRect.yMin) * gradRect.height / prevGradRect.height + gradRect.yMin;
                keyframe.inTangent *= ratio;
                keyframe.outTangent *= ratio;
                animCurve.MoveKey(i, keyframe);
            }
        }
        
        public void UpdateActiveCurveKeys() {
            if ((gradRect.width != 0f) && (gradRect.height != 0f) && (ActiveCurveForm.curve1 != null)) {
                UpdateCurveKeys(ActiveCurveForm.curve1);
                if (ActiveCurveForm.curve2 != null) {
                    UpdateCurveKeys(ActiveCurveForm.curve2);
                }
            }
        }

        public void SetGradRectAndActiveForm(Rect gradRect)
        {
            GradRect = gradRect;
            if (ActiveCurveForm != null) {
                ActiveCurveForm.gradRect = gradRect;
            }
        }

        /// <summary>
        /// Adds a new curve form, with references to the given curve1 (the usual case is of a single curve shown, when the curve2 is null).
        /// </param>
        public void AddCurveForm(AnimationCurve curve1, AnimationCurve curve2, AnimationCurve curve3) {
            //if there is a curve form having the curve1, then only the second curve is updated
            CurveForm curveForm = curveFormList.Find(x => x.curve1 == curve1);
            if (curveForm == null) {
                if (usedColorList.Count == 0) {
                    FillListColor();
                }
                curveForm = new CurveForm(curve1, curve2, usedColorList[0]);
                if (ActiveCurveForm != null) {
                    GradRect = curveForm.gradRect;//always set the grad to the default values
                }
                curveFormList.Add(curveForm);
                usedColorList.RemoveAt(0);
                if (usedColorList.Count == 0) {
                    FillListColor();
                }
                selectedKeyIndex = KeyHandling.UNSELECTED;
            } else {
                curveForm.curve2 = curve2;
                curveForm.curve3 = curve3;
                if ((curve2 == null) && !curveForm.firstCurveSelected) {
                    selectedKeyIndex = KeyHandling.UNSELECTED;
                    curveForm.firstCurveSelected = true;
                }
            }

            if ((multipleKeySelection != null) && multipleKeySelection.MultipleKeysAreSelected()) {
                multipleKeySelection.ClearMultipleKeySelection();
            }

            AddContextMenuStructs(curve1);
            AddContextMenuStructs(curve2);

            undoRedo?.ClearOperationsList();//add curve has no undo/redo support(because is external) so clear the operations list

            AlterData();
            ActiveCurveForm = curveForm;
        }

        public void AddRestoredCurveForm(AnimationCurve curve1, AnimationCurve curve2, AnimationCurve curve3, Rect gradRect) {
            AddCurveForm(curve1, curve2, curve3);
            ActiveCurveForm.gradRect = gradRect;
            GradRect = gradRect;
        }

        public void RestoreActiveCurve(AnimationCurve curve) {
            if (curve != ActiveCurveForm.curve1) {
                CurveForm curveForm = curveFormList.Find(x => x.curve1 == curve);
                if (curveForm != null) {
                    ActiveCurveForm = curveForm;
                    if ((multipleKeySelection != null) && multipleKeySelection.MultipleKeysAreSelected()) {
                        multipleKeySelection.ClearMultipleKeySelection();
                    }
                    GradRect = curveForm.gradRect;
                }
            }
        }

        public void ResetRect() {
            prevEntireRectXMin = 0;
            prevEntireRectYMin = 0;
            prevEntireRectHeight = 0;
            prevEntireRectWidth = 0;
            GridRect = Rect.zero;
            prevGradRect = Rect.zero;
        }

        void AddContextMenuStructs(AnimationCurve curve) {
            contextMenuManager.AddContextMenuObjects(curve);
        }

        /// <summary>
        /// Remove the curve form related to the given curve.
        /// </param>
        public void RemoveCurve(AnimationCurve curve) {
            CurveForm curveForm = curveFormList.Find(x => x.curve1 == curve);
            if (curveForm != null) {
                usedColorList.Insert(0, curveForm.color);
                curveFormList.Remove(curveForm);
                contextMenuManager.Remove(curve);

                if (curveFormList.Count == 0) {
                    ActiveCurveForm = new CurveForm();
                    GradRect = ActiveCurveForm.gradRect;
                } else {
                    UpdateActiveCurveForm(curveFormList[0]);
                }
                if ((multipleKeySelection != null) && multipleKeySelection.MultipleKeysAreSelected()) {
                    multipleKeySelection.ClearMultipleKeySelection();
                }
                selectedKeyIndex = KeyHandling.UNSELECTED;
                undoRedo?.ClearOperationsList();//remove curve has no undo/redo support(because is external) so clear the operations list
                AlterData();
            }
        }

        /// <summary>
        /// Replace the active curve when the user clicks on a basic shape.
        /// </param>
        void ReplaceActiveCurve(AnimationCurve curve) {
            ReplaceActiveCurve(curve.keys);
        }

        public void ReplaceActiveCurve(Keyframe[] keyframes, List<ContextMenu> tempCurveContextMenus) {
            AnimationCurve curve = ActiveCurveForm.SelectedCurve();
            while (curve.length > 0) {
                curve.RemoveKey(0);//remove all the keys
            }
            for (int i = 0; i < keyframes.Length; ++i) {
                Keyframe keyframe = keyframes[i];
                curve.AddKey(keyframe);
            }
            contextMenuManager.dictCurvesContextMenus[curve] = tempCurveContextMenus;
            SetActiveCurveFormKeyCount(curve.length);
        }

        void ReplaceActiveCurve(Keyframe[] keyframes) {
            AnimationCurve newCurve = ActiveCurveForm.firstCurveSelected ? ActiveCurveForm.curve1 : ActiveCurveForm.curve2;
            while (newCurve.length > 0) {
                newCurve.RemoveKey(0);//remove all the keys
            }

            float ratio = gradRect.height * normalRect.width / (gradRect.width * normalRect.height);
            for (int i = 0; i < keyframes.Length; ++i) {
                Keyframe keyframe = keyframes[i];
                keyframe.value = (keyframe.value - normalRect.yMin) * gradRect.height / normalRect.height + gradRect.yMin;
                keyframe.time = (keyframe.time - normalRect.xMin) * gradRect.width / normalRect.width + gradRect.xMin;
                keyframe.inTangent *= ratio;
                keyframe.outTangent *= ratio;
                newCurve.AddKey(keyframe);
            }

            contextMenuManager.UpdateContextMenuList(newCurve);
            SetActiveCurveFormKeyCount(newCurve.length);
        }

        void SetActiveCurveFormKeyCount(int length) {
            if (ActiveCurveForm.firstCurveSelected) {
                ActiveCurveForm.curve1KeysCount = length;
            } else {
                ActiveCurveForm.curve2KeysCount = length;
            }
            AlterData();
            if (multipleKeySelection.MultipleKeysAreSelected()) {
                multipleKeySelection.ClearMultipleKeySelection();
            }
        }

        /// <summary>
        /// True if the given curve is visible in the editor.
        /// </param>
        public bool CurveShown(AnimationCurve curve) {
            CurveForm curveForm = curveFormList.Find(x => x.curve1 == curve);
            return (curveForm != null) && (curveForm.curve1 != null) && (curveForm.curve2 == null);
        }

        /// <summary>
        /// True if the given curves are added as a path to the editor.
        /// </param>
        public bool CurvesShown(AnimationCurve curve1, AnimationCurve curve2) {
            CurveForm curveForm = curveFormList.Find(x => x.curve1 == curve1);
            return (curveForm != null) && (curveForm.curve1 != null) && (curveForm.curve2 != null);
        }

        public void SetGradRectYMax(float yMax, bool addToUndoStack = false) {
            if (addToUndoStack) {
                undoRedo.AddOperation(new GradChangeOperation(this, GradRect.yMax));
            }
            Rect gradRectTemp = GradRect;
            gradRectTemp.yMax = yMax;
            if(gradRectTemp.yMin != 0)
            {
                gradRectTemp.yMin = -yMax;
            }

            GradRect = gradRectTemp;

            if (ActiveCurveForm != null) {
                ActiveCurveForm.gradRect = GradRect;
                UpdateActiveCurveKeys();
            }
        }

        public void UpdateGrid(Rect newGridRect) {
            if (GridRect.width == 0) {
                EntireRect = newGridRect;
                var zoomLevel = curveWindow.GetComponent<ZoomBehaviour>().Level;

                if (zoomLevel != ZoomBehaviour.DEFAULT_LEVEL || zoomRatioX != 0 || zoomRatioY != 0)
                {
                    EntireRect = new Rect(EntireRect.x - zoomRatioX * EntireRect.width, EntireRect.y - zoomRatioY * EntireRect.height, 
                                            EntireRect.width * zoomLevel, EntireRect.height * zoomLevel);
                    zoomDefault.gameObject.SetActive(true);
                }
            } else {
                //proportions should stay the same, as the grid is moved or resized (not zoomed)
                float ratioX = newGridRect.width / GridRect.width;
                float ratioY = newGridRect.height / GridRect.height;
                EntireRect = new Rect(ratioX * (EntireRect.xMin - GridRect.xMin) + newGridRect.xMin,
                                        ratioY * (EntireRect.yMin - GridRect.yMin) + newGridRect.yMin,
                                        ratioX * EntireRect.width, ratioY * EntireRect.height);
            }
            GridRect = newGridRect;
            adjGridRect = newGridRect;
            adjGridRect.xMin -= 0.5f;
            adjGridRect.xMax += 0.5f;
            adjGridRect.yMin -= 0.5f;
            adjGridRect.yMax += 0.5f;

            multipleKeySelection.UpdateSelectedKnots(GridRect);
        }

        void DrawGridAndLines() {
            if (gradRect.height > Mathf.Epsilon) {
                deltaTime += Time.deltaTime;
                if (deltaTime > PREFERRED_DELTA_TIME)
                {
                    UpdateGridLinesMesh();
                }

                Graphics.RenderMesh(RenderParams, meshGridLines, 0, Matrix4x4.identity);
                DrawCurves();
               // DrawBasicShapes();
                if (deltaTime > PREFERRED_DELTA_TIME)
                {
                    deltaTime = 0;
                }
            }
        }
        
        void UpdateGridLinesMesh()
        {
            bool recalculate = false;
            bool gradUpdate = false;
            bool gridVerticalUpdate = false;

            if (prevGradRect != gradRect)
            {
                prevGradRect = gradRect;
                recalculate = true;
                gradUpdate = true;
            }

            if (prevEntireRectYMin != EntireRect.yMin)
            {
                prevEntireRectYMin = EntireRect.yMin;
                recalculate = true;
                gridVerticalUpdate = true;
            }

            if (prevEntireRectHeight != EntireRect.height)
            {
                prevEntireRectHeight = EntireRect.height;
                if (!gridVerticalUpdate)
                {
                    recalculate = true;
                    gridVerticalUpdate = true;
                }
            }

            if (recalculate)
            {
                CalculateHorizontalLines(gradUpdate, gridVerticalUpdate);
                mMirroredHor = gradRect.yMin < 0;
                mSegmentsHor = mMirroredHor ? 2 : 1;
                mSampleHor = (1f - mRezid) * EntireRect.height / (mSegmentsHor * mHorLines);
                mStartHor = mMirroredHor ? (EntireRect.yMin + EntireRect.yMax) * 0.5f : EntireRect.yMin;
            }

            //vertical lines (it was tested only with default values for xMin and xMax)
            bool gridHorizontallUpdate = false;
            if (prevEntireRectWidth != EntireRect.width)
            {
                prevEntireRectWidth = EntireRect.width;
                gridHorizontallUpdate = true;
            }

            if (prevEntireRectXMin != EntireRect.xMin)
            {
                prevEntireRectXMin = EntireRect.xMin;
                gridHorizontallUpdate = true;
            }

            if (gridHorizontallUpdate)
            {
                CalculateVerticalLines();
            }

            if(recalculate || gridHorizontallUpdate)
            {                 
                List<Vector3> vertices = new List<Vector3>();
                List<Color> colors = new List<Color>();
                
                //horyzontal lines(it was tested for gradations that ranges from ymin = 0(or '-ymax') to ymax = 'posivitve value' )	      
                for (int i = 0; i <= mHorLines; i++)
                {
                    if (i % (mMidHor ? 2 : 5) == 0 || LastLine(i) || i == 0)
                    {
                        lineColor.a = 1.0f;
                    } else {
                        lineColor.a = mAlpha;
                    }
                    
                    if (AddLineVertices(i, mStartHor, mSampleHor, EntireRect.yMax, vertices))
                    {
                        AddColorTwice(colors, lineColor);
                    }
                    if ((i != 0) && mMirroredHor)
                    {
                        if(AddLineVertices(i, mStartHor, -mSampleHor, EntireRect.yMin, vertices))
                        {
                            AddColorTwice(colors, lineColor);
                        }
                    }

                    if (LastLineRezid(i))
                    {
                        lineColor.a = 1.0f;
                        if(AddRezidualLineVertices(EntireRect.yMax, vertices))
                        {
                            AddColorTwice(colors, lineColor);
                        }
                        if (mMirroredHor)
                        {
                            if(AddRezidualLineVertices(EntireRect.yMin, vertices))
                            {
                                AddColorTwice(colors, lineColor);
                            }
                        }
                    }
                }

                //positions of vertical lines are calculate each drawing cycle, as an improvement, these positions should be calculated(re - calculated)
                //only when the size of the rectangle gets modified
                for (int i = 0; i <= vertLines; i++)
                {
                    float gradation = EntireRect.xMin + i * vertLinesGap;
                    if ((adjGridRect.xMin <= gradation) && (gradation <= adjGridRect.xMax))
                    {
                        if (i % vertLineStones == 0)
                        {
                            lineColor.a = 1.0f;
                        }
                        else
                        {
                            lineColor.a = vertLinesAlpha;
                        }

                        AddColorTwice(colors, lineColor);

                        var yMin = Mathf.Max(GridRect.yMin, EntireRect.yMin);
                        var yMax = Mathf.Min(GridRect.yMax, EntireRect.yMax);

                        if (GridRect == Rect.zero)
                        {
                            //Debug.LogWarning("GridRect is zero!");
                            yMin = EntireRect.yMin;
                            yMax = EntireRect.yMax;
                        }
                        vertices.Add(currentCamera.ScreenToWorldPoint(new Vector3(gradation, yMin, Z_LINE_POS)));
                        vertices.Add(currentCamera.ScreenToWorldPoint(new Vector3(gradation, yMax, Z_LINE_POS)));
                    }
                }

                SetupMeshLines(meshGridLines, vertices, colors);                           
                Curves.TriggerUpdateCurves();
                Curves.TriggerUpdateBasicCurves();
            }
        }
        
        void AddColorTwice(List<Color> colors, Color color)
        {
            colors.AddRange(new Color[] { color, color });
        }

        bool LastLine(int i) {
            return (i == mHorLines) && (mRezid == 0);
        }

        bool LastLineRezid(int i) {
            return (i == mHorLines) && (mRezid > 0);
        }

        bool AddLineVertices(int i, float start, float sample, float limit, List<Vector3> vertices) {
            bool add = false;
            float gradation = LastLine(i) ? limit : (start + i * sample);
            if ((adjGridRect.yMin <= gradation) && (gradation <= adjGridRect.yMax)) {
                var xMin = Mathf.Max(GridRect.xMin, EntireRect.xMin);
                var xMax = Mathf.Min(GridRect.xMax, EntireRect.xMax);
                if (GridRect == Rect.zero)
                {
                    //Debug.LogWarning("GridRect is zero!");
                    xMin = EntireRect.xMin;
                    xMax = EntireRect.xMax;
                }
                vertices.Add(currentCamera.ScreenToWorldPoint(new Vector3(xMin, gradation, Z_LINE_POS)));
                vertices.Add(currentCamera.ScreenToWorldPoint(new Vector3(xMax, gradation, Z_LINE_POS)));
                add = true;
            }
            return add;
        }

        bool AddRezidualLineVertices(float limit, List<Vector3> vertices) {
            bool add = false;
            if ((adjGridRect.yMin <= limit) && (limit <= adjGridRect.yMax)) {
                var xMin = Mathf.Max(GridRect.xMin, EntireRect.xMin);
                var xMax = Mathf.Min(GridRect.xMax, EntireRect.xMax);
                if (GridRect == Rect.zero)
                {
                    //Debug.LogWarning("GridRect is zero!");
                    xMin = EntireRect.xMin;
                    xMax = EntireRect.xMax;
                }
                vertices.Add(currentCamera.ScreenToWorldPoint(new Vector3(xMin, limit, Z_LINE_POS)));
                vertices.Add(currentCamera.ScreenToWorldPoint(new Vector3(xMax, limit, Z_LINE_POS)));
                add = true;
            }
            return add;
        }

        void SetupMeshLines(Mesh mesh, List<Vector3> vertices, List<Color> colors)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            int[] indices = new int[vertices.Count];
            for (int i = 0; i < vertices.Count; ++i)
            {
                indices[i] = i;
            }
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.SetColors(colors);
        }

        void CalculateHorizontalLines(bool gradUpdate, bool gridHeightUpdate) {
            //calculate how many horyzontal lines should be drawn (based of the grid size, and the gradations ranges)        
            float ratio = EntireRect.height / HeightUnit;
            int segments = 1;
            float prevRezid;
            int prevHorlines;
            bool mirrored;
            if ((gradRect.yMin < 0) && (0 < gradRect.yMax)) {
                segments = 2;
                GetHorLinesCountAndRezid(gradRect.yMax, ratio / segments, out mRezid, out prevRezid, out mHorLines, out prevHorlines);
                mirrored = true;
            } else {
                GetHorLinesCountAndRezid(gradRect.yMax - gradRect.yMin, ratio, out mRezid, out prevRezid, out mHorLines, out prevHorlines);
                mirrored = false;
            }

            mAlpha = (ratio / (mHorLines * segments) - 0.2f) * 1.25f;//the intermediate lines are more transparent             

            int rowCount = mHorLines;
            float rezid = mRezid;
            if (mAlpha < 0.35f) {
                rowCount = prevHorlines;
                rezid = prevRezid;
            }

            if (gridHeightUpdate || gradUpdate) {
                curveWindow.UpdateVerGradations(rowCount, rezid, mirrored, gradUpdate);
            }
        }

        void CalculateVerticalLines()
        {
            // 核心修改：基于时间（秒）来划分横轴网格，确保刻度总是整数秒
            float pixelsPerSecond = EntireRect.width / GradRect.width;
            float minSecondsPerTick = (WidthUnit * 1.0f) / pixelsPerSecond;

            int timeStep = 1;
            vertLineStones = 5;

            // 智能档位：根据缩放级别，自动选择最舒服的整数秒间距
            if (minSecondsPerTick <= 1) { timeStep = 1; vertLineStones = 5; }
            else if (minSecondsPerTick <= 2) { timeStep = 2; vertLineStones = 5; }
            else if (minSecondsPerTick <= 5) { timeStep = 5; vertLineStones = 2; }
            else if (minSecondsPerTick <= 10) { timeStep = 10; vertLineStones = 6; }
            else if (minSecondsPerTick <= 15) { timeStep = 15; vertLineStones = 4; }
            else if (minSecondsPerTick <= 30) { timeStep = 30; vertLineStones = 2; }
            else if (minSecondsPerTick <= 60) { timeStep = 60; vertLineStones = 2; }
            else
            {
                timeStep = Mathf.CeilToInt(minSecondsPerTick / 60f) * 60;
                vertLineStones = 2;
            }

            vertLines = Mathf.CeilToInt(GradRect.width / timeStep);
            vertLinesGap = timeStep * pixelsPerSecond;

            float currentRatio = timeStep / minSecondsPerTick;
            vertLinesAlpha = Mathf.Clamp((currentRatio - 0.2f) * 1.25f, 0.2f, 1.0f);

            // 将计算出的整数秒步长传递给UI绘制
            curveWindow.UpdateHorGradations(timeStep);
        }
        /*
        public void Zoom(float factor, Vector2 mousePos) {
            float invFactor = 1f / factor;
            float x = mousePos.x - (mousePos.x - EntireRect.x) * invFactor;
            float width = EntireRect.width * invFactor;
            float y = mousePos.y - (mousePos.y - EntireRect.y) * invFactor;
            float height = EntireRect.height * invFactor;
            UpdateEntireRect(x, y, width, height);
            zoomDefault.gameObject.SetActive(true);
        }*/

        public void Zoom(float factor, Vector2 mousePos)
        {
            bool zoomX = Input.GetKey(KeyCode.Z);
            bool zoomY = Input.GetKey(KeyCode.X);

            // 1. 如果 Z 和 X 都没有按住，直接返回，不触发任何缩放
            if (!zoomX && !zoomY)
            {
                return;
            }

            float invFactor = 1f / factor;

            // 2. 先获取当前的原始位置和尺寸
            float x = EntireRect.x;
            float y = EntireRect.y;
            float width = EntireRect.width;
            float height = EntireRect.height;

            // 3. 如果按住了 Z 键，计算并应用横向(X轴)的缩放
            if (zoomX)
            {
                // 核心修改：去掉以鼠标为中心的偏移算法。
                // x 保持为 EntireRect.x (固定左侧锚点)，只对宽度进行延伸计算
                width = EntireRect.width * invFactor;
            }

            // 4. 如果按住了 X 键，计算并应用纵向(Y轴)的缩放
            if (zoomY)
            {
                // 同理，纵向也去掉鼠标中心缩放，以底部为固定锚点向上延伸
                height = EntireRect.height * invFactor;
            }

            // 5. 更新视图矩形 (如果同时按住 Z 和 X，则会同时缩放双轴)
            UpdateEntireRect(x, y, width, height);
            zoomDefault.gameObject.SetActive(true);
        }

        public void Pan(Vector2 diff)
        {
            bool panX = Input.GetKey(KeyCode.Z);
            bool panY = Input.GetKey(KeyCode.X);

            // 1. 如果 Z 和 X 都没有按住，直接返回，彻底禁止原有的自由拖动
            if (!panX && !panY)
            {
                return;
            }

            float width = EntireRect.width;
            float height = EntireRect.height;

            // 2. 先获取当前的原始位置
            float x = EntireRect.x;
            float y = EntireRect.y;

            // 3. 按住 Z 键时，才将横向的拖拽增量加上去
            if (panX)
            {
                x += diff.x;
            }

            // 4. 按住 X 键时，才将纵向的拖拽增量加上去
            if (panY)
            {
                y += diff.y;
            }

            // 5. 应用最终的位置
            UpdateEntireRect(x, y, width, height);
            zoomDefault.gameObject.SetActive(true);
        }

        void UpdateEntireRect(float x, float y, float width, float height) {
            EntireRect = new Rect(x, y, width, height);

            // ------------------ 新增代码 ------------------
            // 必须在这里通知 CurveWindow 更新红线，
            // 因为 Pan(平移) 和 Zoom(缩放) 都会调用这个方法。
            if (curveWindow != null) {
                curveWindow.UpdateTimeCursorPosition();
            }
            // ---------------------------------------------

            multipleKeySelection.UpdateSelectedKnots(GridRect);
            AlterData();
        }

        void ResetBasicClips()
        {
            for (int i = 0; i < SHAPE_COUNT; ++i)
            {
                basicShapeClips[i] = 0;
            }
        }

        void DrawBasicShapes() {
            if (gradRect.height > 0f)
            {
                if (deltaTime > PREFERRED_DELTA_TIME) {
                    if (Curves.BasicCurvesUpdate(basicShapes[0])) {
                        float alignMiddle = 0f;
                        if (GridRect.width > SHAPE_COUNT * (basicShapeWidthPixels + basicShapesSpacePixels)) {
                            alignMiddle = GridRect.width * 0.5f - (SHAPE_COUNT * 0.5f * (basicShapeWidthPixels + basicShapesSpacePixels));
                        }
                        ResetBasicClips();
                        List<Vector3> verticesQuads = new List<Vector3>();
                        for (int i = 0; i < SHAPE_COUNT; ++i) {
                            float shapeMin = (basicShapeWidthPixels + basicShapesSpacePixels) * i;
                            if (GridRect.xMin + shapeMin > GridRect.xMax) {
                                break;
                            }

                            float shapeMax = basicShapeWidthPixels + shapeMin;

                            if (GridRect.xMin + shapeMax > GridRect.xMax) {
                                shapeMax = GridRect.xMax - GridRect.xMin;
                            }

                            Rect shapeRect = Rect.MinMaxRect(GridRect.xMin + shapeMin + alignMiddle, BottomShapesYmin, GridRect.xMin + shapeMax + alignMiddle, BottomShapesYmax);

                            verticesQuads.Add(currentCamera.ScreenToWorldPoint(new Vector3(shapeRect.xMin, shapeRect.yMin, 0)));
                            verticesQuads.Add(currentCamera.ScreenToWorldPoint(new Vector3(shapeRect.xMin, shapeRect.yMax, 0)));
                            verticesQuads.Add(currentCamera.ScreenToWorldPoint(new Vector3(shapeRect.xMax, shapeRect.yMax, 0)));
                            verticesQuads.Add(currentCamera.ScreenToWorldPoint(new Vector3(shapeRect.xMax, shapeRect.yMin, 0)));

                            basicShapeClips[i] = shapeRect.width;
                            shapeRect.xMax = GridRect.xMin + basicShapeWidthPixels + shapeMin + alignMiddle;
                            if (basicShapeClips[i] < shapeRect.width) {
                                basicShapeClips[i] = basicShapeClips[i] / shapeRect.width;
                            } else {
                                basicShapeClips[i] = 1f;
                            }
                            basicShapesRect[i] = shapeRect;
                        }

                        meshBasicCurveQuads.Clear();
                        meshBasicCurveQuads.SetVertices(verticesQuads);
                        int[] indices = new int[verticesQuads.Count];
                        Color[] colors = new Color[verticesQuads.Count];
                        for (int i = 0; i < verticesQuads.Count; ++i)
                        {
                            indices[i] = i;
                            colors[i] = HALF_GRAY;
                        }
                        meshBasicCurveQuads.SetColors(colors);
                        meshBasicCurveQuads.SetIndices(indices, MeshTopology.Quads, 0);
                    }
                }
                Graphics.RenderMesh(RenderParams, meshBasicCurveQuads, 0, Curves.matrix4X4_Z_POS_HALF);

                for (int i = 0; i < SHAPE_COUNT; ++i)
                {
                    float clip = basicShapeClips[i];
                    if (clip == 0f)
                    {
                        break;
                    }
                    Curves.DrawCurveForm(LIGHT_GRAY, basicShapes[i], null, false, false, -1, basicShapesRect[i], basicShapesRect[i], normalRect, true, clip);
                }
            }
        }

        void UpdateActiveCurveForm(CurveForm curveForm) {
            if (ActiveCurveForm.curve1 == curveForm.curve1) {
                ActiveCurveForm.firstCurveSelected = curveForm.firstCurveSelected;
            } else {
                ActiveCurveForm = curveForm;
                GradRect = ActiveCurveForm.gradRect;
            }
            AlterData();
        }

        void CheckChangeCurveSelection(CurveForm curveForm, int keyIndex) {
            if ((ActiveCurveForm != curveForm) || (ActiveCurveForm.firstCurveSelected != curveForm.firstCurveSelected) || (selectedKeyIndex != keyIndex)) {
                undoRedo.AddOperation(new CurveSelectionOperation(this, curveFormList.IndexOf(ActiveCurveForm), ActiveCurveForm.firstCurveSelected, selectedKeyIndex));
            }
        }

        public void SelectCurveForm(int curveFormIndex) {
            UpdateActiveCurveForm(curveFormList[curveFormIndex]);
        }
        /*
        /// <summary>
        /// 供外部 UI 按钮调用的切换曲线方法。
        /// </summary>
        /// <param name="index">曲线在列表中的索引 (0代表第一条，1代表第二条...)</param>
        public void SelectCurveByIndex(int index)
        {
            // 1. 安全检查：确保索引没有越界
            if (index < 0 || index >= curveFormList.Count)
            {
                Debug.LogWarning($"[CurveLines] 尝试切换到的曲线索引 {index} 超出范围！当前曲线总数: {curveFormList.Count}");
                return;
            }

            CurveForm targetCurveForm = curveFormList[index];

            // 2. 如果点击的就是当前已经激活的曲线，直接跳过
            if (ActiveCurveForm == targetCurveForm)
            {
                return;
            }

            // 3. 切换前，必须清空之前曲线的关键帧选中状态（单选和多选）
            selectedKeyIndex = KeyHandling.UNSELECTED;
            if (multipleKeySelection != null && multipleKeySelection.MultipleKeysAreSelected())
            {
                multipleKeySelection.ClearMultipleKeySelection();
            }
            if (keyValue != null)
            {
                keyValue.SetLabelEnabled(false);
            }

            // 4. 切换激活的曲线 (这会自动更新 GradRect 并调用 AlterData)
            UpdateActiveCurveForm(targetCurveForm);

            // 5. 触发重绘，让曲线在视觉上呈现被选中的状态
            Curves.TriggerUpdateCurve(targetCurveForm.SelectedCurve());
            if (targetCurveForm.UnselectedCurve() != null)
            {
                Curves.TriggerUpdateCurve(targetCurveForm.UnselectedCurve());
            }
        }*/
        /// <summary>
        /// 原切换曲线方法 -> 现已改造为【曲线显示/隐藏切换】方法。
        /// （保持方法名不变，避免你的 UI 按钮绑定丢失）
        /// </summary>
        public void SelectCurveByIndex(int index)
        {
            if (index < 0 || index >= curveFormList.Count) return;

            CurveForm targetCurveForm = curveFormList[index];

            // ✨ 核心修改：反转可见性
            targetCurveForm.isVisible = !targetCurveForm.isVisible;

            // 隐藏时的安全清理逻辑
            if (!targetCurveForm.isVisible)
            {
                if (ActiveCurveForm == targetCurveForm)
                {
                    selectedKeyIndex = KeyHandling.UNSELECTED;
                    if (multipleKeySelection != null && multipleKeySelection.MultipleKeysAreSelected())
                    {
                        multipleKeySelection.ClearMultipleKeySelection();
                    }
                    if (keyValue != null)
                    {
                        keyValue.SetLabelEnabled(false);
                    }
                }
            }
            else
            {
                // ✨ 可选优化：当重新显示一条曲线时，顺便把它设为激活(焦点)，方便直接编辑
                UpdateActiveCurveForm(targetCurveForm);
            }

            // 触发全局重绘，刷新视觉
            AlterData();
            Curves.TriggerUpdateCurves();
        }


        /// <summary>
        /// 供外部 UI 按钮调用的：一键【全部显示 / 全部隐藏】所有曲线
        /// </summary>
        public void ToggleAllCurvesVisibility()
        {
            if (curveFormList == null || curveFormList.Count == 0) return;

            // 1. 判断当前状态：如果所有曲线都已经显示，则目标状态为"隐藏"；否则目标状态为"全部显示"
            bool allVisible = true;
            foreach (CurveForm form in curveFormList)
            {
                if (!form.isVisible)
                {
                    allVisible = false;
                    break;
                }
            }

            // 目标状态反转
            bool targetVisibility = !allVisible;

            // 2. 遍历应用目标状态
            foreach (CurveForm form in curveFormList)
            {
                form.isVisible = targetVisibility;
            }

            // 3. 安全清理逻辑：如果是执行“全部隐藏”，必须清空所有的关键帧选中与编辑状态，防止报错
            if (!targetVisibility)
            {
                selectedKeyIndex = KeyHandling.UNSELECTED;

                if (multipleKeySelection != null && multipleKeySelection.MultipleKeysAreSelected())
                {
                    multipleKeySelection.ClearMultipleKeySelection();
                }

                if (keyValue != null)
                {
                    keyValue.SetLabelEnabled(false);
                }
            }

            // 4. 触发全局数据更新和画面重绘
            AlterData();
            Curves.TriggerUpdateCurves();
        }

        public int GetCurveFormIndex() {
            return curveFormList.IndexOf(ActiveCurveForm);
        }

        public bool GetFirstCurveSelected() {
            return (ActiveCurveForm != null) && ActiveCurveForm.firstCurveSelected;
        }

        public void SelectFirstCurve(bool firstCurve) {
            ActiveCurveForm.firstCurveSelected = firstCurve;
        }

        /// <summary>
        /// Checks if the user wanna drag a tangent of the selected key
        /// </param>
        void CheckMouseTangentSelection(Vector2 mousePos) {
            if ((selectedKeyIndex >= 0) && Input.GetMouseButtonDown(0)) {
                // ✨ 新增拦截：如果当前激活的曲线被隐藏了，禁止调节切线
                if (!ActiveCurveForm.isVisible) return;
                AnimationCurve curve = ActiveCurveForm.firstCurveSelected ? ActiveCurveForm.curve1 : ActiveCurveForm.curve2;
                Vector2 keyScreenPos = Utils.Convert(new Vector2(curve[selectedKeyIndex].time, curve[selectedKeyIndex].value), EntireRect, ActiveCurveForm.gradRect);

                if (adjGridRect.Contains(keyScreenPos)) {//check first the keyframe(and tangents) are visible
                    Keyframe keyframe = curve[selectedKeyIndex];     
                    ContextMenu contextMenu = GetContextMenuForCurrentKey();
                    if (curve.length - selectedKeyIndex > 1) {
                        if (Curves.tangPeakRightVisible && Vector2.SqrMagnitude(Curves.tangPeakRight - mousePos) <= sqrMarginPixels) {
                            isTangentSelected = true;
                            leftTangentSelected = false;
                        }
                    }
                    if (!isTangentSelected && (selectedKeyIndex > 0))
                    {
                        if (Curves.tangPeakLeftVisible && Vector2.SqrMagnitude(Curves.tangPeakLeft - mousePos) <= sqrMarginPixels) {
                            isTangentSelected = true;
                            leftTangentSelected = true;
                        }
                    }
                    if (isTangentSelected) {
                        undoRedo.AddOperation(new TangentModeOperation(this, contextMenu, keyframe.inTangent, keyframe.outTangent));
                    }
                }
            }
        }

        /// <summary>
        /// Checks what the user clicks(selects), a key or a whole curve. 
        /// </summary>
        bool CheckMouseSelection(Vector2 mousePos, CurveForm curveForm) {
            // ✨ 新增拦截：如果曲线处于隐藏状态，彻底无视鼠标的点击判定
            if (!curveForm.isVisible) return false;
            int i;
            List<AnimationCurve> curves = new List<AnimationCurve>();
            curves.Add(curveForm.curve1);
            if (curveForm.curve2 != null) {
                curves.Add(curveForm.curve2);
            }

            foreach (AnimationCurve curve in curves) {
                for (i = 0; i < curve.length; ++i) {
                    Keyframe keyframe = curve[i];
                    Vector2 keyframePos = new Vector2(keyframe.time, keyframe.value);
                    Vector2 keyScreenPos = Utils.Convert(keyframePos, EntireRect, curveForm.gradRect);
                    if (adjGridRect.Contains(keyScreenPos)) {//check first the keyframe is visible
                        if (Vector2.SqrMagnitude(keyScreenPos - mousePos) <= sqrMarginPixels) {
                            CheckChangeCurveSelection(curveForm, i);
                            selectedKeyIndex = i;
                            curveForm.firstCurveSelected = curveForm.curve1 == curve;

                            Curves.TriggerUpdateCurve(curveForm.SelectedCurve());
                            Curves.TriggerUpdateCurve(curveForm.UnselectedCurve());
                            UpdateActiveCurveForm(curveForm);
                            if (multipleKeySelection.MultipleKeysAreSelected()) {
                                multipleKeySelection.ClearMultipleKeySelection();
                            }
                            if (curveWindow.IsDoubleTap() || Input.GetMouseButtonDown(1)) {
                                ShowContextMenuKey(mousePos);
                            } else if (curveWindow.IsSingleTap() || Input.GetMouseButtonDown(0)) {
                                keyDragged = true;
                                keyValue.SetLabelEnabled(true, this);
                                selectedKeyStartingPos = keyframePos;

                                keyValue.SetTimeValueText(keyframe.time, keyframe.value);
                                keyValue.SetLabelPos(keyScreenPos);
                            }
                            return true;
                        }
                    }
                }
                if (i == curve.length) {
                    if (Utils.PointLineSqrDist(new Vector2(mousePos.x, mousePos.y), curve, EntireRect, curveForm.gradRect, contextMenuManager) <= sqrMarginPixels) {
                        if (curveWindow.IsDoubleTap() || Input.GetMouseButton(1)) {
                            ShowContextMenuAddKey(mousePos);
                        }
                        /*
                        else if (Input.GetMouseButtonDown(0)) 
                        {
                            lineDragged = true;
                            showCursorNormal = false;
                            Cursor.SetCursor(TextureNS, CurveWindow.hotspot, CursorMode.ForceSoftware);
                            lineDragKeysStartingValue = new List<float>(curve.length);
                            foreach (Keyframe keyframe in curve.keys) {
                                lineDragKeysStartingValue.Add(keyframe.value);
                            }
                        }*/
                        CheckChangeCurveSelection(curveForm, KeyHandling.UNSELECTED);
                        curveForm.firstCurveSelected = curveForm.curve1 == curve;

                        Curves.TriggerUpdateCurve(curveForm.SelectedCurve());
                        Curves.TriggerUpdateCurve(curveForm.UnselectedCurve());
                        UpdateActiveCurveForm(curveForm);
                        if (multipleKeySelection.MultipleKeysAreSelected()) {
                            multipleKeySelection.ClearMultipleKeySelection();
                        }
                        selectedKeyIndex = KeyHandling.UNSELECTED;
                        return true;
                    }
                } 
            }
            return false;
        }

        void AddKeyByScreenPos(Vector3 screenPos) {
            Vector2 pos = Utils.Convert(new Vector2(screenPos.x, screenPos.y), gradRect, EntireRect);
            AddKey(pos);
            undoRedo.AddOperation(new AddOperation(this, pos));
        }

        public void AddKey(Vector2 pos, ContextMenu definedContextMenu = null) {
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            List<ContextMenu> listContextMenus = contextMenuManager.dictCurvesContextMenus[activeCurve];
            ContextMenu contextMenu = definedContextMenu;
            if (definedContextMenu == null) 
            {
                contextMenu = new ContextMenu();
                switch (lastSelectedOption) {
                    case ContextOptions.freesmooth:
                        contextMenu.freeSmooth = true;
                        break;
                    case ContextOptions.clamped:
                        contextMenu.clampedAuto = true;
                        break;
                    case ContextOptions.auto:
                        contextMenu.auto = true;
                        break;
                    case ContextOptions.broken:
                        contextMenu.broken = true;
                        contextMenu.leftTangent.free = true;
                        contextMenu.rightTangent.free = true;
                        contextMenu.bothTangents.free = true;
                        break;
                }
            }

            if ((pos.x < activeCurve[0].time) || (pos.x > activeCurve[activeCurve.length - 1].time)) {
                Keyframe keyframeNeighbour = (pos.x < activeCurve[0].time) ? activeCurve[0] : activeCurve[activeCurve.length - 1];
                selectedKeyIndex = activeCurve.AddKey(pos.x, pos.y);
                if (activeCurve.length > 2) {
                    //hack needed( wierd:when a key is added in the clamped area, the neighbour's key changes its tangents...)
                    activeCurve.MoveKey((pos.x < activeCurve[1].time) ? 1 : activeCurve.length - 2, keyframeNeighbour);
                }
                Keyframe keyframe = activeCurve[selectedKeyIndex];
                keyframe.inTangent = 0;
                keyframe.outTangent = 0;

                if (contextMenu.freeSmooth) {
                    contextMenu.flat = true;
                }
                listContextMenus.Insert(selectedKeyIndex, contextMenu);
            } else {
                for (int i = 0; i < activeCurve.length - 1; ++i) {
                    if ((pos.x > activeCurve[i].time) && (pos.x < activeCurve[i + 1].time)) {
                        Keyframe keyframe = new Keyframe(pos.x, pos.y);
                        if (listContextMenus[i].rightTangent.constant || listContextMenus[i + 1].leftTangent.constant ||
                           (activeCurve[i].outTangent == float.PositiveInfinity) || (activeCurve[i + 1].inTangent == float.PositiveInfinity)) {
                            keyframe.inTangent = 0;
                            keyframe.outTangent = 0;
                            contextMenu.freeSmooth = true;
                        } else {
                            var keyOut = activeCurve[i];
                            var keyIn = activeCurve[i + 1];

                            Vector2 val = new Vector2(keyOut.time, keyOut.value);
                            val = Utils.Convert(val, EntireRect, gradRect);
                            Vector2 val2 = new Vector2(keyIn.time, keyIn.value);
                            val2 = Utils.Convert(val2, EntireRect, gradRect);
                            float tangOut = keyOut.outTangent;
                            float tangIn = keyIn.inTangent;
                            float ratio = EntireRect.height * gradRect.width / (EntireRect.width * gradRect.height);

                            var tangOutWeightMode = keyOut.weightedMode;
                            var tangInWeightMode = keyIn.weightedMode;

                            float tangWeightOut = (tangOutWeightMode == WeightedMode.Out) || (tangOutWeightMode == WeightedMode.Both) ? keyOut.outWeight : Curves.WEIGHTED_RATIO;
                            float tangWeightIn = (tangInWeightMode == WeightedMode.In) || (tangInWeightMode == WeightedMode.Both) ? keyIn.inWeight : Curves.WEIGHTED_RATIO;

                            Vector2 c1;
                            Vector2 c2;
                            Curves.GetControlPoints(val, val2, tangOut * ratio, tangIn * ratio, out c1, out c2, tangWeightOut, tangWeightIn);

                            float t = Utils.closestPointTValue;
                            //de Casteljau's algorithm for dividing a bezier curve
                            Vector2 p00 = (1 - t) * val + t * c1;
                            Vector2 p11 = (1 - t) * c1 + t * c2;
                            Vector2 p22 = (1 - t) * c2 + t * val2;
                            Vector2 newC2 = (1 - t) * p00 + t * p11;
                            Vector2 newC1 = (1 - t) * p11 + t * p22;

                            //got the control points, now find the tangents for the new point
                            Curves.GetTangents(val, Utils.closestPoint, c1, newC2, out tangOut, out tangIn);
                            tangIn /= ratio;
                            keyframe.inTangent = -tangIn;
                            Curves.GetTangents(Utils.closestPoint, val2, newC1, c2, out tangOut, out tangIn);
                            tangOut /= ratio;

                            keyframe.outTangent = tangOut;
                        }

                        selectedKeyIndex = activeCurve.AddKey(keyframe);

                        if (contextMenu.freeSmooth && ContextMenuManager.IsKeyframeFlat(keyframe)) {
                            contextMenu.flat = true;
                        }
                        listContextMenus.Insert(selectedKeyIndex, contextMenu);
                        break;
                    }
                }
            }

            CheckUpdateAutoTangents(contextMenu, activeCurve, selectedKeyIndex);

            //update neighbours if they are auto	
            if (selectedKeyIndex > 0) {
                CheckUpdateAutoTangents(listContextMenus[selectedKeyIndex - 1], activeCurve, selectedKeyIndex - 1);
                //update the neighbour if it is linear in this direction
                if (listContextMenus[selectedKeyIndex - 1].leftTangent.linear) {
                    UpdateLinearTangent(activeCurve, selectedKeyIndex - 1, false);
                }
            }
            if (selectedKeyIndex < activeCurve.keys.Length - 1) {
                CheckUpdateAutoTangents(listContextMenus[selectedKeyIndex + 1], activeCurve, selectedKeyIndex + 1);
                //update the neighbour if it is linear on this direction
                if (listContextMenus[selectedKeyIndex + 1].rightTangent.linear) {
                    UpdateLinearTangent(activeCurve, selectedKeyIndex + 1, true);
                }
            }

            if (ActiveCurveForm.firstCurveSelected) {
                ActiveCurveForm.curve1KeysCount += 1;
            } else {
                ActiveCurveForm.curve2KeysCount += 1;
            }
            AlterCurveData(activeCurve);
        }

        public void CheckUpdateAutoTangents(ContextMenu contextMenu, AnimationCurve animationCurve, int keyIndex) {
            if (contextMenu.auto) {
                UpdateLegacyAutoTangents(animationCurve, keyIndex);
            } else if (contextMenu.clampedAuto) {
                UpdateClampedAutoTangents(animationCurve, keyIndex);
            }
        }

        void ShowContextMenuKey(Vector2 mousePos) {
            contextMenuUI.EnablePanel();
            contextMenuUI.SetPos(mousePos);
            contextMenuUI.SetListener(this);
            ContextMenu contextMenu = contextMenuManager.dictCurvesContextMenus[ActiveCurveForm.SelectedCurve()][selectedKeyIndex];
            contextMenuUI.SetSelectedOption(contextMenu);
        }

        void ShowContextMenuAddKey(Vector2 mousePos) {
            contextMenuUI.EnableAddPanel(true);
            contextMenuUI.SetPos(mousePos);
            contextMenuUI.SetListener(this);
            addKeyPos = mousePos;
        }

        public void DeleteKey() {
            if (ActiveCurveForm.SelectedCurve().length > 1) {
                undoRedo.AddOperation(new DeleteOperation(this));
            }
            DeleteKeySimple();
        }

        public void DeleteKeySimple() {
            KeyHandling.DeleteKey();
            selectedKeyIndex = KeyHandling.UNSELECTED;
            AlterCurveData(ActiveCurveForm.SelectedCurve());
        }

        public void AddKey() {
            AddKeyByScreenPos(addKeyPos);
        }

        public void AddKey(Keyframe keyframe, ContextMenu contextMenu) {
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            AddKey(new Vector2(keyframe.time, keyframe.value), contextMenu);
            activeCurve.MoveKey(selectedKeyIndex, keyframe);//activate tangents
        }

        public void EditKey() {
            keyValue.SetKeyEditEnabled(true, this);
            Keyframe keyframe = ActiveCurveForm.SelectedCurve()[selectedKeyIndex];
            Vector2 keyframePos = Utils.Convert(new Vector2(keyframe.time, keyframe.value), EntireRect, gradRect);
            keyValue.SetPanelEditPos(keyframePos);
            keyValue.SetTimeValueEditFields(keyframe.time, keyframe.value);
        }

        public ContextMenu GetContextMenuForCurrentKey() {
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            return contextMenuManager.dictCurvesContextMenus[activeCurve][selectedKeyIndex];
        }

        void TangentModeChange() {
            Keyframe keyframe = ActiveCurveForm.SelectedCurve()[selectedKeyIndex];
            undoRedo.AddOperation(new TangentModeOperation(this, GetContextMenuForCurrentKey(), keyframe.inTangent, keyframe.outTangent));
        }

        public void ClampedAutoKey(bool addToUndoStack = false) {
            if (addToUndoStack) {
                TangentModeChange();
            }
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            ContextMenu contextMenu = contextMenuManager.dictCurvesContextMenus[activeCurve][selectedKeyIndex];
            if (!contextMenu.clampedAuto) {
                contextMenu.Reset();
                contextMenu.clampedAuto = true;
                if (activeCurve.keys.Length > 0) {
                    UpdateClampedAutoTangents(activeCurve, selectedKeyIndex);
                }
                lastSelectedOption = ContextOptions.clamped;
            }
            AlterCurveData(activeCurve);
        }

        public void AutoKey(bool addToUndoStack = false) {
            if (addToUndoStack) {
                TangentModeChange();
            }
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            ContextMenu contextMenu = contextMenuManager.dictCurvesContextMenus[activeCurve][selectedKeyIndex];
            if (!contextMenu.auto) {
                contextMenu.Reset();
                contextMenu.auto = true;
                if (activeCurve.keys.Length > 0) {
                    UpdateLegacyAutoTangents(activeCurve, selectedKeyIndex);
                }
                lastSelectedOption = ContextOptions.auto;
            }
            AlterCurveData(activeCurve);
        }

        public void FreeSmoothKey(bool addToUndoStack = false) {
            if (addToUndoStack) {
                TangentModeChange();
            }
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            ContextMenu contextMenu = contextMenuManager.dictCurvesContextMenus[activeCurve][selectedKeyIndex];
            if (!contextMenu.freeSmooth) {
                contextMenu.Reset();
                contextMenu.freeSmooth = true;
                Keyframe keyframe = activeCurve.keys[selectedKeyIndex];
                float outTangRad = Mathf.Atan(keyframe.outTangent);
                float inTangRad = Mathf.Atan(keyframe.inTangent);
                float diff = Mathf.Abs(outTangRad - inTangRad) * 0.5f * ((outTangRad > inTangRad) ? 1 : -1);
                outTangRad -= diff;
                inTangRad += diff;
                keyframe.inTangent = Mathf.Tan(inTangRad);
                keyframe.outTangent = Mathf.Tan(outTangRad);
                activeCurve.MoveKey(selectedKeyIndex, keyframe);
                lastSelectedOption = ContextOptions.freesmooth;
            }
            AlterCurveData(activeCurve);
        }

        public void FlatKey(bool addToUndoStack = false) {
            if (addToUndoStack) {
                TangentModeChange();
            }
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            ContextMenu contextMenu = contextMenuManager.dictCurvesContextMenus[activeCurve][selectedKeyIndex];
            if (!contextMenu.flat) {
                contextMenu.Reset();
                contextMenu.freeSmooth = true;
                contextMenu.flat = true;
                Keyframe keyframe = activeCurve.keys[selectedKeyIndex];
                keyframe.inTangent = 0;
                keyframe.outTangent = 0;
                activeCurve.MoveKey(selectedKeyIndex, keyframe);
                lastSelectedOption = ContextOptions.freesmooth;
            }
            AlterCurveData(activeCurve);
        }

        public void BrokenKey(bool addToUndoStack = false) {
            if (addToUndoStack) {
                TangentModeChange();
            }
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            ContextMenu contextMenu = contextMenuManager.dictCurvesContextMenus[activeCurve][selectedKeyIndex];
            if (!contextMenu.broken) {
                contextMenu.Reset();
                contextMenu.broken = true;
                lastSelectedOption = ContextOptions.broken;
                contextMenu.leftTangent.free = true;
                contextMenu.rightTangent.free = true;
                contextMenu.bothTangents.free = true;
            }
            AlterCurveData(activeCurve);
        }

        public void Free(TangentPart tangentPart, bool addToUndoStack = false) {
            if (addToUndoStack) {
                TangentModeChange();
            }
            ContextMenu contextMenu = InitTangentPart(tangentPart);
            if (tangentPart == TangentPart.Left) {
                contextMenu.leftTangent.free = true;
                contextMenu.bothTangents.free = contextMenu.rightTangent.free;
            } else if (tangentPart == TangentPart.Right) {
                contextMenu.rightTangent.free = true;
                contextMenu.bothTangents.free = contextMenu.leftTangent.free;
            } else if (tangentPart == TangentPart.Both) {
                contextMenu.bothTangents.free = true;
                contextMenu.leftTangent.free = true;
                contextMenu.rightTangent.free = true;
            }
            if (addToUndoStack)
            {
                AlterCurveData(ActiveCurveForm.SelectedCurve());
            }
        }

        public void Linear(TangentPart tangentPart, bool addToUndoStack = false) {
            if (addToUndoStack) {
                TangentModeChange();
            }
            ContextMenu contextMenu = InitTangentPart(tangentPart);
            if (tangentPart == TangentPart.Left) {
                contextMenu.leftTangent.linear = true;
                contextMenu.bothTangents.linear = contextMenu.rightTangent.linear;
            } else if (tangentPart == TangentPart.Right) {
                contextMenu.rightTangent.linear = true;
                contextMenu.bothTangents.linear = contextMenu.leftTangent.linear;
            } else if (tangentPart == TangentPart.Both) {
                contextMenu.bothTangents.linear = true;
                contextMenu.leftTangent.linear = true;
                contextMenu.rightTangent.linear = true;
            }
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            Keyframe keyframe = activeCurve.keys[selectedKeyIndex];
            if (contextMenu.leftTangent.linear && selectedKeyIndex > 0) {
                Keyframe keyframePrev = activeCurve.keys[selectedKeyIndex - 1];
                keyframe.inTangent = (keyframePrev.value - keyframe.value) / (keyframePrev.time - keyframe.time);
            }
            if (contextMenu.rightTangent.linear && (selectedKeyIndex < activeCurve.keys.Length - 1)) {
                Keyframe keyframeNext = activeCurve.keys[selectedKeyIndex + 1];
                keyframe.outTangent = (keyframeNext.value - keyframe.value) / (keyframeNext.time - keyframe.time);
            }
            activeCurve.MoveKey(selectedKeyIndex, keyframe);
            AlterCurveData(activeCurve);
        }

        public void Constant(TangentPart tangentPart, bool addToUndoStack = false) {
            if (addToUndoStack) {
                TangentModeChange();
            }
            ContextMenu contextMenu = InitTangentPart(tangentPart);
            if (tangentPart == TangentPart.Left) {
                contextMenu.leftTangent.constant = true;
                contextMenu.bothTangents.constant = contextMenu.rightTangent.constant;
            } else if (tangentPart == TangentPart.Right) {
                contextMenu.rightTangent.constant = true;
                contextMenu.bothTangents.constant = contextMenu.leftTangent.constant;
            } else if (tangentPart == TangentPart.Both) {
                contextMenu.bothTangents.constant = true;
                contextMenu.leftTangent.constant = true;
                contextMenu.rightTangent.constant = true;
            }
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            Keyframe keyframe = activeCurve.keys[selectedKeyIndex];
            if (contextMenu.leftTangent.constant && (selectedKeyIndex > 0)) {
                keyframe.inTangent = float.PositiveInfinity;
            }
            if (contextMenu.rightTangent.constant && (selectedKeyIndex < activeCurve.keys.Length - 1)) {
                keyframe.outTangent = float.PositiveInfinity;
            }
            activeCurve.MoveKey(selectedKeyIndex, keyframe);
            AlterCurveData(activeCurve);
        }

        public void Weighted(TangentPart tangentPart, bool addToUndoStack = false)
        {
            if (addToUndoStack)
            {
                TangentModeChange();
            }
            Free(tangentPart);
            var selectedCurve = ActiveCurveForm.SelectedCurve();
            ContextMenu contextMenu = contextMenuManager.dictCurvesContextMenus[selectedCurve][selectedKeyIndex];
            if (tangentPart == TangentPart.Left)
            {
                contextMenu.leftTangent.weighted = !contextMenu.leftTangent.weighted;
                contextMenu.bothTangents.weighted = contextMenu.rightTangent.weighted && contextMenu.leftTangent.weighted;
            }
            else if (tangentPart == TangentPart.Right)
            {
                contextMenu.rightTangent.weighted = !contextMenu.rightTangent.weighted;
                contextMenu.bothTangents.weighted = contextMenu.rightTangent.weighted && contextMenu.leftTangent.weighted;
            }
            else if (tangentPart == TangentPart.Both)
            {
                contextMenu.bothTangents.weighted = !contextMenu.bothTangents.weighted;
                contextMenu.leftTangent.weighted = contextMenu.bothTangents.weighted;
                contextMenu.rightTangent.weighted = contextMenu.bothTangents.weighted;
            }

            var keyframe = selectedCurve[selectedKeyIndex];
            if (contextMenu.bothTangents.weighted)
            {
                keyframe.weightedMode = WeightedMode.Both;
            } 
            else if (contextMenu.leftTangent.weighted)
            {
                keyframe.weightedMode = WeightedMode.In;
            }
            else if (contextMenu.rightTangent.weighted)
            {
                keyframe.weightedMode = WeightedMode.Out;
            }
            else
            {
                keyframe.weightedMode = WeightedMode.None;
            }
            selectedCurve.MoveKey(selectedKeyIndex, keyframe);

            if (addToUndoStack)
            {
                AlterCurveData(selectedCurve);
            }
        }

        public bool MousePosOverContextMenu(Vector2 mousePos) {
            return contextMenuUI.Hover(mousePos);
        }

        ContextMenu InitTangentPart(TangentPart tangentPart) {
            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            ContextMenu contextMenu = contextMenuManager.dictCurvesContextMenus[activeCurve][selectedKeyIndex];
            if (!contextMenu.broken) {
                contextMenu.Reset();
                contextMenu.broken = true;
                contextMenu.bothTangents.free = true;
                contextMenu.leftTangent.free = true;
                contextMenu.rightTangent.free = true;
            }
            if (tangentPart == TangentPart.Left) {
                contextMenu.leftTangent.Reset();
            } else if (tangentPart == TangentPart.Right) {
                contextMenu.rightTangent.Reset();
            } else if (tangentPart == TangentPart.Both) {
                contextMenu.leftTangent.Reset();
                contextMenu.rightTangent.Reset();
            }
            contextMenu.bothTangents.Reset();
            return contextMenu;
        }

        public void MouseUp() {
            ResetSelections();
        }

        void ResetSelections() {
            currentAxisLock = AxisLock.None; // 新增：松开鼠标结束拖拽时，彻底重置锁定状态
            if (lineDragged) {
                lineDragged = false;
                showCursorNormal = true;
                Cursor.SetCursor(TextureDefault, Vector2.zero, CursorMode.ForceSoftware);
                AnimationCurve curve = ActiveCurveForm.SelectedCurve();
                List<float> keyDiffs = new List<float>();
                bool significantDiff = false;//need to know if to add operation do undo/redo stack
                int i = 0;
                float minSignificantDiff = GradRect.height * marginErr;
                foreach (Keyframe keyframe in curve.keys) {
                    float diff = keyframe.value - lineDragKeysStartingValue[i];
                    if (!significantDiff) {
                        significantDiff = Mathf.Abs(diff) > minSignificantDiff;
                    }
                    keyDiffs.Add(diff);
                    i += 1;
                }
                if (significantDiff)
                {
                    undoRedo.AddOperation(new MoveOperation(this, keyDiffs));
                }
            } else if (keyDragged) {
                keyDragged = false;
                Keyframe keyframe = ActiveCurveForm.SelectedCurve()[selectedKeyIndex];
                Vector2 keyDiff = new Vector2(keyframe.time, keyframe.value) - selectedKeyStartingPos;
                Vector2 minSignificantDiff = new Vector2(GradRect.width, GradRect.height) * marginErr;
                if ((keyDiff.x > minSignificantDiff.x) || (keyDiff.y > minSignificantDiff.y)) {
                    undoRedo.AddOperation(new MoveOperation(this, selectedKeyIndex, keyDiff));
                }
                keyValue.SetLabelEnabled(false);
            } else if (isTangentSelected) {
                isTangentSelected = false;
            } else if (multipleKeysMove) {
                multipleKeysMove = false;
                AnimationCurve curve = ActiveCurveForm.SelectedCurve();
                List<int> selectedKeyIndices = multipleKeySelection.SelectedKeyIndices();
                List<Vector2> keyDiffs = new List<Vector2>(selectedKeyIndices.Count);
                int i = 0;
                bool significantDiff = false;//need to know if to add operation do undo/redo stack
                Vector2 minSignificantDiff = new Vector2(GradRect.width, GradRect.height) * marginErr;
                foreach (int index in selectedKeyIndices) {
                    Keyframe keyframe = curve[index];
                    Vector2 keyDiff = new Vector2(keyframe.time, keyframe.value) - selectedKeysStartingPos[i];
                    if (!significantDiff) {
                        significantDiff = (keyDiff.x > minSignificantDiff.x) || (keyDiff.y > minSignificantDiff.y);
                    }
                    keyDiffs.Add(keyDiff);
                    i += 1;
                }
                if (significantDiff) {
                    undoRedo.AddOperation(new MoveOperation(this, new List<int>(selectedKeyIndices), keyDiffs));
                }
            } else if (multipleKeysResize != ResizePart.None) {
                multipleKeysResize = ResizePart.None;
                //TODO check if that significantDiff might be needed
            }
        }

        public void UpdateContextMenus(int newIndex, int index) {
            KeyHandling.UpdateContextMenus(newIndex, index);
        }

        void UpdateClampedAutoTangents(AnimationCurve curve, int selectedKey) {
            Keyframe keyframe = curve.keys[selectedKey];
            if ((selectedKey > 0) && (selectedKey < curve.keys.Length - 1)) {
                Keyframe keyframePrev = curve.keys[selectedKey - 1];
                Keyframe keyframeNext = curve.keys[selectedKey + 1];
                if (((keyframePrev.value < keyframe.value) && (keyframe.value < keyframeNext.value)) || ((keyframeNext.value < keyframe.value) && (keyframe.value < keyframePrev.value))) {
                    keyframe.inTangent = Mathf.Sqrt((keyframePrev.value - keyframe.value) * (keyframe.value - keyframeNext.value)) * 2f / (keyframeNext.time - keyframePrev.time);
                } else {
                    keyframe.inTangent = 0;
                }
                keyframe.outTangent = keyframe.inTangent;
            } else if (curve.keys.Length >= 2) {
                if (selectedKey == 0) {
                    keyframe.outTangent = 0;
                } else if (selectedKey == curve.keys.Length - 1) {
                    keyframe.inTangent = 0;
                }
            }
            curve.MoveKey(selectedKey, keyframe);
        }

        void UpdateLegacyAutoTangents(AnimationCurve curve, int selectedKey) {
            Keyframe keyframe = curve.keys[selectedKey];
            if (selectedKey > 0 && (selectedKey < curve.keys.Length - 1)) {
                Keyframe keyframePrev = curve.keys[selectedKey - 1];
                Keyframe keyframeNext = curve.keys[selectedKey + 1];
                float tangPrev = (keyframe.value - keyframePrev.value) / (keyframe.time - keyframePrev.time);
                float tangNext = (keyframe.value - keyframeNext.value) / (keyframe.time - keyframeNext.time);
                keyframe.inTangent = (tangPrev + tangNext) * 0.5f;
                keyframe.outTangent = keyframe.inTangent;
            } else if (curve.keys.Length >= 2) {
                if (selectedKey == 0) {
                    Keyframe keyframeNext = curve.keys[selectedKey + 1];
                    keyframe.outTangent = (keyframe.value - keyframeNext.value) / (keyframe.time - keyframeNext.time);
                } else if (selectedKey == curve.keys.Length - 1) {
                    Keyframe keyframePrev = curve.keys[selectedKey - 1];
                    keyframe.inTangent = (keyframePrev.value - keyframe.value) / (keyframePrev.time - keyframe.time);
                }
            }
            curve.MoveKey(selectedKey, keyframe);
        }

        public void UpdateLinearTangent(AnimationCurve activeCurve, int keyIndex, bool leftTangent = false) {
            Keyframe keyframe = activeCurve.keys[keyIndex];
            if (leftTangent) {
                Keyframe keyframePrev = activeCurve.keys[keyIndex - 1];
                keyframe.inTangent = (keyframePrev.value - keyframe.value) / (keyframePrev.time - keyframe.time);
            } else {
                Keyframe keyframeNext = activeCurve.keys[keyIndex + 1];
                keyframe.outTangent = (keyframeNext.value - keyframe.value) / (keyframeNext.time - keyframe.time);
            }
            activeCurve.MoveKey(keyIndex, keyframe);
        }

        public void UpdateAutoLinearSideEffects() {//TODO should be temporary
            KeyHandling.UpdateAutoLinearSideEffects(selectedKeyIndex);
        }

        public void MouseDrag(Vector2 diff) {

            // ---------------- 修改：按住 Alt 智能锁定拖拽轴向 ----------------
            bool ctrlHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (ctrlHeld)
            {
                // 当还没确定锁定方向时，根据这一帧的移动趋势(diff)来决定并锁定
                if (currentAxisLock == AxisLock.None)
                {
                    if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
                    {
                        currentAxisLock = AxisLock.Horizontal; // 横向移动幅度更大，锁定为横向
                    }
                    else if (Mathf.Abs(diff.y) > Mathf.Abs(diff.x))
                    {
                        currentAxisLock = AxisLock.Vertical;   // 纵向移动幅度更大，锁定为纵向
                    }
                }

                // 根据已经确定的锁定方向，强行过滤掉另一个方向的增量
                if (currentAxisLock == AxisLock.Horizontal)
                {
                    diff.y = 0f;
                }
                else if (currentAxisLock == AxisLock.Vertical)
                {
                    diff.x = 0f;
                }
            }
            else
            {
                // 如果用户在中途松开了 Ctrl 键，立刻解除锁定
                currentAxisLock = AxisLock.None;
            }

            AnimationCurve activeCurve = ActiveCurveForm.SelectedCurve();
            if (activeCurve != null) {
                List<ContextMenu> listContextMenus = contextMenuManager.dictCurvesContextMenus[activeCurve];
                if (isTangentSelected || keyDragged) {
                    Keyframe keyframe = activeCurve[selectedKeyIndex];
                    Vector2 keyframePos = Utils.Convert(new Vector2(keyframe.time, keyframe.value), EntireRect, gradRect);
                    if (isTangentSelected) {//if any tangent is selected
                        Vector2 mousePos = curveWindow.CursorPos();
                        float ratio = gradRect.height * EntireRect.width / (gradRect.width * EntireRect.height);

                        ContextMenu contextMenu = listContextMenus[selectedKeyIndex];
                        if (contextMenu.auto || contextMenu.clampedAuto) {
                            contextMenu.Reset();
                            contextMenu.freeSmooth = true;
                        }
                        if (leftTangentSelected) {
                            if (keyframePos.x - mousePos.x < marginErr) {
                                keyframe.inTangent = float.PositiveInfinity;
                            } else {
                                keyframe.inTangent = ratio * (mousePos.y - keyframePos.y) / (mousePos.x - keyframePos.x);
                            }
                            if (contextMenu.freeSmooth) {
                                keyframe.outTangent = keyframe.inTangent;
                                contextMenu.flat = keyframe.inTangent == 0;
                            }

                            if (contextMenu.leftTangent.weighted)
                            {
                                var keyframeLeft = activeCurve[selectedKeyIndex - 1];
                                var keyframePosLeft = Utils.Convert(new Vector2(keyframeLeft.time, keyframeLeft.value), EntireRect, gradRect);
                                if (mousePos.x < keyframePosLeft.x)
                                {
                                    mousePos.y = keyframePos.y + (keyframePosLeft.x - keyframePos.x) / (mousePos.x - keyframePos.x) * (mousePos.y - keyframePos.y);
                                    mousePos.x = keyframePosLeft.x;
                                }
                                keyframe.inWeight = (keyframePos - mousePos).magnitude * Curves.WEIGHTED_RATIO / (keyframePos - keyframePosLeft).magnitude;
                            }
                        } else {//TODO it duplicates the above branch
                            if (mousePos.x - keyframePos.x < marginErr) {
                                keyframe.outTangent = float.PositiveInfinity;
                            } else {
                                keyframe.outTangent = ratio * (mousePos.y - keyframePos.y) / (mousePos.x - keyframePos.x);
                            }
                            if (contextMenu.freeSmooth) {
                                keyframe.inTangent = keyframe.outTangent;
                                contextMenu.flat = keyframe.outTangent == 0;
                            }

                            if (contextMenu.rightTangent.weighted)
                            {
                                var keyframeRight = activeCurve[selectedKeyIndex + 1];
                                var keyframePosRight = Utils.Convert(new Vector2(keyframeRight.time, keyframeRight.value), EntireRect, gradRect);
                                if (keyframePosRight.x < mousePos.x)
                                {
                                    mousePos.y = keyframePos.y + (keyframePosRight.x - keyframePos.x) / (mousePos.x - keyframePos.x) * (mousePos.y - keyframePos.y);
                                    mousePos.x = keyframePosRight.x;
                                }
                                keyframe.outWeight = (keyframePos - mousePos).magnitude * Curves.WEIGHTED_RATIO / (keyframePos - keyframePosRight).magnitude;
                            }
                        }
                        activeCurve.MoveKey(selectedKeyIndex, keyframe);
                    } else if (keyDragged) {//if any key is selected   
                        selectedKeyIndex = KeyHandling.MoveKey(selectedKeyIndex, diff);
                        if (selectedKeyIndex == KeyHandling.UNSELECTED)
                        {
                            keyDragged = false;
                        }
                        else
                        {//if key still selected
                            keyValue.SetTimeValueText(keyframe.time, keyframe.value);
                            keyValue.SetLabelPos(keyframePos);
                        }
                    }
                    AlterCurveData(activeCurve);
                } else if (lineDragged) {//if any curve is selected
                    if (showCursorNormal && EntireRect.Contains(curveWindow.CursorPos())) {
                        showCursorNormal = false;
                        Cursor.SetCursor(TextureNS, CurveWindow.hotspot, CursorMode.ForceSoftware);
                    } else if (!showCursorNormal && !EntireRect.Contains(curveWindow.CursorPos())) {
                        showCursorNormal = true;
                        //Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
                        Cursor.SetCursor(TextureDefault, Vector2.zero, CursorMode.ForceSoftware);
                    }
                    diff.x = 0;
                    for (int i = 0; i < activeCurve.length; ++i) {
                        KeyHandling.MoveKey(i, diff);
                    }
                    AlterCurveData(activeCurve);
                } else if (multipleKeysMove) {
                    List<int> selectedKeyIndices = multipleKeySelection.SelectedKeyIndices();
                  //  if (((activeCurve[selectedKeyIndices[0]].time > CurveForm.X_MIN) || (diff.x > 0)) && ((activeCurve[selectedKeyIndices[selectedKeyIndices.Count - 1]].time < CurveForm.X_MAXIM) || (diff.x < 0)))
                    {
                       // Debug.Log("++++" + CurveForm.X_MAXIM);
                        bool revOrder = false;
                        if ((selectedKeyIndices.Count > 1) && (diff.x > Mathf.Epsilon))
                        {
                            //just do the movement of the keys, from the last to first, to avoid the change of order of the selected keys 
                            revOrder = true;
                        }

                        int length = activeCurve.length;
                        for (int i = 0; i < selectedKeyIndices.Count; ++i)
                        {
                            int ii = revOrder ? (selectedKeyIndices.Count - i - 1) : i;
                            int index = selectedKeyIndices[ii];
                            int newIndex = KeyHandling.MoveKey(index, diff);

                            if ((i == 0) && !revOrder && (activeCurve.length < length))
                            {
                                //case when the first key of a curve is deleted because it's overlapped by the moving neighbour
                                //so, shift all the other selected keys
                                for (int j = 0; j < selectedKeyIndices.Count; ++j)
                                {
                                    selectedKeyIndices[j] -= 1;
                                }

                                /// ask for this particular update
                                Curves.TriggerUpdateCurve(activeCurve);
                                DrawCurves();//TODO try to do update just the 'selected knots' list
                                selectedKeyIndex = KeyHandling.UNSELECTED;
                            }
                            KeyHandling.CheckMovingBeyond(index, newIndex, selectedKeyIndex, selectedKeyIndices, ii);
                        }
                        multipleKeySelection.UpdateSelectedKnots(GridRect);
                        AlterCurveData(activeCurve);
                    }
                } else if (multipleKeysResize != ResizePart.None) {
                    List<int> selectedKeyIndices = multipleKeySelection.SelectedKeyIndices();

                    bool revOrder = false;
                    if ((selectedKeyIndices.Count > 1) && (diff.x > Mathf.Epsilon))
                    {
                        //just do the movement of the keys, from the last to first, to avoid the change of order of the selected keys 
                        revOrder = true;
                    }

                    if ((multipleKeysResize == ResizePart.Left) || (multipleKeysResize == ResizePart.Right))
                    {
                        bool leftPivot = multipleKeysResize == ResizePart.Right;
                        int pivotIndex = selectedKeyIndices[leftPivot ? 0 : (selectedKeyIndices.Count - 1)];
                        int movingKeyIndex = selectedKeyIndices[leftPivot ? (selectedKeyIndices.Count - 1) : 0];
                        const float LIMIT = 0.1f;

                        float timeDiff = activeCurve[movingKeyIndex].time - activeCurve[pivotIndex].time;
                        bool resizeable = (pivotIndex < movingKeyIndex) ? ((timeDiff > LIMIT) || (diff.x >= 0)) : ((-timeDiff > LIMIT) || (diff.x <= 0));

                        if(resizeable)
                        {
                            int length = activeCurve.length;
                            for (int i = 0; i < selectedKeyIndices.Count; ++i)
                            {
                                int ii = revOrder ? (selectedKeyIndices.Count - i - 1) : i;
                                int index = selectedKeyIndices[ii];
                                if (index == pivotIndex)
                                {
                                    continue;//don't move the pivot
                                }
                                float ratio = (activeCurve[index].time - activeCurve[pivotIndex].time) / (activeCurve[movingKeyIndex].time - activeCurve[pivotIndex].time);
                                
                                int newIndex = KeyHandling.MoveKey(index, new Vector2(diff.x * ratio, 0));
                                if ((i == 0) && !revOrder && (activeCurve.length < length))
                                {
                                    //case when the first key of a curve is deleted because it's overlapped by the moving neighbour
                                    //so, shift all the other selected keys
                                    for (int j = 1; j < selectedKeyIndices.Count; ++j)
                                    {
                                        selectedKeyIndices[j] -= 1;
                                    }
                                }
                                KeyHandling.CheckMovingBeyond(index, newIndex, selectedKeyIndex, selectedKeyIndices, ii);
                            }
                        }
                    } else {
                        int pivotIndex = selectedKeyIndices[0];
                        int movingIndex = selectedKeyIndices[0];
                        for (int i = 1; i < selectedKeyIndices.Count; ++i)
                        {
                            if ((multipleKeysResize == ResizePart.Top) ^ (activeCurve[pivotIndex].value < activeCurve[selectedKeyIndices[i]].value))
                            {
                                pivotIndex = selectedKeyIndices[i];
                            }

                            if ((multipleKeysResize == ResizePart.Bottom) ^ (activeCurve[movingIndex].value < activeCurve[selectedKeyIndices[i]].value))
                            {
                                movingIndex = selectedKeyIndices[i];
                            }
                        }

                        for (int i = 0; i < selectedKeyIndices.Count; ++i)
                        {
                            int index = selectedKeyIndices[i];
                            float ratio = (activeCurve[index].value - activeCurve[pivotIndex].value) / (activeCurve[movingIndex].value - activeCurve[pivotIndex].value);
                            KeyHandling.MoveKey(index, new Vector2(0, diff.y * ratio));
                        }
                    }

                    multipleKeySelection.UpdateSelectedKnots(GridRect);
                    AlterCurveData(activeCurve);
                }
            }
        }

        public void ChangeKeyValue(float value) {
            ChangeKeyFramePosition();
        }

        public void ChangeKeyTime(float time) {
            ChangeKeyFramePosition();
        }

        void ChangeKeyFramePosition() {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
                AnimationCurve curve = ActiveCurveForm.SelectedCurve();
                Keyframe keyframe = curve[selectedKeyIndex];
                float time;
                float value;
                keyValue.ReadKeyFrameValues(out time, out value);
                Vector2 diff = new Vector2(time - keyframe.time, value - keyframe.value);
                keyframe.time = time;
                keyframe.value = value;
                selectedKeyIndex = KeyHandling.MoveKey(curve, selectedKeyIndex, keyframe);
                undoRedo.AddOperation(new MoveOperation(this, selectedKeyIndex, diff));
                keyValue.SetKeyEditEnabled(false);
                AlterCurveData(curve);
            } else {
                keyValue.SetKeyEditEnabled(false);
            }
        }

        void DrawCurves()
        {
            AnimationCurve activeCurve = ActiveCurveForm.curve1;
            if (activeCurve != null)
            {
                foreach (CurveForm curveForm in curveFormList)
                {
                    if (curveForm.curve1 == ActiveCurveForm.curve1)
                    {
                        continue;
                    }
                    // ✨ 新增拦截：不可见的曲线不参与渲染
                    if (!curveForm.isVisible) continue;

                    Curves.DrawCurveForm(curveForm.shadyColor, curveForm.curve1, curveForm.curve2, false, false, selectedKeyIndex, EntireRect, adjGridRect, curveForm.gradRect);
                }

                // ✨ 新增拦截：当前激活的曲线如果被隐藏了，也不渲染
                if (ActiveCurveForm.isVisible)
                {
                    Curves.DrawCurveForm(ActiveCurveForm.color, ActiveCurveForm.curve1, ActiveCurveForm.curve2, ActiveCurveForm.firstCurveSelected, !ActiveCurveForm.firstCurveSelected, selectedKeyIndex, EntireRect, adjGridRect, gradRect);
                }
            }
        }

        //for the given vertical interval and ratio between grid height and unit height, calculate:
        //intervalInt the number of horizontal lines to be drawn in the grid,
        //rezid which is the percentage to the grid height of the difference between the max value and the highest milestone (e.g. 1.7 to 1.5 or 1.7 to 1.0)
        //prev rezid and prev intervalInt are needed to know the density of the vertical gradations
        void GetHorLinesCountAndRezid(float interval, float ratio, out float rezid, out float prevRezid, out int intervalInt, out int prevIntervalInt) {
            rezid = 0;
            prevRezid = 0;
            intervalInt = 0;
            prevIntervalInt = 0;
            if (interval > 0) { //interval should allways be positive
                if (interval >= 10) {
                    GetHorLinesCountAndRezid(interval / 10.0f, ratio, out rezid, out prevRezid, out intervalInt, out prevIntervalInt);
                } else if (interval < 1) {
                    GetHorLinesCountAndRezid(interval * 10.0f, ratio, out rezid, out prevRezid, out intervalInt, out prevIntervalInt);
                } else {
                    intervalInt = Mathf.FloorToInt(interval);
                    if (!Mathf.Approximately(intervalInt, interval)) {
                        rezid = (interval - intervalInt) / interval;
                    }
                    mMidHor = true;
                    prevRezid = rezid;
                    prevIntervalInt = intervalInt;

                    while (ratio >= intervalInt) {
                        prevIntervalInt = intervalInt;
                        intervalInt *= mMidHor ? 2 : 5;
                        mMidHor = !mMidHor;
                    }
                    mMidHor = !mMidHor;

                    bool intervalModified = prevIntervalInt != intervalInt;
                    CalculateExtraLines(ref rezid, ref intervalInt);
                    if (intervalModified) {
                        CalculateExtraLines(ref prevRezid, ref prevIntervalInt);
                    } else {
                        prevRezid = rezid;
                        prevIntervalInt = intervalInt;
                    }
                }
            }
        }

        void CalculateExtraLines(ref float rezid, ref int intervalInt) {
            float percentSample = (1f - rezid) / intervalInt;
            if (rezid > percentSample) {
                float floatExtraLines = rezid / percentSample + marginErr;//add an error margin , e.g. 4 might pe represented like 3.999 etc...
                int extralines = (int)floatExtraLines;
                rezid -= extralines * percentSample;
                if (rezid < marginErr) {
                    rezid = 0;
                }
                intervalInt += extralines;
            }
        }

        //below are the methods which deal with PersistenceManager
        public void SaveData(string configName, Object obj) {
            PersistenceManager.SaveData(configName, obj, curveWindow, curveFormList, ActiveCurveForm, contextMenuManager.dictCurvesContextMenus);
            AlteredData = false;
        }

        public void LoadData(string configName, Object obj) {
            RemoveData();
            PersistenceManager.LoadData(configName, obj, curveWindow, this, curveFormList, contextMenuManager.dictCurvesContextMenus);
            WindowClosed = curveWindow.WindowClosed;
            AlteredData = false;
        }

        void RemoveData() {
            selectedKeyIndex = -1;
            curveFormList.Clear();
            contextMenuManager.dictCurvesContextMenus.Clear();
            usedColorList.Clear();
            FillListColor();
            ActiveCurveForm = new CurveForm();
            GradRect = ActiveCurveForm.gradRect;
        }

        public void NewWindow() {
            RemoveData();
            curveWindow.transform.localPosition = Vector3.zero;
            curveWindow.GetComponent<RectTransform>().sizeDelta = DEFAULT_WINDOW_SIZE;
            PersistenceManager.RemoveLastFileKey();
            PersistenceManager.Reset(curveWindow, this);
            AlteredData = false;
        }

        public List<string> GetNamesList() {
            return PersistenceManager.GetNamesList();
        }

        public void DeleteFile(string name) {
            PersistenceManager.DeleteFile(name);
        }

        public string GetLastFile() {
            return PersistenceManager.GetLastFile();
        }

        public int GetSelectedIndex() {
            return selectedKeyIndex;
        }

        public void SelectKey(int keyIndex) {
            selectedKeyIndex = keyIndex;
            AlterCurveData(ActiveCurveForm.SelectedCurve());
        }

        public ContextMenuManager GetContextMenuManager() {
            return contextMenuManager;
        }

        public void ResetKeyDragged() {
            keyDragged = false;
        }

        public void ClearMultipleKeySelection() {
            multipleKeySelection.ClearMultipleKeySelection();
        }

        public void SetOnAlterDelegate(System.Action onAlterAction) {
            this.onAlterAction = onAlterAction;
        }

        public void AlterData() {
            if (onAlterAction != null) {
                if (!AlteredData) {
                    AlteredData = true;
                    onAlterAction();
                }
            }
        }

        void AlterCurveData(AnimationCurve curve)
        {
            AlterData();
            Curves.TriggerUpdateCurve(curve);
        }
    }
}