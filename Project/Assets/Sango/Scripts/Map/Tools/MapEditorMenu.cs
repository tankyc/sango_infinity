using UnityEngine;

namespace Sango.Tools
{
    public class MapEditorMenu
    {
        private MapEditor editor;
        
        // 顶部菜单高度常量
        private const float MENU_BAR_HEIGHT = 30f;
        
        private bool showFileMenu = false;
        private bool showEditMenu = false;
        private bool showViewMenu = false;
        private bool showRenderMenu = false;
        private bool showOriginal311Menu = false;
        private bool showHelpWindow = false;
        private Vector2 helpScrollPosition = Vector2.zero;
        
        private UnityEngine.Rect fileMenuRect;
        private UnityEngine.Rect editMenuRect;
        private UnityEngine.Rect viewMenuRect;
        private UnityEngine.Rect renderMenuRect;
        private UnityEngine.Rect original311MenuRect;

        private string[] toolbarSeason = { "秋", "春", "夏", "冬" };

        public MapEditorMenu(MapEditor editor)
        {
            this.editor = editor;
        }

        public void Draw()
        {
            DrawTopMenuBar();
        }

        // 计算菜单高度的辅助方法
        private float CalculateMenuHeight(int itemCount, int separatorCount = 0, int spaceCount = 0)
        {
            float itemHeight = 28f; // 每个菜单项的高度（增加高度）
            float separatorHeight = 20f; // 分隔线的高度（包含上下空格，增加高度）
            float spaceHeight = 5f; // 单个空格的高度
            float padding = 15f; // 菜单顶部和底部的 padding（增加 padding）

            return padding + (itemCount * itemHeight) + (separatorCount * separatorHeight) + (spaceCount * spaceHeight) + padding;
        }

        public void DrawTopMenuBar()
        {
            GUILayout.BeginArea(new UnityEngine.Rect(0, 0, Screen.width, MENU_BAR_HEIGHT), GUI.skin.box);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("文件", GUILayout.Width(60), GUILayout.Height(28)))
            {
                showFileMenu = !showFileMenu;
                showEditMenu = false;
                showViewMenu = false;
                showRenderMenu = false;
                showOriginal311Menu = false;
                showHelpWindow = false;
                // 计算文件菜单高度：5个按钮 + 1个分隔线 + 3个按钮（新增从剧本加载地图）
                float height = CalculateMenuHeight(8, 1);
                fileMenuRect = new UnityEngine.Rect(0, MENU_BAR_HEIGHT, 150, height); // Y坐标从菜单高度开始，确保不与顶部菜单重叠
            }

            if (GUILayout.Button("编辑", GUILayout.Width(60), GUILayout.Height(28)))
            {
                showEditMenu = !showEditMenu;
                showFileMenu = false;
                showViewMenu = false;
                showRenderMenu = false;
                showOriginal311Menu = false;
                showHelpWindow = false;
                // 计算编辑菜单高度：2个按钮 + 1个分隔线 + 2个按钮 + 1个分隔线 + 1个按钮（新增加载剧本）
                // 增加额外空间以确保显示完整
                float height = CalculateMenuHeight(7, 3) + 20f;
                editMenuRect = new UnityEngine.Rect(60, MENU_BAR_HEIGHT, 150, height); // Y坐标从菜单高度开始，确保不与顶部菜单重叠
            }

            if (GUILayout.Button("视图", GUILayout.Width(60), GUILayout.Height(28)))
            {
                showViewMenu = !showViewMenu;
                showFileMenu = false;
                showEditMenu = false;
                showRenderMenu = false;
                showOriginal311Menu = false;
                showHelpWindow = false;
                // 计算视图菜单高度：1个toggle + 1个按钮 + 1个分隔线 + 3个加载按钮 + 1个分隔线 + 1个按钮 + 1个标签 + 4个季节按钮
                // 增加额外空间以确保显示完整
                float height = CalculateMenuHeight(10, 2) + 30f;
                viewMenuRect = new UnityEngine.Rect(120, MENU_BAR_HEIGHT, 150, height); // Y坐标从菜单高度开始，确保不与顶部菜单重叠
            }

            if (GUILayout.Button("渲染", GUILayout.Width(60), GUILayout.Height(28)))
            {
                showRenderMenu = !showRenderMenu;
                showFileMenu = false;
                showEditMenu = false;
                showViewMenu = false;
                showOriginal311Menu = false;
                showHelpWindow = false;
                // 计算渲染菜单高度：2个按钮 + 1个开关
                float height = CalculateMenuHeight(3);
                renderMenuRect = new UnityEngine.Rect(180, MENU_BAR_HEIGHT, 150, height); // Y坐标从菜单高度开始，确保不与顶部菜单重叠
            }

