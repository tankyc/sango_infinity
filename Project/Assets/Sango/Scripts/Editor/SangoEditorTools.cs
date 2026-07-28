
using Sango.UI;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows;

public static class SangeEditorTools
{
    [MenuItem("Assets/UIprefab 加大4字体")]
    [MenuItem("Sango/UIprefab 加大4字体")]
    public static void AddFontSize()
    {
        int addSize = 4;
        Object[] objects = Selection.objects;
        foreach (Object o in objects)
        {
            GameObject uiPrefab = o as GameObject;
            if (uiPrefab != null)
            {
                Text[] text = uiPrefab.GetComponentsInChildren<Text>(true);
                if (text != null)
                {
                    foreach (Text t in text)
                    {
                        if (t.fontSize == 0) continue;
                        float scale = (t.fontSize + addSize) / (float)t.fontSize;
                        RectTransform rect = t.GetComponent<RectTransform>();
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rect.rect.width * scale);
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rect.rect.height * scale);
                        t.fontSize = t.fontSize + (int)addSize;
                    }
                }

                EditorUtility.SetDirty(uiPrefab);
                AssetDatabase.SaveAssetIfDirty(o);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Sango/头像编辑工具导出头像名字批处理")]
    public static void RenameHeadIconName()
    {
        string savedir = EditorUtility.OpenFolderPanel("选择头像文件夹", Application.dataPath, "");
        string[] files = Sango.Directory.GetFiles(savedir, "*.png", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(f);
            string[] s = fileName.Split("_");
            int id;
            int part;
            int.TryParse(s[0], out id);
            int.TryParse(s[2], out part);

            Sango.File.Move(f, string.Format("{0}/{1}_{2}.png", savedir, id, part));

        }
        files = Sango.Directory.GetFiles(savedir, "*.jpg", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(f);
            string[] s = fileName.Split("_");
            int id;
            int part;
            int.TryParse(s[0], out id);
            int.TryParse(s[2], out part);

            Sango.File.Move(f, string.Format("{0}/{1}_{2}.jpg", savedir, id, part));
        }
    }

    [MenuItem("Sango/复制语音")]
    public static void CopySound()
    {
        List<int> sounds = new List<int>()
        {
            2656,
2658,
2660,
2662,
2664,
2665,
2666,
2667,
2670,
2672,
2674,
2676,
2678,
2679,
2680,
2681,
2684,
2686,
2688,
2690,
2692,
2693,
2694,
2695,
2698,
2700,
2702,
2704,
2706,
2707,
2708,
2709,
2712,
2714,
2716,
2718,
2720,
2721,
2722,
2723,
2726,
2728,
2730,
2732,
2734,
2735,
2736,
2737,
2740,
2742,
2744,
2746,
2748,
2749,
2750,
2751,
2754,
2756,
2758,
2760,
2762,
2763,
2764,
2765,
2768,
2770,
2772,
2774,
2776,
2777,
2778,
2779,
2782,
2784,
2786,
2788,
2790,
2791,
2792,
2793,
2796,
2798,
2800,
2802,
2804,
2805,
2806,
2807,
2810,
2812,
2814,
2816,
2818,
2819,
2820,
2821,
2824,
2826,
2828,
2830,
2832,
2833,
2834,
2835,
2838,
2840,
2842,
2844,
2846,
2847,
2848,
2849,
2852,
2854,
2856,
2858,
2860,
2861,
2862,
2863,
2866,
2868,
2870,
2872,
2874,
2875,
2876,
2877,
2880,
2882,
2884,
2886,
2888,
2889,
2890,
2891,
2894,
2896,
2898,
2900,
2902,
2903,
2904,
2905,
2908,
2910,
2912,
2914,
2916,
2917,
2918,
2919,
2922,
2924,
2926,
2928,
2930,
2931,
2932,
2933,
2936,
2938,
2940,
2942,
2944,
2945,
2946,
2947,
2950,
2952,
2954,
2956,
2958,
2959,
2960,
2961,
2964,
2966,
2968,
2970,
2972,
2973,
2974,
2975,
2992,
2994,
2996,
2998,
3000,
3001,
3002,
3003,
3020,
3022,
3024,
3026,
3028,
3029,
3030,
3031,
3034,
3036,
3038,
3040,
3042,
3043,
3044,
3045,
3048,
3050,
3052,
3054,
3056,
3057,
3058,
3059,
3062,
3064,
3066,
3068,
3070,
3071,
3072,
3073,
3076,
3078,
3080,
3082,
3084,
3085,
3086,
3087,
3090,
3092,
3094,
3096,
3098,
3099,
3100,
3101,
3104,
3106,
3108,
3110,
3112,
3113,
3114,
3115,
3118,
3120,
3122,
3124,
3126,
3127,
3128,
3129,
3132,
3134,
3136,
3138,
3140,
3141,
3142,
3143,
3146,
3148,
3150,
3152,
3154,
3155,
3156,
3157,
3160,
3162,
3164,
3166,
3168,
3169,
3170,
3171,
3188,
3190,
3192,
3194,
3196,
3197,
3198,
3199,
3202,
3204,
3206,
3208,
3210,
3211,
3212,
3213,
3216,
3218,
3220,
3222,
3224,
3225,
3226,
3227,
3230,
3232,
3234,
3236,
3238,
3239,
3240,
3241,
3244,
3246,
3248,
3250,
3252,
3253,
3254,
3255,
3258,
3260,
3262,
3264,
3266,
3267,
3268,
3269,
3272,
3274,
3276,
3278,
3280,
3281,
3282,
3283,
        };

        string savedir = EditorUtility.OpenFolderPanel("选择语音文件夹", Application.dataPath, "");
        string[] files = Sango.Directory.GetFiles(savedir, "*.ogg", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(f);
            int soundId = int.Parse(fileName);
            if (sounds.Contains(soundId))
            {
                Sango.File.Move(f, string.Format("{0}/{1}.ogg", savedir, soundId));
            }
        }
    }


    [MenuItem("Sango/检查UISFXPlay")]
    public static void CheckUISFXPlay()
    {
        Object[] objects = Selection.objects;
        foreach (Object o in objects)
        {
            GameObject uiPrefab = o as GameObject;
            if (uiPrefab != null)
            {
                //UISFXPlay[] text = uiPrefab.GetComponentsInChildren<UISFXPlay>(true);
                //if (text != null)
                //{
                //    foreach (UISFXPlay t in text)
                //    {
                //        if(t.sfxPath.Equals("Assets/Sound/button.mp3"))
                //        {
                //            t.sfxId = 3;
                //        }
                //        else if(t.sfxPath.Equals("Assets/Sound/cancel.mp3"))
                //        {
                //            t.sfxId = 4;
                //        }
                //        else
                //        {
                //            Debug.LogError($"{o.name}- {t.sfxPath}");
                //        }
                //    }
                //}

                //EditorUtility.SetDirty(uiPrefab);
                //AssetDatabase.SaveAssetIfDirty(o);
            }
        }
    }

    [MenuItem("Sango/地形贴图文件夹文件名字替换")]
    public static void RenameTerrainTexName()
    {
        Sango.Path.Init();
        string savedir = EditorUtility.OpenFolderPanel("选择贴图文件夹", Sango.Path.ContentRootPath, "");
        string[] files = Sango.Directory.GetFiles(savedir, "*.png", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(f);
            string[] s = fileName.Split("_");
            if (s.Length > 1)
            {
                int id;
                int.TryParse(s[1], out id);
                Sango.File.Move(f, string.Format("{0}/layer_{1}.png", savedir, id));
            }
        }

    }


    [MenuItem("Sango/模型WKMD->WKM")]
    public static void RenameWKMD2WKM()
    {
        string savedir = EditorUtility.OpenFolderPanel("选择文件夹", Application.dataPath, "");
        string[] files = Sango.Directory.GetFiles(savedir, "*.WKMD", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string newFileName = f.Remove(f.Length - 4) + "wkm";
            Debug.Log(newFileName);
            Sango.File.Move(f, newFileName);
        }

    }

    [MenuItem("Assets/选中图集拆小图")]
    [MenuItem("Sango/选中图集拆小图")]
    static void ProcessToSprite()
    {
        Texture2D image = Selection.activeObject as Texture2D;//获取旋转的对象
        string rootPath = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(image));//获取路径名称

        string path = rootPath + "/" + image.name + ".png";//图片路径名称

        TextureImporter texImp = AssetImporter.GetAtPath(path) as TextureImporter;

        if (!AssetDatabase.IsValidFolder(rootPath.Replace("\\cutAtlas", "") + "/" + image.name))
            AssetDatabase.CreateFolder(rootPath.Replace("\\cutAtlas", ""), image.name);//创建文件夹

        foreach (SpriteMetaData metaData in texImp.spritesheet)//遍历小图集
        {
            Texture2D myimage = new Texture2D((int)metaData.rect.width, (int)metaData.rect.height);

            for (int y = (int)metaData.rect.y; y < metaData.rect.y + metaData.rect.height; y++)//Y轴像素
            {
                for (int x = (int)metaData.rect.x; x < metaData.rect.x + metaData.rect.width; x++)
                    myimage.SetPixel(x - (int)metaData.rect.x, y - (int)metaData.rect.y, image.GetPixel(x, y));
            }

            //转换纹理到EncodeToPNG兼容格式
            if (myimage.format != TextureFormat.ARGB32 && myimage.format != TextureFormat.RGB24)
            {
                Texture2D newTexture = new Texture2D(myimage.width, myimage.height);
                newTexture.SetPixels(myimage.GetPixels(0), 0);
                myimage = newTexture;
            }
            var pngData = myimage.EncodeToPNG();

            string dstPng = rootPath.Replace("\\cutAtlas", "") + "/" + image.name + "/" + metaData.name + ".png";
            System.IO.File.WriteAllBytes(dstPng, pngData);
            AssetDatabase.Refresh();

            TextureImporter spriteImp = AssetImporter.GetAtPath(dstPng) as TextureImporter;
            if (spriteImp.textureType != TextureImporterType.Sprite)
            {
                spriteImp.textureType = TextureImporterType.Sprite;
                spriteImp.spriteBorder = metaData.border;
                spriteImp.SaveAndReimport();
            }
        }
    }


