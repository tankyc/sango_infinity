using UnityEngine;

namespace Sango.Tools
{
    public class MapEditorWindows
    {
        private MapEditor editor;
        
        // 顶部菜单高度常量
        private const float MENU_BAR_HEIGHT = 30f;
        
        private bool showSettingsWindow = false;
        private bool showNewMapWindow = false;
        private bool showLightWindow = false;
        private bool showFogWindow = false;
        
        private UnityEngine.Rect settingsWindowRect;
        private UnityEngine.Rect newMapWindowRect;
        private UnityEngine.Rect lightWindowRect;
        private UnityEngine.Rect fogWindowRect;
        
        private int newMapWidth = 1024;
        private int newMapHeight = 1024;
        private string newMapKey = "";
        
        private Vector2 settingsScrollPosition = Vector2.zero;

        public MapEditorWindows(MapEditor editor)
        {
            this.editor = editor;
        }

        public void Draw()
        {
            DrawSettingsWindow();
            DrawNewMapWindow();
            DrawLightWindow();
            DrawFogWindow();
        }

        public void ShowSettingsWindow()
        {
            showSettingsWindow = true;
        }

        public void ShowNewMapWindow()
        {
            showNewMapWindow = true;
        }

        public void ShowLightWindow()
        {
            showLightWindow = true;
        }

        public void ShowFogWindow()
        {
            showFogWindow = true;
        }

