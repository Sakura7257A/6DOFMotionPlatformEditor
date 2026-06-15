/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：PlatformModelManager.cs
 * 作者：LeonLiu (AI Assisted)
 * 日期：2026/2/1
 * 功能：3自由度/6自由度兼容平台模型管理器
*************************************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MPE
{
    public class PlatformModelManager : MonoBehaviour
    {
        public enum PlatformType { DOF3, DOF6 }

        [Header("System Mode")]
        public PlatformType currentPlatformType = PlatformType.DOF3;

        [Header("Settings")]
        public string targetLayerName = "Triangle";
        public Material materialTriangleA; // 顶部平台(动)材质
        public Material materialTriangleB; // 底部平台(静)材质

        [Header("Text Settings")]
        public Color textColor = Color.yellow;
        public float textScale = 0.2f;

        [Header("3DOF Triangle Edges (Meters)")]
        [Min(0.1f)] public float a = 3f;
        [Min(0.1f)] public float b = 4f;
        [Min(0.1f)] public float c = 5f;

        [Header("6DOF Settings (Edges in mm)")]
        [Tooltip("底座短边长度 (毫米)")]
        public float baseShortEdge = 1000f;
        [Tooltip("底座长边长度 (毫米)")]
        public float baseLongEdge = 4000f;

        [Tooltip("顶部短边长度 (毫米)")]
        public float topShortEdge = 800f;
        [Tooltip("顶部长边长度 (毫米)")]
        public float topLongEdge = 3000f;

        [Tooltip("顶部平台朝向偏移 (60度可让顶部短边对准底部长边，形成交叉)")]
        public float topPhaseOffset = 60f;

        [Header("Vertical Offset")]
        public float height = 3f;

        [Header("Read Only Data (Output)")]
        // 最大支持 6 根缸长数据导出
        public float[] linkDistances = new float[6];

        public bool createState = false;

        public GameObject triangleA;
        public GameObject triangleB;
        Mesh meshA;
        Mesh meshB;

        LineRenderer[] links = new LineRenderer[6];
        TextMesh[] distanceLabels = new TextMesh[6];

        // ✨ 核心修改：6DOF的上下平台形状不同，必须用两个数组分别记录锚点
        Vector3[] localVerticesA;
        Vector3[] localVerticesB;

        float lastA, lastB, lastC, lastHeight;
        float lastTShort, lastTLong, lastBShort, lastBLong, lastPhaseOffset;
        Material lastMaterialA;
        Material lastMaterialB;

        void Update()
        {
            if (createState && (triangleA == null || triangleB == null))
            {
                createState = false;
                return;
            }

            if (createState)
            {
                // 检测尺寸是否发生变化，自动重建网格
                bool needsRebuild = false;
                if (currentPlatformType == PlatformType.DOF3)
                {
                    if (a != lastA || b != lastB || c != lastC) needsRebuild = true;
                }
                else
                {
                    if (topShortEdge != lastTShort || topLongEdge != lastTLong ||
                        baseShortEdge != lastBShort || baseLongEdge != lastBLong ||
                        topPhaseOffset != lastPhaseOffset) needsRebuild = true;
                }

                if (needsRebuild)
                {
                    RebuildTriangles();
                }

                // ⛔🔥 核心修复：删除了 triangleA.transform.localPosition = Vector3.up * height;
                // 平台的移动权彻底交还给 MPEManager，模型层不再干涉位置！

                // 检测材质变化
                if (materialTriangleA != lastMaterialA || materialTriangleB != lastMaterialB)
                {
                    UpdateMaterials();
                    lastMaterialA = materialTriangleA;
                    lastMaterialB = materialTriangleB;
                }

                // ⛔🔥 将 UpdateLinksAndLabels() 从这里移走
            }
        }

        // ✨ 新增：使用 LateUpdate 确保连杆和数值的绘制，永远在 MPEManager 移动平台【之后】进行
        void LateUpdate()
        {
            if (createState)
            {
                // 每帧最后时刻更新连杆位置和缸长显示，实现完美贴合随动
                UpdateLinksAndLabels();
            }
        }
        public void Cleanup()
        {
            // 修复潜在的内存泄漏：销毁网格和材质引用
            if (meshA != null) Destroy(meshA);
            if (meshB != null) Destroy(meshB);

            if (triangleA) Destroy(triangleA);
            if (triangleB) Destroy(triangleB);

            var oldChildren = transform.GetComponentsInChildren<Transform>();
            foreach (var child in oldChildren)
            {
                if (child != null && child.gameObject.name.StartsWith("Link_"))
                    Destroy(child.gameObject);
            }

            links = new LineRenderer[6];
            distanceLabels = new TextMesh[6];
            linkDistances = new float[6];
        }

        public void InitializeSystem()
        {
            int layerID = GetLayerID();

            CreateTriangle(ref triangleA, ref meshA, "TriangleA", Vector3.up * height, layerID, materialTriangleA);
            CreateTriangle(ref triangleB, ref meshB, "TriangleB", Vector3.zero, layerID, materialTriangleB);

            CreateLinks(layerID);
            RebuildTriangles();
            createState = true;

            lastMaterialA = materialTriangleA;
            lastMaterialB = materialTriangleB;
        }

        int GetLayerID() => LayerMask.NameToLayer(targetLayerName) == -1 ? 0 : LayerMask.NameToLayer(targetLayerName);

        void CreateTriangle(ref GameObject go, ref Mesh mesh, string name, Vector3 localPos, int layer, Material mat)
        {
            go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mesh = new Mesh();
            mf.sharedMesh = mesh;
            ApplyMaterial(mr, mat);
        }

        void ApplyMaterial(MeshRenderer mr, Material targetMat)
        {
            if (targetMat != null) mr.sharedMaterial = targetMat;
            else
            {
                Material defaultMat = new Material(Shader.Find("Standard"));
                mr.material = defaultMat;
            }
        }

        void UpdateMaterials()
        {
            if (triangleA != null) ApplyMaterial(triangleA.GetComponent<MeshRenderer>(), materialTriangleA);
            if (triangleB != null) ApplyMaterial(triangleB.GetComponent<MeshRenderer>(), materialTriangleB);
        }

        void CreateLinks(int layer)
        {
            // 动态判断需要生成几根液压缸
            int linkCount = currentPlatformType == PlatformType.DOF3 ? 3 : 6;
            if (linkDistances == null || linkDistances.Length != 6) linkDistances = new float[6];

            for (int i = 0; i < linkCount; i++)
            {
                GameObject linkObj = new GameObject("Link_" + i);
                linkObj.layer = layer;
                linkObj.transform.SetParent(transform, false);

                LineRenderer lr = linkObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.widthMultiplier = 0.05f;
                lr.useWorldSpace = false;
                lr.material = new Material(Shader.Find("Unlit/Color"));
                lr.material.color = Color.green;
                links[i] = lr;

                GameObject textObj = new GameObject("Label");
                textObj.transform.SetParent(linkObj.transform, false);
                TextMesh tm = textObj.AddComponent<TextMesh>();
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.characterSize = 0.1f;
                tm.fontSize = 60;
                tm.color = textColor;
                distanceLabels[i] = tm;
            }
        }

        void RebuildTriangles()
        {
            if (currentPlatformType == PlatformType.DOF3)
            {
                // 3DOF 逻辑
                localVerticesA = CalculateCenteredTriangleVertices(a, b, c);
                localVerticesB = localVerticesA; // 3DOF上下形状一致
                BuildMesh(meshA, localVerticesA);
                BuildMesh(meshB, localVerticesB);
            }
            else
            {
                // 6DOF 逻辑：先将毫米转换为米
                float tShortM = topShortEdge / 1000f;
                float tLongM = topLongEdge / 1000f;
                float bShortM = baseShortEdge / 1000f;
                float bLongM = baseLongEdge / 1000f;

                // 计算动平台 (Top / A) 的半径与夹角并生成
                float topRadius = Mathf.Sqrt((tShortM * tShortM + tLongM * tLongM + tShortM * tLongM) / 3f);
                float topJointAngle = Mathf.Asin(tShortM / (2f * topRadius)) * Mathf.Rad2Deg;
                localVerticesA = CalculateStewartHexagonVertices(topRadius, topJointAngle, topPhaseOffset);
                BuildMesh(meshA, localVerticesA);

                // 计算静平台 (Base / B) 的半径与夹角并生成
                float baseRadius = Mathf.Sqrt((bShortM * bShortM + bLongM * bLongM + bShortM * bLongM) / 3f);
                float baseJointAngle = Mathf.Asin(bShortM / (2f * baseRadius)) * Mathf.Rad2Deg;
                localVerticesB = CalculateStewartHexagonVertices(baseRadius, baseJointAngle, 0f);
                BuildMesh(meshB, localVerticesB);
            }

            // 更新缓存记录，用于检测变化
            lastA = a; lastB = b; lastC = c; lastHeight = height;
            lastTShort = topShortEdge; lastTLong = topLongEdge;
            lastBShort = baseShortEdge; lastBLong = baseLongEdge;
            lastPhaseOffset = topPhaseOffset;
        }

        // ✨ 新增：运动学几何换算算法 (利用勾股定理，把设定的缸长转换为平台在3D空间中的垂直高度)
        public float GetInitialHeightFromCylinderLength(float cylinderLength)
        {
            if (currentPlatformType == PlatformType.DOF3)
            {
                // 3DOF 的缸是直上直下的，所以 垂直高度 = 缸长
                return cylinderLength;
            }
            else
            {
                // 6DOF 必须有顶点数据才能算水平偏差
                if (localVerticesA == null || localVerticesB == null || localVerticesA.Length < 6 || localVerticesB.Length < 6)
                    return cylinderLength;

                // 取0号腿的动平台锚点 和 静平台锚点 (6DOF交叉结构下0连1)
                Vector3 pTop = localVerticesA[0];
                Vector3 pBase = localVerticesB[1];

                // 计算水平方向的跨度距离平方 (dx^2 + dz^2)
                float dx = pTop.x - pBase.x;
                float dz = pTop.z - pBase.z;
                float horizontalDistSq = dx * dx + dz * dz;

                // 物理设定的缸长平方
                float cylinderSq = cylinderLength * cylinderLength;

                // 防呆保护：如果输入的缸长比水平跨度还要短，物理上根本连不上，返回一个安全极小值
                if (cylinderSq < horizontalDistSq)
                {
                    Debug.LogWarning("设定的初始缸长过短，物理上无法跨越当前设定的平台长短边间距！");
                    return 0.1f;
                }

                // 勾股定理求直角边： 垂直高度 = √(斜边² - 底边²)
                return Mathf.Sqrt(cylinderSq - horizontalDistSq);
            }
        }
        void BuildMesh(Mesh mesh, Vector3[] vertices)
        {
            if (mesh == null || vertices == null || vertices.Length == 0) return;
            mesh.Clear();
            mesh.vertices = vertices;

            if (vertices.Length == 3)
            {
                // 3自由度的三角形画法
                mesh.triangles = new int[] { 0, 1, 2, 0, 2, 1 };
            }
            else if (vertices.Length == 6)
            {
                // 6自由度的六边形画法
                mesh.triangles = new int[] {
                    0, 1, 2,   0, 2, 3,   0, 3, 4,   0, 4, 5,
                    0, 2, 1,   0, 3, 2,   0, 4, 3,   0, 5, 4
                };
            }

            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        Vector3[] CalculateCenteredTriangleVertices(float a, float b, float c)
        {
            Vector3 p0 = Vector3.zero;
            Vector3 p1 = new Vector3(c, 0, 0);

            float x = (b * b + c * c - a * a) / (2 * c);
            float math_y = Mathf.Sqrt(Mathf.Max(0, b * b - x * x));
            Vector3 p2 = new Vector3(x, 0, math_y);

            float centerX = c / 2f;
            float centerZ = 0f;

            if (math_y > 0.001f)
            {
                centerZ = (x * x - x * c + math_y * math_y) / (2f * math_y);
            }

            Vector3 center = new Vector3(centerX, 0, centerZ);

            p0 -= center;
            p1 -= center;
            p2 -= center;

            return new Vector3[] { p0, p1, p2 };
        }

        Vector3[] CalculateStewartHexagonVertices(float radius, float jointAngle, float phaseOffset)
        {
            Vector3[] vertices = new Vector3[6];
            float[] baseAngles = { 0f, 120f, 240f };

            for (int i = 0; i < 3; i++)
            {
                float centerAngle = baseAngles[i] + phaseOffset;
                float rad1 = (centerAngle - jointAngle) * Mathf.Deg2Rad;
                float rad2 = (centerAngle + jointAngle) * Mathf.Deg2Rad;

                vertices[i * 2] = new Vector3(Mathf.Cos(rad1) * radius, 0, Mathf.Sin(rad1) * radius);
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(rad2) * radius, 0, Mathf.Sin(rad2) * radius);
            }
            return vertices;
        }

        void UpdateLinksAndLabels()
        {
            if (triangleA == null || triangleB == null) return;
            if (linkDistances == null || linkDistances.Length != 6) linkDistances = new float[6];

            Camera cam = Camera.current;
            if (cam == null) cam = Camera.main;

            int linkCount = localVerticesA != null ? localVerticesA.Length : 0;
            string[] prefixes = { "A", "B", "C", "D", "E", "F" };

            // ✨ 核心修改 1：获取全局的安全极值（米）
            // 初始缸长即为最短收缩距离，初始缸长+最大行程即为最长拉伸距离
            // 加入 0.002f (2毫米) 的容差，防止 Unity 浮点数精度问题导致误报警
            float minSafeLength = Global.Instance.stroke - 0.002f;
            float maxSafeLength = Global.Instance.stroke + Global.Instance.maxStroke + 0.002f;

            for (int i = 0; i < linkCount; i++)
            {
                if (links[i] == null) continue;

                Vector3 worldPosA = triangleA.transform.TransformPoint(localVerticesA[i]);

                // 斯图尔特交叉连线算法
                int baseIndex = i;
                if (currentPlatformType == PlatformType.DOF6)
                {
                    baseIndex = (i + 1) % 6;
                }

                Vector3 worldPosB = triangleB.transform.TransformPoint(localVerticesB[baseIndex]);

                Vector3 localPosA = transform.InverseTransformPoint(worldPosA);
                Vector3 localPosB = transform.InverseTransformPoint(worldPosB);

                links[i].SetPosition(0, localPosA);
                links[i].SetPosition(1, localPosB);

                // 计算当前这根液压缸的实时拉伸长度
                float dist = Vector3.Distance(worldPosA, worldPosB);
                linkDistances[i] = dist;

                // ✨ 核心修改 2：防顶缸判定
                bool isDanger = dist < minSafeLength || dist > maxSafeLength;

                // ✨ 核心修改 3：动态切换连杆材质颜色（危险显示红色，安全显示绿色）
                links[i].material.color = isDanger ? Color.red : Color.green;

                if (distanceLabels[i] != null)
                {
                    distanceLabels[i].text = $"{prefixes[i]}: {dist * 1000f:F0} mm";

                    // ✨ 核心修改 4：动态切换文字颜色（危险显示红色，安全显示默认黄色）
                    distanceLabels[i].color = isDanger ? Color.red : textColor;
                    distanceLabels[i].transform.localScale = Vector3.one * textScale;
                    distanceLabels[i].transform.position = (worldPosA + worldPosB) / 2f;

                    if (cam != null)
                    {
                        distanceLabels[i].transform.LookAt(distanceLabels[i].transform.position + cam.transform.rotation * Vector3.forward, cam.transform.rotation * Vector3.up);
                    }
                }
            }
        }
    }
}