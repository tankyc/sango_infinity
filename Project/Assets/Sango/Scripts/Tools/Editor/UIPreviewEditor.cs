using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameObject))]
[CanEditMultipleObjects]
public class UIPreviewEditor : UnityEditor.Editor
{
    private UnityEditor.Editor m_GameObjectInspector;
    private MethodInfo m_OnHeaderGUI;
    private MethodInfo m_ShouldHideOpenButton;

    UnityEditor.Editor reflectorGameObjectEditor
    {
        get
        {
            return m_GameObjectInspector;
        }
    }


    bool ValidObject()
    {
        GameObject targetGameObject = target as GameObject;

        UIPreview example = targetGameObject.GetComponent<UIPreview>();

        if (example == null)
            return false;

        return true;
    }

    public override bool HasPreviewGUI()
    {
        if (!ValidObject())
            return reflectorGameObjectEditor.HasPreviewGUI();

        return true;
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background)
    {

        if (!ValidObject())
        {
            reflectorGameObjectEditor.OnPreviewGUI(r, background);
            return;
        }

        var targetGameObject = target as GameObject;
        UIPreview example = targetGameObject.GetComponent<UIPreview>();
        if (example.PreviewImage == null)
            return;

        GUI.DrawTexture(r, example.PreviewImage);
    }

    public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
    {
        if (!ValidObject())
            return reflectorGameObjectEditor.RenderStaticPreview(assetPath, subAssets, width, height);

        GameObject argetGameObject = target as GameObject;
        UIPreview example = argetGameObject.GetComponent<UIPreview>();

        if (example.PreviewThumbnail == null)
            return null;

        //example.PreviewIcon must be a supported format: ARGB32, RGBA32, RGB24,
        // Alpha8 or one of float formats
        Texture2D tex = new Texture2D(width, height);
        EditorUtility.CopySerialized(example.PreviewThumbnail, tex);

        return tex;
    }

    public override void DrawPreview(Rect previewArea)
    {
        reflectorGameObjectEditor.DrawPreview(previewArea);
    }

    public override void OnInspectorGUI()
    {
        reflectorGameObjectEditor.OnInspectorGUI();
    }

    public override string GetInfoString()
    {
        return reflectorGameObjectEditor.GetInfoString();
    }

    public override GUIContent GetPreviewTitle()
    {
        return reflectorGameObjectEditor.GetPreviewTitle();
    }

    public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
    {
        reflectorGameObjectEditor.OnInteractivePreviewGUI(r, background);
    }


    public override void OnPreviewSettings()
    {
        reflectorGameObjectEditor.OnPreviewSettings();
    }

    public override void ReloadPreviewInstances()
    {
        reflectorGameObjectEditor.ReloadPreviewInstances();
    }

    void OnEnable()
    {
        System.Type gameObjectorInspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameObjectInspector");
        m_OnHeaderGUI = gameObjectorInspectorType.GetMethod("OnHeaderGUI",
            BindingFlags.NonPublic | BindingFlags.Instance);
        m_GameObjectInspector = UnityEditor.Editor.CreateEditor(target, gameObjectorInspectorType);
    }

    void OnDisable()
    {
        if (Application.isPlaying)
            return;
        if (m_GameObjectInspector)
            DestroyImmediate(m_GameObjectInspector);
        m_GameObjectInspector = null;
    }

    protected override void OnHeaderGUI()
    {
        if (m_OnHeaderGUI != null)
        {
            m_OnHeaderGUI.Invoke(m_GameObjectInspector, null);
        }
    }

    public override bool RequiresConstantRepaint()
    {
        return reflectorGameObjectEditor.RequiresConstantRepaint();
    }

    public override bool UseDefaultMargins()
    {
        return reflectorGameObjectEditor.UseDefaultMargins();
    }

    protected override bool ShouldHideOpenButton()
    {
        return (bool)m_ShouldHideOpenButton.Invoke(m_GameObjectInspector, null);
    }