        private void DrawSettingsWindow()
        {
            if (showSettingsWindow)
            {
                if (settingsWindowRect.width == 0)
                {
                    float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 280); // 确保窗口顶部至少距离顶部菜单，避免与顶部菜单重叠
                    settingsWindowRect = new UnityEngine.Rect(
                        Screen.width / 2 - 280,
                        windowY,
                        560,
                        560
                    );
                }

                // 确保窗口不会与顶部菜单重叠
                if (settingsWindowRect.y < MENU_BAR_HEIGHT)
                {
                    settingsWindowRect.y = MENU_BAR_HEIGHT;
                }

                GUI.DrawTexture(settingsWindowRect, CreateWindowBackground());

                GUILayout.BeginArea(settingsWindowRect);

                GUILayout.BeginHorizontal();
                GUILayout.Label("⚙️ 设置", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    showSettingsWindow = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(15);

                settingsScrollPosition = GUILayout.BeginScrollView(settingsScrollPosition, GUILayout.Height(410));

                DrawSettingsSection("⌨️ 快捷键设置", () =>
                {
                    DrawSettingRow("保存快捷键", $"Ctrl + {editor.shortcuts.save}", () => { });
                    DrawSettingRow("撤销快捷键", $"Ctrl + {editor.shortcuts.undo}", () => { });
                    DrawSettingRow("重做快捷键", $"Ctrl + {editor.shortcuts.redo}", () => { });
                    DrawSettingRow("加载快捷键", $"Ctrl + {editor.shortcuts.load}", () => { });
                    DrawSettingRow("切换视角", $"{editor.shortcuts.toggleView}", () => { });

                    if (GUILayout.Button("重置为默认", GUILayout.Width(150)))
                    {
                        editor.shortcuts.ResetToDefault();
                    }
                });

                DrawSettingsSection("� MapKey设置", () =>
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("地图标识:", GUILayout.Width(100));
                    editor.map.WorkContent = GUILayout.TextField(editor.map.WorkContent, GUILayout.Width(300));
                    GUILayout.EndHorizontal();
                });

                DrawSettingsSection("💾 自动保存设置", () =>
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("启用自动保存:", GUILayout.Width(130));
                    editor.autoSave.AutoSaveEnabled = GUILayout.Toggle(editor.autoSave.AutoSaveEnabled, "");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("自动保存间隔(分钟):", GUILayout.Width(150));
                    float interval = EditorUtility.FloatField(editor.autoSave.AutoSaveInterval / 60f, GUILayout.MaxWidth(70));
                    if (GUI.changed)
                    {
                        interval = Mathf.Clamp(interval, 1f, 60f);
                        editor.autoSave.AutoSaveInterval = interval * 60f;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("自动保存数量限制:", GUILayout.Width(150));
                    int limit = EditorUtility.IntField(editor.autoSave.AutoSaveLimit, GUILayout.MaxWidth(70));
                    if (GUI.changed)
                    {
                        limit = Mathf.Clamp(limit, 1, 20);
                        editor.autoSave.AutoSaveLimit = limit;
                    }
                    GUILayout.EndHorizontal();
                });

                DrawSettingsSection("🎥 相机设置", () =>
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("移动速度:", GUILayout.Width(130));
                    float moveSpeed = EditorUtility.FloatField(editor.map.mapCamera.keyBoardMoveSpeed, GUILayout.MaxWidth(70));
                    if (GUI.changed)
                    {
                        editor.map.mapCamera.keyBoardMoveSpeed = moveSpeed;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("可视距离:", GUILayout.Width(130));
                    float viewDistance = EditorUtility.FloatField(editor.map.showLimitLength, GUILayout.MaxWidth(70));
                    if (GUI.changed)
                    {
                        editor.map.showLimitLength = viewDistance;
                    }
                    GUILayout.EndHorizontal();
                });

                GUILayout.EndScrollView();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("应用", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    ApplySettings();
                }
                if (GUILayout.Button("取消", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    showSettingsWindow = false;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndArea();
            }
        }

        private void DrawNewMapWindow()
        {
            if (showNewMapWindow)
            {
                if (newMapWindowRect.width == 0)
                {
                    float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 200); // 确保窗口顶部至少距离顶部菜单，避免与顶部菜单重叠
                    newMapWindowRect = new UnityEngine.Rect(
                        Screen.width / 2 - 250,
                        windowY,
                        500,
                        400
                    ); 
                }

                // 确保窗口不会与顶部菜单重叠
                if (newMapWindowRect.y < MENU_BAR_HEIGHT)
                {
                    newMapWindowRect.y = MENU_BAR_HEIGHT;
                }

                GUI.DrawTexture(newMapWindowRect, CreateWindowBackground());

                GUILayout.BeginArea(newMapWindowRect);

                GUILayout.BeginHorizontal();
                GUILayout.Label("🗺️ 新建地图", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    showNewMapWindow = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(15);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("地图参数", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
                GUILayout.Space(8);

                GUILayout.BeginHorizontal();
                GUILayout.Label("宽度:", GUILayout.Width(100));
                string widthStr = GUILayout.TextField(newMapWidth.ToString(), GUILayout.Width(100));
                if (int.TryParse(widthStr, out int width) && width > 0)
                {
                    newMapWidth = width;
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("高度:", GUILayout.Width(100));
                string heightStr = GUILayout.TextField(newMapHeight.ToString(), GUILayout.Width(100));
                if (int.TryParse(heightStr, out int height) && height > 0)
                {
                    newMapHeight = height;
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.Space(12);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("MapKey <color=red>*必填</color>", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
                GUILayout.Space(8);

                GUILayout.Label("剧本地图需要填写此标识,会在Assets/Map/下面新建一个此名字文件夹,地图相关资源会优先在此存放和读取!!");

                GUILayout.BeginHorizontal();
                GUILayout.Label("地图标识:", GUILayout.Width(100));
                newMapKey = GUILayout.TextField(newMapKey, GUILayout.Width(350));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(15);

                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("创建", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    if (newMapWidth <= 0 || newMapHeight <= 0)
                    {
                        Sango.Log.Error("地图宽度和高度必须为正整数");
                        return;
                    }
                    if (string.IsNullOrEmpty(newMapKey))
                    {
                        Sango.Log.Error("MapKey不能为空");
                        return;
                    }

                    editor.CreateNewMap(newMapWidth, newMapHeight, newMapKey);
                    editor.lastSavedPath = "";
                    showNewMapWindow = false;
                    Sango.Log.Info($"已创建新地图: {newMapWidth} x {newMapHeight}");
                }
                if (GUILayout.Button("取消", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    showNewMapWindow = false;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndArea();
            }
        }

        private void DrawLightWindow()
        {
            if (showLightWindow)
            {
                if (lightWindowRect.width == 0)
                {
                    float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 280); // 确保窗口顶部至少距离顶部菜单，避免与顶部菜单重叠
                    lightWindowRect = new UnityEngine.Rect(
                        Screen.width / 2 - 300,
                        windowY,
                        600,
                        560
                    );
                }

                // 确保窗口不会与顶部菜单重叠
                if (lightWindowRect.y < MENU_BAR_HEIGHT)
                {
                    lightWindowRect.y = MENU_BAR_HEIGHT;
                }

                GUI.DrawTexture(lightWindowRect, CreateWindowBackground());

                GUILayout.BeginArea(lightWindowRect);

                GUILayout.BeginHorizontal();
                GUILayout.Label("💡 灯光设置", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    showLightWindow = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(15);

                string[] seasons = { "秋", "春", "夏", "冬" };

                // 保存原始背景颜色
                Color originalColor = GUI.backgroundColor;
                
                for (int i = 0; i < 4; i++)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.BeginHorizontal();

                    // 如果是当前季节，设置较亮的颜色
                    if (i == editor.map.curSeason)
                    {
                        GUI.backgroundColor = new Color(0.7f, 0.9f, 1.0f); // 亮蓝色
                    }
                    else
                    {
                        GUI.backgroundColor = originalColor;
                    }
                    
                    if (GUILayout.Button(seasons[i], GUILayout.Width(50), GUILayout.Height(30)))
                    {
                        editor.map.curSeason = i;
                        foreach (BrushBase brush in editor.brushes)
                        {
                            brush.OnSeasonChanged(i);
                        }
                    }

                    GUILayout.Space(15);

                    GUILayout.Label("灯光颜色:", GUILayout.Width(70));
                    int seasonIndex = i;
                    EditorUtility.ColorField(editor.map.mapLight.light_color[seasonIndex], "", (color) => { editor.map.mapLight.light_color[seasonIndex] = color; });

                    GUILayout.Label("灯光强度:", GUILayout.Width(70));
                    editor.map.mapLight.light_intensity[i] = EditorUtility.FloatField(editor.map.mapLight.light_intensity[i], GUILayout.Width(80));
                    editor.map.mapLight.light_intensity[i] = Mathf.Max(0, editor.map.mapLight.light_intensity[i]);

                    GUILayout.Label("灯光方向:", GUILayout.Width(70));
                    editor.map.mapLight.light_direction[i] = EditorUtility.Vector3Field(editor.map.mapLight.light_direction[i], "");

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(10);
                }
                
                // 恢复原始背景颜色
                GUI.backgroundColor = originalColor;

                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("应用", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    editor.map.mapLight.UpdateRender();
                    string message = "灯光设置已应用";
                    Sango.Log.Info(message);
                    editor.autoSave.ShowSaveNotification(message);
                }
                if (GUILayout.Button("关闭", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    showLightWindow = false;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndArea();
            }
        }

        private void DrawFogWindow()
        {
            if (showFogWindow)
            {
                if (fogWindowRect.width == 0)
                {
                    float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 280); // 确保窗口顶部至少距离顶部菜单，避免与顶部菜单重叠
                    fogWindowRect = new UnityEngine.Rect(
                        Screen.width / 2 - 300,
                        windowY,
                        600,
                        560
                    );
                }

                // 确保窗口不会与顶部菜单重叠
                if (fogWindowRect.y < MENU_BAR_HEIGHT)
                {
                    fogWindowRect.y = MENU_BAR_HEIGHT;
                }

                GUI.DrawTexture(fogWindowRect, CreateWindowBackground());

                GUILayout.BeginArea(fogWindowRect);

                GUILayout.BeginHorizontal();
                GUILayout.Label("🌫️ 雾效设置", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    showFogWindow = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(15);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("雾效显示开关", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                GUILayout.Label("启用雾效:", GUILayout.Width(80));
                RenderSettings.fog = GUILayout.Toggle(RenderSettings.fog, "");
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(12);

                string[] seasons = { "秋", "春", "夏", "冬" };

                // 保存原始背景颜色
                Color originalColor = GUI.backgroundColor;
                
                for (int i = 0; i < 4; i++)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.BeginHorizontal();

                    // 如果是当前季节，设置较亮的颜色
                    if (i == editor.map.curSeason)
                    {
                        GUI.backgroundColor = new Color(0.7f, 0.9f, 1.0f); // 亮蓝色
                    }
                    else
                    {
                        GUI.backgroundColor = originalColor;
                    }
                    
                    if (GUILayout.Button(seasons[i], GUILayout.Width(50), GUILayout.Height(30)))
                    {
                        editor.map.curSeason = i;
                        foreach (BrushBase brush in editor.brushes)
                        {
                            brush.OnSeasonChanged(i);
                        }
                    }

                    GUILayout.Space(15);

                    GUILayout.Label("雾颜色:", GUILayout.Width(60));
                    int fogSeasonIndex = i;
                    EditorUtility.ColorField(editor.map.mapFog.fog_color[fogSeasonIndex], "", (color) => { 
                        editor.map.mapFog.fog_color[fogSeasonIndex] = color;
                        if (fogSeasonIndex == editor.map.curSeason)
                        {
                            editor.map.mapFog.UpdateRender();
                        }
                    });

                    GUILayout.Label("起点距离:", GUILayout.Width(70));
                    float newStart = EditorUtility.FloatField(editor.map.mapFog.fog_start[i], GUILayout.Width(80));
                    if (newStart != editor.map.mapFog.fog_start[i])
                    {
                        editor.map.mapFog.fog_start[i] = Mathf.Max(0, newStart);
                        if (i == editor.map.curSeason)
                        {
                            editor.map.mapFog.UpdateRender();
                        }
                    }

                    GUILayout.Label("结束距离:", GUILayout.Width(70));
                    float newEnd = EditorUtility.FloatField(editor.map.mapFog.fog_end[i], GUILayout.Width(80));
                    if (newEnd != editor.map.mapFog.fog_end[i])
                    {
                        editor.map.mapFog.fog_end[i] = Mathf.Max(editor.map.mapFog.fog_start[i], newEnd);
                        if (i == editor.map.curSeason)
                        {
                            editor.map.mapFog.UpdateRender();
                        }
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(10);
                }
                
                // 恢复原始背景颜色
                GUI.backgroundColor = originalColor;

                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("应用", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    editor.map.mapFog.UpdateRender();
                    string message = "雾效设置已应用";
                    Sango.Log.Info(message);
                    editor.autoSave.ShowSaveNotification(message);
                }
                if (GUILayout.Button("关闭", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    showFogWindow = false;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndArea();
            }
        }

        private void DrawSettingsSection(string title, System.Action content)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(title, new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(8);
            content();
            GUILayout.EndVertical();
            GUILayout.Space(12);
        }

        private void DrawSettingRow(string label, string value, System.Action onModify)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", GUILayout.Width(130));
            GUILayout.Label(value, new GUIStyle(GUI.skin.label) { normal = new GUIStyleState { textColor = Color.cyan } });
            if (GUILayout.Button("修改", GUILayout.Width(70)))
            {
                onModify();
            }
            GUILayout.EndHorizontal();
        }

        private Texture2D CreateWindowBackground()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.18f, 0.95f));
            texture.Apply();
            return texture;
        }

        private void ApplySettings()
        {
            showSettingsWindow = false;
            string message = "设置已应用";
            Sango.Log.Info(message);
            editor.autoSave.ShowSaveNotification(message);
        }
    }
}