            if (GUILayout.Button("原版311", GUILayout.Width(80), GUILayout.Height(28)))
            {
                showOriginal311Menu = !showOriginal311Menu;
                showFileMenu = false;
                showEditMenu = false;
                showViewMenu = false;
                showRenderMenu = false;
                showHelpWindow = false;
                // 计算原版311菜单高度：2个按钮
                float height = CalculateMenuHeight(2);
                original311MenuRect = new UnityEngine.Rect(240, MENU_BAR_HEIGHT, 150, height); // Y坐标从菜单高度开始，确保不与顶部菜单重叠
            }

            if (GUILayout.Button("帮助", GUILayout.Width(60), GUILayout.Height(28)))
            {
                showHelpWindow = !showHelpWindow;
                showFileMenu = false;
                showEditMenu = false;
                showViewMenu = false;
                showRenderMenu = false;
                showOriginal311Menu = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (showFileMenu)
            {
                DrawFileMenu();
            }

            if (showEditMenu)
            {
                DrawEditMenu();
            }

            if (showViewMenu)
            {
                DrawViewMenu();
            }

            if (showRenderMenu)
            {
                DrawRenderMenu();
            }

            if (showOriginal311Menu)
            {
                DrawOriginal311Menu();
            }

            if (showHelpWindow)
            {
                DrawHelpWindow();
            }

            if (Event.current.type == EventType.MouseDown &&
                !fileMenuRect.Contains(Event.current.mousePosition) &&
                !editMenuRect.Contains(Event.current.mousePosition) &&
                !viewMenuRect.Contains(Event.current.mousePosition) &&
                !renderMenuRect.Contains(Event.current.mousePosition) &&
                !original311MenuRect.Contains(Event.current.mousePosition))
            {
                UnityEngine.Rect menuBarRect = new UnityEngine.Rect(0, 0, Screen.width, MENU_BAR_HEIGHT);
                if (!menuBarRect.Contains(Event.current.mousePosition))
                {
                    showFileMenu = false;
                    showEditMenu = false;
                    showViewMenu = false;
                    showRenderMenu = false;
                    showOriginal311Menu = false;
                }
            }
        }

        private void DrawFileMenu()
        {
            GUILayout.BeginArea(fileMenuRect, GUI.skin.box);

            if (GUILayout.Button("新建地图"))
            {
                editor.windows.ShowNewMapWindow();
                showFileMenu = false;
            }

            if (GUILayout.Button("加载地图"))
            {
                string[] path = WindowDialog.OpenFileDialog("地图文件(*.bin)\0*.bin;\0\0");
                if (path != null)
                {
                    string fName = path[0];
                    editor.lastSavedPath = fName;
                    editor.map.LoadMap(fName);
                    editor.editorToolsBarWindow.visible = true;
                    EditorFreeCamera editorfree = Camera.main.gameObject.GetComponent<Sango.Tools.EditorFreeCamera>();
                    if (editorfree != null)
                        editorfree.lookAt = editor.map.mapCamera.GetCenterTransform();
                    BrushBase brush = editor.CheckBrush();
                    if (brush != null)
                        brush.OnEnter();
                    if (editor.ViewIs311Camera)
                        editor.SetCameraControlType(1);
                    else
                        editor.SetCameraControlType(0);
                    Sango.Log.Info($"地图已加载: {fName}");
                }
                showFileMenu = false;
            }

            if (GUILayout.Button("保存地图"))
            {
                string path = WindowDialog.SaveFileDialog("map.bin", "地图文件(*.bin)\0*.bin;\0\0");
                if (path != null)
                {
                    editor.lastSavedPath = path;
                    editor.map.SaveMap(path);
                    
                    if (editor.scenario != null && !string.IsNullOrEmpty(editor.scenario.FilePath))
                    {
                        editor.CorrectCityPositions();
                        editor.scenario.Export(editor.scenario.FilePath);
                        Sango.Log.Info("剧本数据已同步保存");
                    }
                    
                    editor.autoSave.ShowSaveNotification($"地图已保存到: {System.IO.Path.GetFileName(path)}");
                }
                showFileMenu = false;
            }

            if (GUILayout.Button("放大2倍保存"))
            {
                string path = WindowDialog.SaveFileDialog("map_2x.bin", "地图文件(*.bin)\0*.bin;\0\0");
                if (path != null)
                {
                    // 这里实现放大2倍保存的逻辑
                    editor.lastSavedPath = path;
                    editor.map.SaveScaleMap(path, 2);

                    if (editor.scenario != null && !string.IsNullOrEmpty(editor.scenario.FilePath))
                    {
                        editor.CorrectCityPositions();
                        editor.scenario.Export(editor.scenario.FilePath);
                        Sango.Log.Info("剧本数据已同步保存");
                    }

                    editor.autoSave.ShowSaveNotification($"地图已保存到: {System.IO.Path.GetFileName(path)}");
                }
                showFileMenu = false;
            }

            GUILayout.Space(5);
            GUILayout.Label("---");
            GUILayout.Space(5);

            if (GUILayout.Button("从剧本加载地图"))
            {
                string[] path = WindowDialog.OpenFileDialog("剧本文件(*.json)\0*.json;\0\0");
                if (path != null)
                {
                    editor.LoadMapFromScenario(path[0]);
                }
                showFileMenu = false;
            }

            GUILayout.Space(5);
            GUILayout.Label("---");
            GUILayout.Space(5);

            if (GUILayout.Button("设置"))
            {
                editor.windows.ShowSettingsWindow();
                showFileMenu = false;
            }

            if (GUILayout.Button("退出编辑器"))
            {
                Application.Quit();
            }

            GUILayout.EndArea();
        }

