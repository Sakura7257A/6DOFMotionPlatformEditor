/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：PlatformModelManager.cs
 * 作者：LeonLiu
 * 日期：2026/2/1 12:59:24
 * 功能：3自由度平台模型
*************************************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MPE
{
    public class PlatformModelManager : MonoBehaviour
    {
        [Header("Settings")]
        public string targetLayerName = "Triangle";

        // ✨ 核心修改：将原本的单一材质拆分为 A 和 B 两个独立材质
        public Material materialTriangleA; // 顶部三角形(动平台)材质
        public Material materialTriangleB; // 底部三角形(静平台)材质

        [Header("Text Settings")]
        public Color textColor = Color.yellow;
        public float textScale = 0.2f;

        [Header("Triangle Edges")]
        [Min(0.1f)] public float a = 3f;
        [Min(0.1f)] public float b = 4f;
        [Min(0.1f)] public float c = 5f;

        [Header("Vertical Offset")]
        public float height = 3f;

        [Header("Read Only Data (Output)")]
        public float[] linkDistances = new float[3];

        public bool createState = false;

        public GameObject triangleA;
        GameObject triangleB;
        Mesh meshA;
        Mesh meshB;

        LineRenderer[] links = new LineRenderer[3];
        TextMesh[] distanceLabels = new TextMesh[3];
        Vector3[] localVertices;

        float lastA, lastB, lastC, lastHeight;

        // ✨ 核心修改：分别追踪两个材质的更改状态
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
                if (a != lastA || b != lastB || c != lastC)
                {
                    RebuildTriangles();
                }

                if (triangleA != null)
                {
                    triangleA.transform.localPosition = Vector3.up * height;
                }

                // ✨ 核心修改：检测 A 或 B 任何一个材质发生变化时触发更新
                if (materialTriangleA != lastMaterialA || materialTriangleB != lastMaterialB)
                {
                    UpdateMaterials();
                    lastMaterialA = materialTriangleA;
                    lastMaterialB = materialTriangleB;
                }

                UpdateLinksAndLabels();
            }
        }

        public void Cleanup()
        {
            if (triangleA) DestroyImmediate(triangleA);
            if (triangleB) DestroyImmediate(triangleB);

            var oldChildren = transform.GetComponentsInChildren<Transform>();
            foreach (var child in oldChildren)
            {
                if (child != null && child.gameObject.name.StartsWith("Link_"))
                    DestroyImmediate(child.gameObject);
            }

            links = new LineRenderer[3];
            distanceLabels = new TextMesh[3];
            linkDistances = new float[3];
        }

        public void InitializeSystem()
        {
            int layerID = GetLayerID();

            // ✨ 核心修改：在初始化生成时，分别传入上下三角形的独立材质
            CreateTriangle(ref triangleA, ref meshA, "TriangleA", Vector3.up * height, layerID, materialTriangleA);
            CreateTriangle(ref triangleB, ref meshB, "TriangleB", Vector3.zero, layerID, materialTriangleB);

            CreateLinks(layerID);
            RebuildTriangles();
            createState = true;

            lastMaterialA = materialTriangleA;
            lastMaterialB = materialTriangleB;
        }

        int GetLayerID() => LayerMask.NameToLayer(targetLayerName) == -1 ? 0 : LayerMask.NameToLayer(targetLayerName);

        // ✨ 核心修改：新增 mat 参数，用于接收指定的材质
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

        // ✨ 核心修改：重构应用材质的方法，支持传入目标材质
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
            // ✨ 核心修改：分别对 A 和 B 更新它们各自的材质
            if (triangleA != null) ApplyMaterial(triangleA.GetComponent<MeshRenderer>(), materialTriangleA);
            if (triangleB != null) ApplyMaterial(triangleB.GetComponent<MeshRenderer>(), materialTriangleB);
        }

        void CreateLinks(int layer)
        {
            if (linkDistances == null || linkDistances.Length != 3) linkDistances = new float[3];

            for (int i = 0; i < 3; i++)
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
            localVertices = CalculateCenteredTriangleVertices(a, b, c);
            BuildMesh(meshA);
            BuildMesh(meshB);
            lastA = a; lastB = b; lastC = c; lastHeight = height;
        }

        void BuildMesh(Mesh mesh)
        {
            if (mesh == null) return;
            mesh.Clear();
            mesh.vertices = localVertices;
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 1 };

            Vector2[] uvs = new Vector2[localVertices.Length];
            for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(localVertices[i].x, localVertices[i].z);
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

        void UpdateLinksAndLabels()
        {
            if (triangleA == null || triangleB == null) return;
            if (linkDistances == null || linkDistances.Length != 3) linkDistances = new float[3];

            Camera cam = Camera.current;
            if (cam == null) cam = Camera.main;

            string[] prefixes = { "A", "B", "C" };

            for (int i = 0; i < 3; i++)
            {
                if (links[i] == null) continue;

                Vector3 worldPosA = triangleA.transform.TransformPoint(localVertices[i]);
                Vector3 worldPosB = triangleB.transform.TransformPoint(localVertices[i]);

                Vector3 localPosA = transform.InverseTransformPoint(worldPosA);
                Vector3 localPosB = transform.InverseTransformPoint(worldPosB);

                links[i].SetPosition(0, localPosA);
                links[i].SetPosition(1, localPosB);

                float dist = Vector3.Distance(worldPosA, worldPosB);
                linkDistances[i] = dist;

                if (distanceLabels[i] != null)
                {
                    distanceLabels[i].text = $"{prefixes[i]}: {dist * 1000f:F1} mm";

                    distanceLabels[i].color = textColor;
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