    [MenuItem("Assets/Sprite Sheet Packer/Process to Sprites Info")]
    static void ProcessToSpriteInfo()
    {
        Texture2D image = Selection.activeObject as Texture2D;//获取旋转的对象
        string rootPath = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(image));//获取路径名称
        string path = rootPath + "/" + image.name + ".png";//图片路径名称

        TextureImporter texImp = AssetImporter.GetAtPath(path) as TextureImporter;

        AssetDatabase.CreateFolder(rootPath, image.name);//创建文件夹

        StringBuilder sb = new StringBuilder();
        foreach (SpriteMetaData metaData in texImp.spritesheet)//遍历小图集
        {
            sb.AppendLine(string.Format("{0};{1};{2};{3};{4}; {5};{6}; {7};{8};{9};{10}",
                metaData.name, metaData.rect.x, metaData.rect.y, metaData.rect.width, metaData.rect.height,
                metaData.pivot.x, metaData.pivot.y,
                metaData.border.x, metaData.border.y, metaData.border.z, metaData.border.w));
        }

        System.IO.File.WriteAllText(rootPath + "/" + image.name + "/" + image.name + ".tpsheet", sb.ToString());

    }

    [MenuItem("Sango/贴图后缀小写")]
    public static void RenamePNG2png()
    {
        string savedir = EditorUtility.OpenFolderPanel("选择文件夹", Application.dataPath, "");
        string[] files = Sango.Directory.GetFiles(savedir, "*.PNG", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string newFileName = f.Remove(f.Length - 3) + "png";
            Debug.Log(newFileName);
            Sango.File.Move(f, newFileName + "1");
            Sango.File.Move(newFileName + "1", newFileName);
        }

        files = Sango.Directory.GetFiles(savedir, "*.PNG.meta", System.IO.SearchOption.AllDirectories);
        foreach (string f in files)
        {
            string newFileName = f.Remove(f.Length - 8) + "png.meta";
            Debug.Log(newFileName);
            Sango.File.Move(f, newFileName + "1");
            Sango.File.Move(newFileName + "1", newFileName);
        }
    }


    class ModelDataaa
    {
        public int Id;
        public string name;
        public string model;
        public string texture;
    }

    [MenuItem("Sango/自动生成模型预制件")]
    public static void AutoMakeModelPrefab()
    {
        string savedir = EditorUtility.OpenFilePanel("选择模型信息", Application.dataPath, "");
        string data = System.IO.File.ReadAllText(savedir);

        Dictionary<int, ModelDataaa> datas = new Dictionary<int, ModelDataaa>();
        datas = TKNewtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<int, ModelDataaa>>(data);

        string matDir = "Assets/Mods/Content/Assets/Model/Materials/";

        string goSaveDir = "Assets/Mods/Content/Assets/Model/Prefab/Auto";

        foreach (ModelDataaa model in datas.Values)
        {
            string modelFile = model.model.Replace("Model/", "Assets/Mods/Content/Assets/Model/Mesh/");
            string texFile = "Assets/Mods/Content/Assets/Model/" + model.texture;
            string modelName = System.IO.Path.GetFileNameWithoutExtension(model.model);

            GameObject modelObj = AssetDatabase.LoadAssetAtPath<GameObject>($"{goSaveDir}{modelName}.prefab");
            if (modelObj != null)
                continue;

            GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(modelFile);
            if (obj == null)
                continue;

            obj = GameObject.Instantiate(obj);
            obj.name = modelName;

            string texName = System.IO.Path.GetFileNameWithoutExtension(texFile);
            string matFile = $"{matDir}{texName}.mat";

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matFile);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sango/building_urp"));
                mat.name = texName;
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texFile);
                mat.SetTexture("_BaseMap", texture);
                AssetDatabase.CreateAsset(mat, matFile);
            }

            MeshRenderer meshRender = obj.GetComponentInChildren<MeshRenderer>();
            if (meshRender != null)
            {
                meshRender.sharedMaterial = mat;
            }

            PrefabUtility.SaveAsPrefabAsset(obj, $"{goSaveDir}/{modelName}.prefab");
            GameObject.DestroyImmediate(obj);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

    }

    [MenuItem("Sango/材质球MainTex->BaseTex")]
    public static void MatSaveMainTex2BaseTex()
    {
        Object[] objects = Selection.objects;
        foreach (Object o in objects)
        {
            Material material = o as Material;
            if (material != null)
            {
                Texture tex = material.GetTexture("_MainTex");
                if (tex != null)
                {
                    material.SetTexture("_BaseMap", tex);
                }
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Assets/UIprefab重定设置Outline颜色为不透明黑色")]
    [MenuItem("Sango/UIprefab重定设置Outline颜色为不透明黑色")]
    public static void UIPrefabResetOutlineColor()
    {
        Object[] objects = Selection.objects;
        foreach (Object o in objects)
        {
            GameObject uiPrefab = o as GameObject;
            if (uiPrefab != null)
            {
                bool changed = false;
                TextOutline[] images = uiPrefab.GetComponentsInChildren<TextOutline>(true);
                if (images != null)
                {
                    foreach (TextOutline image in images)
                    {
                        UnityEngine.Color c = new UnityEngine.Color(0.12f, 0.12f, 0.12f);
                        if (image.m_OutlineColor != c)
                            image.m_OutlineColor = c;
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(uiPrefab);
                    AssetDatabase.SaveAssetIfDirty(o);
                }
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Assets/UIprefab outline -> TextOutline")]
    [MenuItem("Sango/UIprefab outline -> TextOutline")]
    public static void UIPrefabResetOutlineColor2()
    {
        Object[] objects = Selection.objects;
        foreach (Object o in objects)
        {
            GameObject uiPrefab = o as GameObject;
            if (uiPrefab != null)
            {
                bool changed = false;
                UnityEngine.UI.Outline[] images = uiPrefab.GetComponentsInChildren<UnityEngine.UI.Outline>(true);
                if (images != null)
                {
                    foreach (UnityEngine.UI.Outline image in images)
                    {
                        Text text = image.GetComponent<Text>();
                        if (text != null)
                        {
                            text.material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Sango/Resources/OutlineMat.mat");
                            TextOutline textOutline = image.gameObject.AddComponent<TextOutline>();
                            GameObject.DestroyImmediate(image, true);
                            changed = true;
                        }
                    }
                }

                UnityEngine.UI.Shadow[] images_shadow = uiPrefab.GetComponentsInChildren<UnityEngine.UI.Shadow>(true);
                if (images_shadow != null)
                {
                    foreach (UnityEngine.UI.Shadow image in images_shadow)
                    {
                        Text text = image.GetComponent<Text>();
                        if (text != null)
                        {
                            text.material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Sango/Resources/OutlineMat.mat");

                            TextOutline textOutline = image.gameObject.AddComponent<TextOutline>();
                            GameObject.DestroyImmediate(image, true);
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(uiPrefab);
                    AssetDatabase.SaveAssetIfDirty(o);
                }
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }


    [MenuItem("Assets/UIprefab重定向为小图sprite")]
    [MenuItem("Sango/UIprefab重定向为小图sprite")]
    public static void UIPrefabResetSprite()
    {
        Object[] objects = Selection.objects;
        foreach (Object o in objects)
        {
            GameObject uiPrefab = o as GameObject;
            if (uiPrefab != null)
            {
                bool changed = false;
                UnityEngine.UI.Image[] images = uiPrefab.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                if (images != null)
                {
                    foreach (UnityEngine.UI.Image image in images)
                    {
                        if (image.sprite != null)
                        {
                            string srcPath = AssetDatabase.GetAssetPath(image.sprite);
                            if (System.IO.Path.GetFileNameWithoutExtension(srcPath) != image.sprite.name)
                            {
                                string name = image.sprite.name;
                                string[] dir = name.Split('_');
                                if (dir.Length > 0)
                                {
                                    string dstDir = dir[0];
                                    Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Mods/Content/Assets/UI/AtlasTexture/{dstDir}/{name}.png");
                                    if (spr != null)
                                    {
                                        Debug.Log($"Sprite替换 {image.sprite.name}");
                                        image.sprite = spr;
                                        changed = true;
                                    }
                                }
                            }
                        }
                    }
                }

                UnityEngine.UI.Button[] buttons = uiPrefab.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                if (buttons != null)
                {
                    foreach (UnityEngine.UI.Button image in buttons)
                    {
                        UnityEngine.UI.SpriteState spriteState = image.spriteState;

                        if (spriteState.highlightedSprite != null)
                        {
                            string srcPath = AssetDatabase.GetAssetPath(spriteState.highlightedSprite);
                            if (System.IO.Path.GetFileNameWithoutExtension(srcPath) != spriteState.highlightedSprite.name)
                            {
                                string name = spriteState.highlightedSprite.name;
                                string[] dir = name.Split('_');
                                if (dir.Length > 0)
                                {
                                    string dstDir = dir[0];
                                    Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Mods/Content/Assets/UI/AtlasTexture/{dstDir}/{name}.png");
                                    if (spr != null)
                                    {
                                        Debug.Log($"Sprite替换 {spr.name}");
                                        spriteState.highlightedSprite = spr;
                                        changed = true;
                                    }
                                }
                            }
                        }

                        if (spriteState.pressedSprite != null)
                        {
                            string srcPath = AssetDatabase.GetAssetPath(spriteState.pressedSprite);
                            if (System.IO.Path.GetFileNameWithoutExtension(srcPath) != spriteState.pressedSprite.name)
                            {
                                string name = spriteState.pressedSprite.name;
                                string[] dir = name.Split('_');
                                if (dir.Length > 0)
                                {
                                    string dstDir = dir[0];
                                    Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Mods/Content/Assets/UI/AtlasTexture/{dstDir}/{name}.png");
                                    if (spr != null)
                                    {
                                        Debug.Log($"Sprite替换 {spr.name}");
                                        spriteState.pressedSprite = spr;
                                        changed = true;
                                    }
                                }
                            }
                        }

                        if (spriteState.disabledSprite != null)
                        {
                            string srcPath = AssetDatabase.GetAssetPath(spriteState.disabledSprite);
                            if (System.IO.Path.GetFileNameWithoutExtension(srcPath) != spriteState.disabledSprite.name)
                            {
                                string name = spriteState.disabledSprite.name;
                                string[] dir = name.Split('_');
                                if (dir.Length > 0)
                                {
                                    string dstDir = dir[0];
                                    Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Mods/Content/Assets/UI/AtlasTexture/{dstDir}/{name}.png");
                                    if (spr != null)
                                    {
                                        Debug.Log($"Sprite替换 {spr.name}");
                                        spriteState.disabledSprite = spr;
                                        changed = true;
                                    }
                                }
                            }
                        }

                        if (changed)
                        {
                            image.spriteState = spriteState;
                        }
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(uiPrefab);
                    AssetDatabase.SaveAssetIfDirty(o);
                }
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }


    [MenuItem("Sango/修复中文命名的语音")]
    public static void FixCNSound()
    {
        List<string> sounds = new List<string>()
        {
            "Assets/Sound/按钮音.mp3                               "
,"Assets/Sound/取消音.mp3                               "
,"Assets/Sound/出现消息框音.mp3                         "
,"Assets/Sound/过回合音_前.mp3                          "
,"Assets/Sound/过回合_人物弹出窗口+开发耐久提升音.mp3   "
,"Assets/Sound/发生关系变化.mp3                         "
,"Assets/Sound/铜锣.mp3                                 "
,"Assets/Sound/授予官职.mp3                             "
,"Assets/Sound/左下角消息提示音.mp3                     "
,"Assets/Sound/脚步声.mp3                               "
,"Assets/Sound/卷轴声.mp3                               "
,"Assets/Sound/起义声.mp3                               "
,"Assets/Sound/去世.mp3                                 "
,"Assets/Sound/市集音.mp3                               "
,"Assets/Sound/2264.ogg                                 "
,"Assets/Sound/2291.ogg                                 "
,"Assets/Sound/鸟叫声.mp3                               "
,"Assets/Sound/交谈音.mp3                               "
,"Assets/Sound/舌战失败.mp3                             "
,"Assets/Sound/点击地面城池音.mp3                       "
,"Assets/Sound/褒奖征兵巡查后音.mp3                     "
,"Assets/Sound/点击都市，开发，一并等按钮音.mp3         "
,"Assets/Sound/开发设施音.mp3                           "
,"Assets/Sound/军饷0.mp3                                "
,"Assets/Sound/能力研发音.mp3                           "
,"Assets/Sound/收获金钱.mp3                             "
,"Assets/Sound/取消军团最后音效.mp3                     "
,"Assets/Sound/3286.ogg                                 "
,"Assets/Sound/修筑建筑.mp3                             "
,"Assets/Sound/耐久上升.mp3                             "
,"Assets/Sound/技巧点图标到左上角音效.mp3               "
,"Assets/Sound/技巧上升.mp3                             "
,"Assets/Sound/部队行军.mp3                             "
,"Assets/Sound/船行声.mp3                               "
,"Assets/Sound/冲车行进音.mp3                           "
,"Assets/Sound/井阑行进音.mp3                           "
,"Assets/Sound/集气.mp3                                 "
,"Assets/Sound/突刺音.mp3                               "
,"Assets/Sound/突刺音2.mp3                              "
,"Assets/Sound/弓兵集气.mp3                             "
,"Assets/Sound/弓兵发射.mp3                             "
,"Assets/Sound/弓兵射中.mp3                             "
,"Assets/Sound/骑兵突击.mp3                             "
,"Assets/Sound/骑兵突进.mp3                             "
,"Assets/Sound/会心改普攻.mp3                           "
,"Assets/Sound/带暴击图的集气.mp3                       "
,"Assets/Sound/戟兵熊手预备.mp3                         "
,"Assets/Sound/戟兵熊手成功.mp3                         "
,"Assets/Sound/戟兵横扫预备.mp3                         "
,"Assets/Sound/戟兵横扫成功.mp3                         "
,"Assets/Sound/戟兵旋风成功.mp3                         "
,"Assets/Sound/部队普通攻击.mp3                         "
,"Assets/Sound/部队普通攻击2.mp3                        "
,"Assets/Sound/部队溃灭.mp3                             "
,"Assets/Sound/部队齐攻.mp3                             "
,"Assets/Sound/使用计策预备.mp3                         "
,"Assets/Sound/火焰声.mp3                               "
,"Assets/Sound/计策灭火成功.mp3                         "
,"Assets/Sound/计策伪报成功.mp3                         "
,"Assets/Sound/计策扰乱成功.mp3                         "
,"Assets/Sound/计策内讧成功.mp3                         "
,"Assets/Sound/计策暴击预备.mp3                         "
,"Assets/Sound/计策镇静成功.mp3                         "
,"Assets/Sound/部队攻城.mp3                             "
,"Assets/Sound/箭矢音.mp3                               "
,"Assets/Sound/中箭音.mp3                               "
,"Assets/Sound/冲车破碎.mp3                             "
,"Assets/Sound/井阑战法音.mp3                           "
,"Assets/Sound/木兽战法.mp3                             "
,"Assets/Sound/投石发出.mp3                             "
,"Assets/Sound/投石射中.mp3                             "
,"Assets/Sound/设施完成.mp3                             "
,"Assets/Sound/设施被摧毁.mp3                           "
,"Assets/Sound/楼船二战法.mp3                           "
,"Assets/Sound/楼船二战法攻城+船普通撞击.mp3            "
,"Assets/Sound/楼船二战法撞到两个船.mp3                 "
        };

        string savedir = EditorUtility.OpenFolderPanel("选择语音文件夹", Application.dataPath, "");

        for (int i = 0; i < sounds.Count; i++)
        {
            string f = sounds[i].Trim();
            string fileName = savedir + "/" + f;
            if (fileName.EndsWith(".mp3") && System.IO.File.Exists(fileName))
            {
                string dstFileName = fileName.Replace(System.IO.Path.GetFileNameWithoutExtension(fileName), (30 + i).ToString());
                Debug.LogError(dstFileName);

                System.IO.File.Move(fileName, dstFileName);
            }
        }
    }


}