        private void DrawEditMenu()
        {
            GUILayout.BeginArea(editMenuRect, GUI.skin.box);

            if (GUILayout.Button("撤销"))
            {
                editor.undoRedoManager.Undo();
                showEditMenu = false;
            }

            if (GUILayout.Button("重做"))
            {
                editor.undoRedoManager.Redo();
                showEditMenu = false;
            }

            GUILayout.Space(5);
            GUILayout.Label("---");
            GUILayout.Space(5);

            bool historyWindowVisible = GUILayout.Toggle(editor.operationHistoryWindow.window.visible, "历史记录");
            if (historyWindowVisible != editor.operationHistoryWindow.window.visible)
            {
                editor.operationHistoryWindow.ToggleWindow();
            }

            if (GUILayout.Button("清空历史"))
            {
                editor.undoRedoManager.ClearHistory();
                showEditMenu = false;
            }

            GUILayout.Space(5);
            GUILayout.Label("---");
            GUILayout.Space(5);

            if (GUILayout.Button("加载剧本"))
            {
                string[] path = WindowDialog.OpenFileDialog("剧本文件(*.json)\0*.json;\0\0");
                if (path != null)
                {
                    editor.LoadScenario(path[0]);
                }
                showEditMenu = false;
            }

            GUILayout.EndArea();
        }

        private void DrawRenderMenu()
        {
            GUILayout.BeginArea(renderMenuRect, GUI.skin.box);

            if (GUILayout.Button("灯光设置"))
            {
                editor.windows.ShowLightWindow();
                showRenderMenu = false;
            }

            if (GUILayout.Button("雾效设置"))
            {
                editor.windows.ShowFogWindow();
                showRenderMenu = false;
            }

            // 雾效开关
            bool fogEnabled = RenderSettings.fog;
            bool newFogEnabled = GUILayout.Toggle(fogEnabled, "雾效开关");
            if (newFogEnabled != fogEnabled)
            {
                RenderSettings.fog = newFogEnabled;
            }

            GUILayout.EndArea();
        }

        private void DrawViewMenu()
        {
            GUILayout.BeginArea(viewMenuRect, GUI.skin.box);

            bool fixedView = GUILayout.Toggle(editor.ViewIs311Camera, "固定视角");
            if (fixedView != editor.ViewIs311Camera)
            {
                editor.ViewIs311Camera = fixedView;
                if (editor.ViewIs311Camera)
                    editor.SetCameraControlType(1);
                else
                    editor.SetCameraControlType(0);
            }

            if (GUILayout.Button("重置相机"))
            {
                editor.map.mapCamera.position = new Vector3(0, 500, 0);
                editor.map.mapCamera.lookRotate = new Vector3(90, -90, 0);
                editor.ViewIs311Camera = false;
                editor.SetCameraControlType(0);
                Camera.main.gameObject.transform.position = editor.map.mapCamera.position;
                Camera.main.gameObject.transform.rotation = Quaternion.Euler(90, -90, 0);
            }

            GUILayout.Space(5);
            GUILayout.Label("---");
            GUILayout.Space(5);

            if (GUILayout.Button("加载高度"))
            {
                string[] path = WindowDialog.OpenFileDialog("高度文件(*.csv)\0*.csv;\0\0");
                if (path != null)
                {
                    Sango.Log.Info("加载高度功能待实现");
                }
                showViewMenu = false;
            }

            if (GUILayout.Button("加载图层"))
            {
                string[] path = WindowDialog.OpenFileDialog("图层文件(*.csv)\0*.csv;\0\0");
                if (path != null)
                {
                    Sango.Log.Info("加载图层功能待实现");
                }
                showViewMenu = false;
            }

            if (GUILayout.Button("加载水"))
            {
                string[] path = WindowDialog.OpenFileDialog("水文件(*.csv)\0*.csv;\0\0");
                if (path != null)
                {
                    Sango.Log.Info("加载水功能待实现");
                }
                showViewMenu = false;
            }

            GUILayout.Space(5);
            GUILayout.Label("---");
            GUILayout.Space(5);

            if (GUILayout.Button("保存布局"))
            {
                editor.layoutManager.SaveLayout();
                string message = "布局已保存";
                Sango.Log.Info(message);
                editor.autoSave.ShowSaveNotification(message);
            }

            GUILayout.Label("季节:");
            // 保存原始背景颜色
            Color originalColor = GUI.backgroundColor;
            for (int i = 0; i < toolbarSeason.Length; i++)
            {
                // 如果是当前季节，设置较亮的颜色
                if (i == editor.map.curSeason)
                {
                    GUI.backgroundColor = new Color(0.7f, 0.9f, 1.0f); // 亮蓝色
                }
                else
                {
                    GUI.backgroundColor = originalColor;
                }
                
                if (GUILayout.Button(toolbarSeason[i]))
                {
                    editor.map.curSeason = i;
                    foreach (BrushBase brush in editor.brushes)
                    {
                        brush.OnSeasonChanged(i);
                    }
                }
            }
            // 恢复原始背景颜色
            GUI.backgroundColor = originalColor;

            GUILayout.EndArea();
        }

