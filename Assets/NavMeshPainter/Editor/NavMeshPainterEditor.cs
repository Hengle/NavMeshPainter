﻿using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ASL.NavMesh;
using ASL.NavMesh.Editor;
using UnityEditor.AI;

[CustomEditor(typeof(NavMeshPainter))]
public class NavMeshPainterEditor : Editor
{
    private enum ToolType
    {
        None = -1,
        Paint,
        Erase,
        Mapping,
        Bake,
    }

    public class Styles
    {
        public GUIStyle buttonLeft = "ButtonLeft";
        public GUIStyle buttonMid = "ButtonMid";
        public GUIStyle buttonRight = "ButtonRight";
        public GUIStyle command = "Command";
        public GUIStyle box = "box";
        public GUIStyle boldLabel = "BoldLabel";

        public GUIContent[] toolIcons = new GUIContent[]
        {
            EditorGUIUtility.IconContent("NavMeshPainter/add.png", "|Paint the NavMesh"),
            EditorGUIUtility.IconContent("NavMeshPainter/reduce.png", "|Erase the NavMesh"),
            EditorGUIUtility.IconContent("NavMeshPainter/texture.png", "|Sampling from texture"),
            EditorGUIUtility.IconContent("NavMeshPainter/nm.png", "|Bake Setting")
        };

        public GUIContent brushIcon = EditorGUIUtility.IconContent("NavMeshPainter/brush.png");
        public GUIContent eraserIcon = EditorGUIUtility.IconContent("NavMeshPainter/eraser.png");
        public GUIContent lineIcon = EditorGUIUtility.IconContent("NavMeshPainter/line.png");
        public GUIContent boxIcon = EditorGUIUtility.IconContent("NavMeshPainter/box.png");
        public GUIContent cylinderIcon = EditorGUIUtility.IconContent("NavMeshPainter/cylinder.png");
        public GUIContent sphereIcon = EditorGUIUtility.IconContent("NavMeshPainter/sphere.png");
        public GUIContent checkerboardIcon = EditorGUIUtility.IconContent("NavMeshPainter/checkerboard.png");

        public GUIContent paintSetting = new GUIContent("Paint Setting");
        public GUIContent paintTool = new GUIContent("Paint Tool");
        public GUIContent bake = new GUIContent("Bake");
        public GUIContent mappingSetting = new GUIContent("Mapping Setting");
        public GUIContent maskTexture = new GUIContent("Mask Texture");
        public GUIContent mask = new GUIContent("Mask");
        public GUIContent applyMask = new GUIContent("ApplyMask");
        public GUIContent setting = new GUIContent("Settings");
        public GUIContent xSize = new GUIContent("XSize");
        public GUIContent zSize = new GUIContent("ZSize");
        public GUIContent maxHeight = new GUIContent("MaxHeight");
        public GUIContent radius = new GUIContent("Radius");
        public GUIContent brushType = new GUIContent("BrushType");
        public GUIContent width = new GUIContent("Width");
        public GUIContent painterData = new GUIContent("PainterData");
        public GUIContent create = new GUIContent("Create New Data");
        public GUIContent generateMesh = new GUIContent("Refresh Preview Mesh");
        public GUIContent wireColor = new GUIContent("WireColor");
        public GUIContent previewMeshColor = new GUIContent("PreviewMesh Color");
        public GUIContent blendMode = new GUIContent("BlendMode");
        public GUIContent topHeight = new GUIContent("Top Height");
        public GUIContent bottomHeight = new GUIContent("Bottom Height");
        public GUIContent clear = new GUIContent("Clear");
        public GUIContent lodTip = new GUIContent("Lod DeltaDis");
    }

    public static Styles styles
    {
        get
        {
            if (s_Styles == null)
                s_Styles = new Styles();
            return s_Styles;
        }
    }

    private static Styles s_Styles;

    private ToolType m_ToolType = ToolType.None;

    private bool m_ShowCheckerBoard = true;

    private NavMeshPainter m_Target;

    private Texture2D m_RoadMask;

    private DefaultAsset m_OcTreeAsset;
    

    private Material m_PreviewMaterial;
    

    private Dictionary<System.Type, NavMeshToolEditor> m_ToolEditors;

    private Transform[] m_Previews;

    private bool m_IsPainterChanged;

    [MenuItem("GameObject/NavMeshPainter/Create NavMeshPainter")]
    static void Create()
    {
        new GameObject("NavMesh Painter").AddComponent<NavMeshPainter>();
    }