    [MenuItem("Assets/刷新预览图")]
    public static void CreatePreview()
    {
        var targetGameObject = Selection.activeGameObject;
        if (targetGameObject == null)
            return;
        const string cachePreviewPath = "CachePreviews";

        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(targetGameObject));


        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError("选择的目标不是一个预制体");
            return;
        }

        string rel_pathname = Path.Combine(cachePreviewPath, guid + ".png");
        string pathname = Path.Combine("Assets", rel_pathname);


        var preview = GetAssetPreview(targetGameObject);

        SaveTexture2D(preview as Texture2D, Path.Combine(Application.dataPath, rel_pathname));

        {

            AssetDatabase.ImportAsset(pathname);
            AssetDatabase.Refresh();

            TextureImporter Importer = AssetImporter.GetAtPath(pathname) as TextureImporter;
            Importer.textureType = TextureImporterType.Default;
            TextureImporterPlatformSettings setting = Importer.GetDefaultPlatformTextureSettings();
            setting.format = TextureImporterFormat.RGBA32;
            setting.textureCompression = TextureImporterCompression.Uncompressed;
            Importer.SetPlatformTextureSettings(setting);
            Importer.mipmapEnabled = false;

            AssetDatabase.ImportAsset(pathname);
            AssetDatabase.Refresh();

            Debug.Log("SaveTextureToPNG " + pathname);
        }

        preview = AssetDatabase.LoadAssetAtPath<Texture2D>(pathname);

        var targetGameObjectClone = PrefabUtility.InstantiatePrefab(targetGameObject) as GameObject;

        var previewCom = targetGameObjectClone.GetComponent<UIPreview>();
        if (previewCom == null)
        {
            previewCom = targetGameObjectClone.AddComponent<UIPreview>();
        }

        previewCom.PreviewImage = preview as Texture2D;
        previewCom.PreviewThumbnail = preview as Texture2D;

        PrefabUtility.ApplyPrefabInstance(targetGameObjectClone, InteractionMode.AutomatedAction);
        GameObject.DestroyImmediate(targetGameObjectClone);
    }


    public static Texture GetAssetPreview(GameObject obj)
    {
        GameObject canvas_obj = null;
        GameObject clone = GameObject.Instantiate(obj);
        Transform cloneTransform = clone.transform;

        GameObject cameraObj = new GameObject("render camera");
        Camera renderCamera = cameraObj.AddComponent<Camera>();
        renderCamera.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        renderCamera.clearFlags = CameraClearFlags.Color;
        renderCamera.cameraType = CameraType.SceneView;
        renderCamera.cullingMask = 1 << 21;
        renderCamera.nearClipPlane = -100;
        renderCamera.farClipPlane = 100;

        bool isUINode = false;
        if (cloneTransform is RectTransform)
        {
            //如果是UGUI节点的话就要把它们放在Canvas下了
            canvas_obj = new GameObject("render canvas", typeof(Canvas));
            Canvas canvas = canvas_obj.GetComponent<Canvas>();
            cloneTransform.parent = canvas_obj.transform;
            cloneTransform.localPosition = Vector3.zero;
            //canvas_obj.transform.position = new Vector3(-1000, -1000, -1000);
            canvas_obj.layer = 21;//放在21层，摄像机也只渲染此层的，避免混入了奇怪的东西
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = renderCamera;

            isUINode = true;
        }
        else
            cloneTransform.position = new Vector3(-1000, -1000, -1000);

        Transform[] all = clone.GetComponentsInChildren<Transform>();
        foreach (Transform trans in all)
        {
            trans.gameObject.layer = 21;
        }

        Bounds bounds = GetBounds(clone);
        Vector3 Min = bounds.min;
        Vector3 Max = bounds.max;



        if (isUINode)
        {
            cameraObj.transform.position = new Vector3(0, 0, -10);
            //Vector3 center = new Vector3(cloneTransform.position.x, (Max.y + Min.y) / 2f, cloneTransform.position.z);
            cameraObj.transform.LookAt(Vector3.zero);

            renderCamera.orthographic = true;
            float width = Max.x - Min.x;
            float height = Max.y - Min.y;
            float max_camera_size = width > height ? width : height;
            renderCamera.orthographicSize = max_camera_size / 2;//预览图要尽量少点空白
        }
        else
        {
            cameraObj.transform.position = new Vector3((Max.x + Min.x) / 2f, (Max.y + Min.y) / 2f, Max.z + (Max.z - Min.z));
            Vector3 center = new Vector3(cloneTransform.position.x, (Max.y + Min.y) / 2f, cloneTransform.position.z);
            cameraObj.transform.LookAt(center);

            int angle = (int)(Mathf.Atan2((Max.y - Min.y) / 2, (Max.z - Min.z)) * 180 / 3.1415f * 2);
            renderCamera.fieldOfView = angle;
        }
        RenderTexture texture = new RenderTexture(128, 128, 0, RenderTextureFormat.Default);
        renderCamera.targetTexture = texture;

        var tex = RTImage(renderCamera);

        Object.DestroyImmediate(canvas_obj);
        Object.DestroyImmediate(cameraObj);


        return tex;
    }

    static Texture2D RTImage(Camera camera)
    {
        // The Render Texture in RenderTexture.active is the one
        // that will be read by ReadPixels.
        var currentRT = RenderTexture.active;
        RenderTexture.active = camera.targetTexture;

        // Render the camera's view.
        //camera.Render();

        camera.Render();
        // Make a new texture and read the active Render Texture into it.
        Texture2D image = new Texture2D(camera.targetTexture.width, camera.targetTexture.height);
        image.ReadPixels(new Rect(0, 0, camera.targetTexture.width, camera.targetTexture.height), 0, 0);
        image.Apply();

        // Replace the original active Render Texture.
        RenderTexture.active = currentRT;
        return image;
    }

    public static Bounds GetBounds(GameObject obj)
    {
        Vector3 Min = new Vector3(99999, 99999, 99999);
        Vector3 Max = new Vector3(-99999, -99999, -99999);
        MeshRenderer[] renders = obj.GetComponentsInChildren<MeshRenderer>();
        if (renders.Length > 0)
        {
            for (int i = 0; i < renders.Length; i++)
            {
                if (renders[i].bounds.min.x < Min.x)
                    Min.x = renders[i].bounds.min.x;
                if (renders[i].bounds.min.y < Min.y)
                    Min.y = renders[i].bounds.min.y;
                if (renders[i].bounds.min.z < Min.z)
                    Min.z = renders[i].bounds.min.z;

                if (renders[i].bounds.max.x > Max.x)
                    Max.x = renders[i].bounds.max.x;
                if (renders[i].bounds.max.y > Max.y)
                    Max.y = renders[i].bounds.max.y;
                if (renders[i].bounds.max.z > Max.z)
                    Max.z = renders[i].bounds.max.z;
            }
        }
        else
        {
            RectTransform[] rectTrans = obj.GetComponentsInChildren<RectTransform>();
            Vector3[] corner = new Vector3[4];
            for (int i = 0; i < rectTrans.Length; i++)
            {
                //获取节点的四个角的世界坐标，分别按顺序为左下左上，右上右下
                rectTrans[i].GetWorldCorners(corner);
                if (corner[0].x < Min.x)
                    Min.x = corner[0].x;
                if (corner[0].y < Min.y)
                    Min.y = corner[0].y;
                if (corner[0].z < Min.z)
                    Min.z = corner[0].z;

                if (corner[2].x > Max.x)
                    Max.x = corner[2].x;
                if (corner[2].y > Max.y)
                    Max.y = corner[2].y;
                if (corner[2].z > Max.z)
                    Max.z = corner[2].z;
            }
        }

        Vector3 center = (Min + Max) / 2;
        Vector3 size = new Vector3(Max.x - Min.x, Max.y - Min.y, Max.z - Min.z);
        return new Bounds(center, size);
    }


    public static bool SaveTexture2D(Texture2D png, string save_file_name)
    {
        byte[] bytes = png.EncodeToPNG();
        string directory = Path.GetDirectoryName(save_file_name);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        FileStream file = File.Open(save_file_name, FileMode.Create);
        BinaryWriter writer = new BinaryWriter(file);
        writer.Write(bytes);
        file.Close();

        return true;
    }
}