        private void DrawHelpWindow()
        {
            // 计算窗口位置，使其居中显示，且不与顶部菜单重叠
            float windowWidth = 400f;
            float windowHeight = 500f;
            float windowX = (Screen.width - windowWidth) / 2f;
            float windowY = Mathf.Max(MENU_BAR_HEIGHT + 10f, (Screen.height - windowHeight) / 2f); // 确保窗口顶部至少距离顶部菜单10像素，避免与顶部菜单重叠
            
            // 绘制窗口
            GUILayout.BeginArea(new UnityEngine.Rect(windowX, windowY, windowWidth, windowHeight), GUI.skin.window);
            
            // 窗口标题
            GUILayout.BeginHorizontal();
            GUILayout.Label("编辑器帮助", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16 });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭", GUILayout.Width(60)))
            {
                showHelpWindow = false;
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // 滚动视图
            helpScrollPosition = GUILayout.BeginScrollView(helpScrollPosition, GUILayout.Width(windowWidth - 20), GUILayout.Height(windowHeight - 60));
            
            // 帮助内容
            GUILayout.Label("基本操作:");
            GUILayout.Label("- 鼠标左键: 选择/编辑");
            GUILayout.Label("- 鼠标右键: 平移视图");
            GUILayout.Label("- 滚轮: 缩放视图");
            GUILayout.Space(15);

            GUILayout.Label("编辑模式:");
            GUILayout.Label("- 基础编辑: 选择和查看");
            GUILayout.Label("- 编辑地形: 修改地形高度和纹理");
            GUILayout.Label("- 编辑地格: 修改地格属性");
            GUILayout.Label("- 模型放置: 添加和编辑模型");
            GUILayout.Space(15);

            GUILayout.Label("快捷键:");
            GUILayout.Label("- Ctrl+S: 保存地图");
            GUILayout.Label("- Ctrl+Z: 撤销操作");
            GUILayout.Label("- Ctrl+Y: 重做操作");
            GUILayout.Label("- Ctrl+O: 加载地图");
            GUILayout.Space(15);

            GUILayout.Label("提示:");
            GUILayout.Label("- 按住Shift键可以进行精确操作");
            GUILayout.Label("- 使用操作历史窗口可以查看和跳转操作");
            GUILayout.Label("- 自动保存功能可以在设置中配置");
            GUILayout.Space(15);
            
            // 结束滚动视图
            GUILayout.EndScrollView();
            
            // 结束窗口
            GUILayout.EndArea();
        }

        private void DrawOriginal311Menu()
        {
            GUILayout.BeginArea(original311MenuRect, GUI.skin.box);

            if (GUILayout.Button("导出地格"))
            {
                string path = WindowDialog.SaveFileDialog("grid.csv", "CSV文件(*.csv)\0*.csv;\0\0");
                if (path != null)
                {
                    // 这里实现导出地格的逻辑
                    Sango.Log.Info("地格导出功能待实现");
                }
                showOriginal311Menu = false;
            }

            if (GUILayout.Button("导入地格"))
            {
                string[] path = WindowDialog.OpenFileDialog("CSV文件(*.csv)\0*.csv;\0\0");
                if (path != null)
                {
                    // 这里实现导入地格的逻辑
                    Sango.Log.Info("地格导入功能待实现");
                }
                showOriginal311Menu = false;
            }

            GUILayout.EndArea();
        }
    }
}