    void OnEnable()
    {
        m_Target = (NavMeshPainter) target;

        BuildData();

        List<Transform> transflist = new List<Transform>();
        for (int i = 0; i < m_Target.transform.childCount; i++)
        {
            var child = m_Target.transform.GetChild(i);
            MeshFilter mf = child.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh)
                transflist.Add(child);
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
        m_Previews = transflist.ToArray();
        
        ResetCheckerBoard();
        NavMeshEditorUtils.SetMaskTexture(null);
    }

    void OnDisable()
    {
        SaveData();
    }

    void OnSceneGUI()
    {
        if(m_ShowCheckerBoard)
            NavMeshEditorUtils.DrawCheckerBoard(m_Target.renderMeshs, Matrix4x4.identity);

        switch (m_ToolType)
        {
            case ToolType.Erase:
            case ToolType.Paint:
                DrawPaintingToolSceneGUI();
                break;
            case ToolType.Mapping:
                DrawMappingSceneGUI();
                break;
        }

    }

    public override void OnInspectorGUI()
    {
        if (m_Target.data == null)
        {
            DrawNoDataGUI();
            return;
        }
        DrawToolsGUI();
        switch (m_ToolType)
        {
            case ToolType.None:

                break;
            case ToolType.Paint:
                DrawPaintSettingGUI();
                break;
            case ToolType.Erase:
                DrawPaintSettingGUI(true);
                break;
            case ToolType.Mapping:
                DrawTextureMappingGUI();
                break;
            case ToolType.Bake:
                DrawBakeSettingGUI();
                break;
        }
        EditorGUILayout.Space();
    }

    private void DrawNoDataGUI()
    {
        EditorGUI.BeginChangeCheck();
        m_OcTreeAsset =
            EditorGUILayout.ObjectField(styles.painterData, m_OcTreeAsset, typeof (DefaultAsset), false) as
                DefaultAsset;
        if (EditorGUI.EndChangeCheck())
        {
            //if (m_OcTreeAsset != null)
            RebuildData(m_OcTreeAsset);
            if(m_Target.data != null)
                ResetCheckerBoard();
        }
        if (GUILayout.Button(styles.create))
        {
            CreateNewNavMeshPainterData();
        }
        EditorGUILayout.HelpBox("No PainterData has been setted!", MessageType.Error);
    }

    private void DrawToolsGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        EditorGUI.BeginChangeCheck();
        m_ShowCheckerBoard = GUILayout.Toggle(m_ShowCheckerBoard, styles.checkerboardIcon, styles.command);
        if (EditorGUI.EndChangeCheck())
        {
            ResetCheckerBoard();
        }

        EditorGUILayout.Space();

        int selectedTool = (int)this.m_ToolType;
        int num = GUILayout.Toolbar(selectedTool, styles.toolIcons, styles.command, new GUILayoutOption[0]);
        if (num != selectedTool)
        {
            this.m_ToolType = (ToolType)num;
        }
        GUILayout.FlexibleSpace();

        GUILayout.EndHorizontal();
    }

    private void DrawPaintSettingGUI(bool erase = false)
    {
        GUILayout.Label(styles.paintSetting, styles.boldLabel);
        GUILayout.BeginVertical(styles.box);
        GUILayout.Label(styles.paintTool, styles.boldLabel);

        GUILayout.BeginHorizontal();

        var brushIcon = erase ? styles.eraserIcon : styles.brushIcon;
        m_Target.paintTool = GUILayout.Toggle(m_Target.paintTool == NavMeshPainter.PaintingToolType.Brush, brushIcon,
            styles.buttonLeft, GUILayout.Width(35))
            ? NavMeshPainter.PaintingToolType.Brush
            : m_Target.paintTool;

        m_Target.paintTool = GUILayout.Toggle(m_Target.paintTool == NavMeshPainter.PaintingToolType.Line, styles.lineIcon,
           styles.buttonMid, GUILayout.Width(35))
           ? NavMeshPainter.PaintingToolType.Line
           : m_Target.paintTool;

        m_Target.paintTool = GUILayout.Toggle(m_Target.paintTool == NavMeshPainter.PaintingToolType.Box, styles.boxIcon,
            styles.buttonMid, GUILayout.Width(35))
            ? NavMeshPainter.PaintingToolType.Box
            : m_Target.paintTool;

        m_Target.paintTool = GUILayout.Toggle(m_Target.paintTool == NavMeshPainter.PaintingToolType.Cylinder, styles.cylinderIcon,
           styles.buttonRight, GUILayout.Width(35))
           ? NavMeshPainter.PaintingToolType.Cylinder
           : m_Target.paintTool;

        GUILayout.EndHorizontal();

        var tooleditor = GetPaintingToolEditor(m_Target.GetPaintingTool());
        if (tooleditor != null)
        {
            tooleditor.DrawGUI();
        }
        //EditorGUILayout.PropertyField(tooleditor);

        if (GUILayout.Button(styles.bake))
        {
            Bake();
        }
        if (GUILayout.Button(styles.clear))
        {
            Clear();
        }

        GUILayout.EndVertical();
    }

    private void DrawTextureMappingGUI()
    {
        GUILayout.Label(styles.mappingSetting, styles.boldLabel);
        GUILayout.BeginVertical(styles.box);
        GUILayout.Label(styles.maskTexture, styles.boldLabel);

        EditorGUI.BeginChangeCheck();
        m_RoadMask = EditorGUILayout.ObjectField(styles.mask, m_RoadMask, typeof (Texture2D), false) as Texture2D;
        if (EditorGUI.EndChangeCheck())
            NavMeshEditorUtils.SetMaskTexture(m_RoadMask);

        //m_ApplyTextureMode = (TextureBlendMode) EditorGUILayout.EnumPopup(styles.blendMode, m_ApplyTextureMode);

        if (GUILayout.Button(styles.applyMask))
        {
            ApplyMask();
        }
        if (GUILayout.Button(styles.bake))
        {
            Bake();
        }
        if (GUILayout.Button(styles.clear))
        {
            Clear();
        }

        GUILayout.EndVertical();
    }

    private void DrawBakeSettingGUI()
    {
        GUILayout.Label(styles.setting, styles.boldLabel);
        EditorGUI.BeginChangeCheck();
        m_OcTreeAsset =
            EditorGUILayout.ObjectField(styles.painterData, m_OcTreeAsset, typeof(DefaultAsset), false) as
                DefaultAsset;
        if (EditorGUI.EndChangeCheck())
        {
            //if (m_OcTreeAsset != null)
            RebuildData(m_OcTreeAsset);
            if(m_Target.data != null)
                ResetCheckerBoard();
        }

        m_Target.navMeshWireColor = EditorGUILayout.ColorField(styles.wireColor, m_Target.navMeshWireColor);
        m_Target.previewColor = EditorGUILayout.ColorField(styles.previewMeshColor, m_Target.previewColor);
        m_Target.lodDeltaDis = Mathf.Max(0.001f, EditorGUILayout.FloatField(styles.lodTip, m_Target.lodDeltaDis));

        if (GUILayout.Button(styles.generateMesh))
        {
            RefreshPreviewMesh();
        }

        if (GUILayout.Button(styles.create))
        {
            CreateNewNavMeshPainterData();
        }
        if (GUILayout.Button(styles.bake))
        {
            Bake();
        }
        if (GUILayout.Button(styles.clear))
        {
            Clear();
        }
    }

    private void DrawPaintingToolSceneGUI()
    {
        IPaintingTool tool = m_Target.GetPaintingTool();
        if (tool == null)
            return;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));


        if (m_Target.data != null && m_Target.renderMeshs != null && m_Target.renderMeshs.Length > 0)
        {
            var tooleditor = GetPaintingToolEditor(tool);
            tooleditor.DrawSceneGUI(m_Target);
        }
    }

    private void DrawMappingSceneGUI()
    {
        NavMeshEditorUtils.DrawMask(m_Target.renderMeshs, Matrix4x4.identity);
    }

    private void ApplyPaint(IPaintingTool tool)
    {
        if (tool != null)
        {
            m_IsPainterChanged = true;
            if (m_ToolType == ToolType.Paint)
                m_Target.Draw(tool);
            else if (m_ToolType == ToolType.Erase)
                m_Target.Erase(tool);
        }
    }

    private void ResetCheckerBoard()
    {
        float minSize = m_Target.GetMinSize();
        NavMeshEditorUtils.SetCheckerBoardCellSize(minSize);
    }

    private void Bake()
    {
        if (RefreshPreviewMesh())
        {
            NavMeshBuilder.BuildNavMesh();
        }
    }

    private void Clear()
    {
        for (int i = 0; i < m_Previews.Length; i++)
        {
            if (m_Previews[i])
                DestroyImmediate(m_Previews[i].gameObject);
        }
        m_Target.Clear();
    }

    private void ApplyMask()
    {
        if (m_RoadMask == null)
            return;
        m_IsPainterChanged = true;
        RenderTexture rt = RenderTexture.GetTemporary(m_RoadMask.width, m_RoadMask.height, 0);
        Graphics.Blit(m_RoadMask, rt);

        RenderTexture active = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D cont = new Texture2D(rt.width, rt.height);
        cont.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        cont.Apply();
        RenderTexture.active = active;

        RenderTexture.ReleaseTemporary(rt);

        m_RoadMask = null;
        m_Target.SamplingFromTexture(cont);

        DestroyImmediate(cont);
        NavMeshEditorUtils.SetMaskTexture(null);
    }

    private void CreateNewNavMeshPainterData()
    {
        NavMeshPainterCreator.CreateWizard(m_Target);
    }

    private bool RefreshPreviewMesh()
    {
        Mesh[] meshes = m_Target.GenerateMeshes();
        if (meshes != null && meshes.Length > 0)
        {
//            if (m_PreviewMeshObj == null)
//            {
//                m_PreviewMeshObj = new GameObject("PreviewMesh");
//                m_PreviewMeshObj.transform.SetParent(m_Target.transform);
//                m_PreviewMeshObj.hideFlags = HideFlags.HideAndDontSave;
//                m_PreviewMeshObj.isStatic = true;
//            }

            if (m_Previews != null && m_Previews.Length > 0)
            {
                for (int i = 0; i < m_Previews.Length; i++)
                {
                    Object.DestroyImmediate(m_Previews[i].gameObject);
                }
            }

            m_Previews = new Transform[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                m_Previews[i] = new GameObject("[MeshPreview_" + i + "]").transform;
                //m_Previews[i].gameObject.hideFlags = HideFlags.DontSave;
                m_Previews[i].gameObject.isStatic = true;
                m_Previews[i].gameObject.layer = m_Target.gameObject.layer;
                m_Previews[i].transform.SetParent(m_Target.transform);
                m_Previews[i].position = Vector3.zero;
                m_Previews[i].eulerAngles = Vector3.zero;
                MeshFilter mf = m_Previews[i].GetComponent<MeshFilter>();
                if (mf == null)
                {
                    mf = m_Previews[i].gameObject.AddComponent<MeshFilter>();
                    //mf.hideFlags = HideFlags.HideAndDontSave;
                }
                mf.sharedMesh = meshes[i];

                MeshRenderer mr = m_Previews[i].GetComponent<MeshRenderer>();
                if (mr == null)
                {
                    mr = m_Previews[i].gameObject.AddComponent<MeshRenderer>();
                    //mr.hideFlags = HideFlags.HideAndDontSave;
                }
                if (m_PreviewMaterial == null)
                {
                    m_PreviewMaterial =
                        new Material((Shader) EditorGUIUtility.Load("NavMeshPainter/Shader/NavMeshRender.shader"));
                    m_PreviewMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                mr.sharedMaterial = m_PreviewMaterial;
            }

            return true;
        }
        return false; 
    }

    private NavMeshToolEditor GetPaintingToolEditor(IPaintingTool tool)
    {
        System.Type tooltype = tool.GetType();
        NavMeshToolEditor editor = null;
        if (m_ToolEditors == null)
        {
            m_ToolEditors = new Dictionary<System.Type, NavMeshToolEditor>();
            System.Reflection.Assembly assembly = this.GetType().Assembly;
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (type.IsSubclassOf(typeof(NavMeshToolEditor)))
                {
                    var attributes = type.GetCustomAttributes(typeof(CustomNavMeshToolEditorAttribute), false);
                    foreach (var att in attributes)
                    {
                        CustomNavMeshToolEditorAttribute a = att as CustomNavMeshToolEditorAttribute;
                        if (a == null)
                            continue;
                        if (!m_ToolEditors.ContainsKey(a.navMeshToolType))
                        {
                            m_ToolEditors[a.navMeshToolType] = (NavMeshToolEditor)System.Activator.CreateInstance(type);
                            m_ToolEditors[a.navMeshToolType].SetApplyAction(new System.Action<IPaintingTool>(ApplyPaint));
                        }
                    }
                }
            }
        }
        if (m_ToolEditors.ContainsKey(tooltype))
        {
            editor = m_ToolEditors[tooltype];
            editor.SetTool(tool);
        }
        return editor;
    }

    private void BuildData()
    {
        //m_Target.Init();
        
        m_Target.Load();

        m_OcTreeAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(m_Target.dataPath);
    }

    private void RebuildData(DefaultAsset asset)
    {
        if (asset != null)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.FileInfo file = new System.IO.FileInfo(path);
                if (file.Extension.ToLower() == ".nmptree" && file.Exists)
                {
                    m_Target.Reload(path);
                    return;
                }
            }
        }
        m_Target.Reload(null);
    }

    private void SaveData()
    {
        //m_Target.Save();
        if(m_IsPainterChanged || !System.IO.File.Exists(m_Target.dataPath))
            m_Target.Save();
        //EditorUtility.SetDirty(m_Target.data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    static void DrawGizmoForMyScript(NavMeshPainter scr, GizmoType gizmoType)
    {
        if(scr && scr.data != null && SceneView.currentDrawingSceneView && SceneView.currentDrawingSceneView.camera)
            scr.data.DrawGizmos(scr.navMeshWireColor, SceneView.currentDrawingSceneView.camera, scr.lodDeltaDis);
    }
}
