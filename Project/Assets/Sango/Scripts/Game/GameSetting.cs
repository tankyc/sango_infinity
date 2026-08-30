/*
 * 文件名：GameSetting.cs
 * 描述：游戏设置类，存储和管理游戏的所有设置信息
 * 创建日期：2026-03-27
 * 最后修改：2026-04-03
 */

using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using System;

namespace Sango.Core
{
    /// <summary>
    /// 游戏设置参数类，存储和管理游戏的设置信息
    /// </summary>
    public class GameSetting : Singleton<GameSetting>
    {
        int src_ScreenWidth;
        int src_ScreenHeight;

        #region 分辨率设置
        /// <summary>
        /// 屏幕宽度
        /// </summary>
        public int ScreenWidth { get; set; } = 1920;

        /// <summary>
        /// 屏幕高度
        /// </summary>
        public int ScreenHeight { get; set; } = 1080;

        /// <summary>
        /// 是否全屏
        /// </summary>
        public bool IsFullScreen { get; set; } = true;

        /// <summary>
        /// 当前分辨率在列表中的索引
        /// </summary>
        public int CurrentResolutionIndex { get; set; } = 0;

        /// <summary>
        /// 支持的分辨率列表
        /// </summary>
        public List<Resolution> SupportedResolutions { get; private set; } = new List<Resolution>();
        #endregion

        #region 音频设置
        /// <summary>
        /// 背景音乐音量（0-1）
        /// </summary>
        public float BgmVolume { get; set; } = 0.6f;

        /// <summary>
        /// 音效音量（0-1）
        /// </summary>
        public float SfxVolume { get; set; } = 1.0f;

        // <summary>
        /// 语音音效音量（0-1）
        /// </summary>
        public float VoiceVolume { get; set; } = 1.0f;


        /// <summary>
        /// 主音量（0-1）
        /// </summary>
        public float MasterVolume { get; set; } = 1.0f;
        #endregion

        #region 图形设置
        /// <summary>
        /// 垂直同步
        /// </summary>
        public bool VSync { get; set; } = true;

        /// <summary>
        /// 帧率限制
        /// </summary>
        public int FrameRateLimit { get; set; } = 30;

        /// <summary>
        /// 当前帧率在列表中的索引
        /// </summary>
        public int CurrentFrameRateIndex { get; set; } = 0;

        /// <summary>
        /// 支持的帧率列表
        /// </summary>
        public List<int> SupportedFrameRates { get; private set; } = new List<int>();

        /// <summary>
        /// 图形质量等级
        /// </summary>
        public int QualityLevel { get; set; } = 2; // 0=低, 1=中, 2=高, 3=超高

        /// <summary>
        /// 后处理开关（Bloom / 景深 / 色彩分级等屏幕后处理效果总开关）
        /// </summary>
        public bool IsPostProcessingEnabled { get; set; } = true;

        /// <summary>
        /// 阴影开关
        /// </summary>
        public bool IsShadowEnabled { get; set; } = true;

        /// <summary>
        /// 画质预设（0=低，1=中，2=高）
        /// </summary>
        public int QualityPreset { get; set; } = 2;
        #endregion

        #region 控制设置
        /// <summary>
        /// 键盘移动速度
        /// </summary>
        public float KeyboardMoveSpeed { get; set; } = 300f;

        /// <summary>
        /// 移动方式（0=手动，1=自动）
        /// </summary>
        public int MovementMode { get; set; } = 0;

        /// <summary>
        /// 移动端的取消按钮
        /// </summary>
        public bool MobileCancel { get; set; } = false;

        #endregion

        #region 语言设置
        /// <summary>
        /// 当前语言
        /// </summary>
        public string Language { get; set; } = "zh-CN";
        #endregion

        #region 界面设置
        /// <summary>
        /// 大字体开关
        /// </summary>
        public bool IsLargeFontEnabled { get; set; } = false;

        /// <summary>
        /// 大字体缩放倍率
        /// </summary>
        public int LargeFontScaleFactor { get; set; } = 0;


        public string FontName { get; set; } = "kaiti";
        /// <summary>
        /// 当前帧率在列表中的索引
        /// </summary>
        public int CurrentFontIndex { get; set; } = 0;
        #endregion

        #region 构造函数
        /// <summary>
        /// 构造函数
        /// </summary>
        public GameSetting()
        {

        }
        #endregion

        public List<FontData> FontDatas = new List<FontData>();
        public struct FontData
        {
            public string fontName;
            public string place;
            public Font Load()
            {
                if (string.IsNullOrEmpty(place))
                    return Resources.Load<Font>(fontName);
                else
                {
                    switch (place)
                    {
                        case "os":
                            return Font.CreateDynamicFontFromOSFont(fontName, 25);
                    }
                }
                return null;
            }
        }

        public Font Font { get; set; }

        #region 初始化方法
        /// <summary>
        /// 初始化游戏设置
        /// </summary>
        public void Initialize()
        {
            src_ScreenWidth = Screen.width; src_ScreenHeight = Screen.height;

            FontDatas.Add(new FontData()
            {
                fontName = "kaiti",
            });

            FontDatas.Add(new FontData()
            {
                fontName = "alibaba",
            });

            FontDatas.Add(new FontData()
            {
                fontName = "siyuancn",
            });

            string[] osFontNames = Font.GetOSInstalledFontNames();
            foreach (string o in osFontNames)
            {
                FontDatas.Add(new FontData()
                {
                    fontName = o,
                    place = "os"
                });
            }

            LoadSettings();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            InitializeResolutions();
#endif
            InitializeFrameRates();

            CurrentFontIndex = 0;
            for (int i = 0; i < FontDatas.Count; i++)
            {
                FontData fontData = FontDatas[i];
                if (fontData.fontName.Equals(FontName))
                {
                    CurrentFontIndex = i;
                    Font = fontData.Load();
                }
            }

            // 监听游戏设置事件
            GameEvent.OnGameSetting += OnGameSetting;

            // 监听剧本初始化事件
            GameEvent.OnScenarioInit += OnScenarioInit;

            ApplyAllSettings();
        }

        /// <summary>
        /// 初始化支持的分辨率列表
        /// </summary>
        private void InitializeResolutions()
        {
            Resolution[] resolutions = Screen.resolutions;
            List<Resolution> uniqueResolutions = new List<Resolution>();

            foreach (Resolution resolution in resolutions)
            {
                bool isDuplicate = false;
                foreach (Resolution existing in uniqueResolutions)
                {
                    if (existing.width == resolution.width && existing.height == resolution.height)
                    {
                        isDuplicate = true;
                        break;
                    }
                }
                if (!isDuplicate)
                {
                    uniqueResolutions.Add(resolution);
                }
            }

            // 按分辨率从大到小排序
            uniqueResolutions.Sort((a, b) =>
            {
                int areaA = a.width * a.height;
                int areaB = b.width * b.height;
                return areaB.CompareTo(areaA);
            });

            SupportedResolutions = uniqueResolutions;

            // 查找当前分辨率的索引
            FindCurrentResolutionIndex();
        }

        /// <summary>
        /// 查找当前分辨率在列表中的索引
        /// </summary>
        private void FindCurrentResolutionIndex()
        {
            for (int i = 0; i < SupportedResolutions.Count; i++)
            {
                Resolution resolution = SupportedResolutions[i];
                if (resolution.width == ScreenWidth && resolution.height == ScreenHeight)
                {
                    CurrentResolutionIndex = i;
                    return;
                }
            }

            // 如果没找到，使用第一个分辨率
            if (SupportedResolutions.Count > 0)
            {
                CurrentResolutionIndex = 0;
                ScreenWidth = SupportedResolutions[0].width;
                ScreenHeight = SupportedResolutions[0].height;
            }
        }

        /// <summary>
        /// 初始化支持的帧率列表
        /// </summary>
        private void InitializeFrameRates()
        {
            // 常用的帧率选项
            SupportedFrameRates = new List<int>
            {
                30,
                60,
                75,
                90,
                120,
                144,
                165,
                240
            };

            // 查找当前帧率的索引
            FindCurrentFrameRateIndex();
        }

        /// <summary>
        /// 查找当前帧率在列表中的索引
        /// </summary>
        private void FindCurrentFrameRateIndex()
        {
            for (int i = 0; i < SupportedFrameRates.Count; i++)
            {
                int frameRate = SupportedFrameRates[i];
                if (frameRate == FrameRateLimit)
                {
                    CurrentFrameRateIndex = i;
                    return;
                }
            }

            // 如果没找到，使用60fps
            CurrentFrameRateIndex = 1; // 60fps的索引
            FrameRateLimit = 60;
        }
        #endregion

        #region 应用设置
        /// <summary>
        /// 应用分辨率设置
        /// </summary>
        public void ApplyResolution()
        {
            Screen.SetResolution(ScreenWidth, ScreenHeight, IsFullScreen);

            // 更新当前分辨率索引
            FindCurrentResolutionIndex();

            PlayerPrefs.SetInt("ScreenWidth", ScreenWidth);
            PlayerPrefs.SetInt("ScreenHeight", ScreenHeight);
            PlayerPrefs.SetInt("IsFullScreen", IsFullScreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 应用音频设置
        /// </summary>
        public void ApplyAudioSettings()
        {
            if (Sango.Manager.AudioManager.Instance != null)
            {
                Sango.Manager.AudioManager.Instance.SetBgmVolume(BgmVolume);
                Sango.Manager.AudioManager.Instance.SetSfxVolume(SfxVolume);
                Sango.Manager.AudioManager.Instance.SetVoiceVolume(VoiceVolume);
            }

            PlayerPrefs.SetFloat("BgmVolume", BgmVolume);
            PlayerPrefs.SetFloat("SfxVolume", SfxVolume);
            PlayerPrefs.SetFloat("VoiceVolume", VoiceVolume);
            PlayerPrefs.SetFloat("MasterVolume", MasterVolume);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 应用图形设置
        /// </summary>
        public void ApplyGraphicsSettings()
        {
#if UNITY_ANDROID || UNITY_IPHONE
            Application.targetFrameRate = FrameRateLimit;
#else
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = FrameRateLimit;
            QualitySettings.SetQualityLevel(QualityLevel);
#endif

            // 更新当前帧率索引
            FindCurrentFrameRateIndex();

            PlayerPrefs.SetInt("VSync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("FrameRateLimit", FrameRateLimit);
            PlayerPrefs.SetInt("QualityLevel", QualityLevel);
            PlayerPrefs.SetInt("IsPostProcessingEnabled", IsPostProcessingEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 应用后处理开关
        /// 触发 OnPostProcessingChanged 事件，由监听者（如 PostProcessVolume 控制器）负责实际启用/禁用。
        /// 同时将设置写入 PlayerPrefs 持久化。
        /// </summary>
        public void ApplyPostProcessingSettings()
        {
            // 通知外部监听者（如 PostProcessVolume 控制器）处理
            GameEvent.OnPostProcessingChanged?.Invoke(IsPostProcessingEnabled);

            Camera camera = Camera.main;
            UniversalAdditionalCameraData additionalCameraData = camera.GetUniversalAdditionalCameraData();
            additionalCameraData.renderPostProcessing = IsPostProcessingEnabled;

            // 写入 PlayerPrefs 持久化
            PlayerPrefs.SetInt("IsPostProcessingEnabled", IsPostProcessingEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 应用阴影设置
        /// 具体效果留空，后续补充代码
        /// </summary>
        public void ApplyShadowSettings()
        {
            // TODO: 根据 IsShadowEnabled 控制场景阴影的开关


            // 写入 PlayerPrefs 持久化
            PlayerPrefs.SetInt("IsShadowEnabled", IsShadowEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 应用画质预设设置
        /// 具体效果留空，后续补充代码
        /// </summary>
        public void ApplyQualityPreset()
        {
            // TODO: 根据 QualityPreset 设置对应的画质参数
            // 0=低，1=中，2=高
            UnityEngine.QualitySettings.SetQualityLevel(QualityPreset);

#if UNITY_ANDROID || UNITY_IPHONE
            switch (QualityPreset)
            {
                case 0:
                    {
                        int height = src_ScreenHeight * 6 / 10;
                        int width = src_ScreenWidth * 6 / 10;
                        Screen.SetResolution(width, height, true);
                    }
                    break;
                case 1:
                    {
                        int height = src_ScreenHeight * 8 / 10;
                        int width = src_ScreenWidth * 8 / 10;
                        Screen.SetResolution(width, height, true);
                    }
                    break;
                case 2:
                    Screen.SetResolution(src_ScreenWidth, src_ScreenHeight, true);
                    break;
            }
#endif
            // 写入 PlayerPrefs 持久化
            PlayerPrefs.SetInt("QualityPreset", QualityPreset);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 应用控制设置
        /// </summary>
        public void ApplyControlSettings()
        {
            PlayerPrefs.SetFloat("KeyboardMoveSpeed", KeyboardMoveSpeed);
            PlayerPrefs.SetInt("MovementMode", MovementMode);
            PlayerPrefs.SetInt("MobileCancel1", MobileCancel ? 1 : 0);

            if(MobileCancel)
                Window.Instance.Open("window_mobile_cancel");
            else
                Window.Instance.Close("window_mobile_cancel");

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 应用语言设置
        /// </summary>
        public void ApplyLanguageSettings()
        {
            PlayerPrefs.SetString("Language", Language);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 应用大字体设置
        /// </summary>
        public void ApplyLargeFontSettings()
        {
            PlayerPrefs.SetString("FontName", FontDatas[CurrentFontIndex].fontName);
            PlayerPrefs.SetInt("IsLargeFontEnabled", IsLargeFontEnabled ? 1 : 0);
            PlayerPrefs.SetInt("LargeFontScaleFactor", LargeFontScaleFactor);
            PlayerPrefs.Save();
        }



        /// <summary>
        /// 应用所有设置
        /// </summary>
        public void ApplyAllSettings()
        {
#if UNITY_STANDALONE_WIN
            ApplyResolution();
#endif
            ApplyGraphicsSettings();
            ApplyPostProcessingSettings();
            ApplyAudioSettings();
            ApplyControlSettings();
            ApplyLanguageSettings();
            ApplyLargeFontSettings();
            ApplyShadowSettings();
            ApplyQualityPreset();
        }

        #endregion

        #region 加载设置
        /// <summary>
        /// 从PlayerPrefs加载设置
        /// </summary>
        public void LoadSettings()
        {
            // 分辨率设置
            ScreenWidth = PlayerPrefs.GetInt("ScreenWidth", 1920);
            ScreenHeight = PlayerPrefs.GetInt("ScreenHeight", 1080);
            IsFullScreen = PlayerPrefs.GetInt("IsFullScreen", 1) == 1;

            // 音频设置
            BgmVolume = PlayerPrefs.GetFloat("BgmVolume", 0.6f);
            SfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1.0f);
            VoiceVolume = PlayerPrefs.GetFloat("VoiceVolume", 1.0f);
            MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);

            // 图形设置
            VSync = PlayerPrefs.GetInt("VSync", 1) == 1;
            FrameRateLimit = PlayerPrefs.GetInt("FrameRateLimit", 60);
            QualityLevel = PlayerPrefs.GetInt("QualityLevel", 2);

            // 后处理开关
            IsPostProcessingEnabled = PlayerPrefs.GetInt("IsPostProcessingEnabled", 1) == 1;

            // 控制设置
            KeyboardMoveSpeed = PlayerPrefs.GetFloat("KeyboardMoveSpeed", 300f);
            MovementMode = PlayerPrefs.GetInt("MovementMode", 0);
            MobileCancel = PlayerPrefs.GetInt("MobileCancel1", 1) == 1;
            // 语言设置
            Language = PlayerPrefs.GetString("Language", "zh-CN");

            // 界面设置
            IsLargeFontEnabled = PlayerPrefs.GetInt("IsLargeFontEnabled", 0) == 1;
            LargeFontScaleFactor = PlayerPrefs.GetInt("LargeFontScaleFactor", 0);
            FontName = PlayerPrefs.GetString("FontName", "kaiti");

            // 图形设置 - 阴影与画质
            IsShadowEnabled = PlayerPrefs.GetInt("IsShadowEnabled", 1) == 1;
            QualityPreset = PlayerPrefs.GetInt("QualityPreset", 2);
        }

        #endregion

        #region 重置设置
        /// <summary>
        /// 重置所有设置为默认值
        /// </summary>
        public void ResetSettings()
        {
            // 分辨率设置
            ScreenWidth = 1920;
            ScreenHeight = 1080;
            IsFullScreen = true;

            // 音频设置
            BgmVolume = 0.6f;
            SfxVolume = 1.0f;
            VoiceVolume = 1.0f;
            MasterVolume = 1.0f;

            // 图形设置
            VSync = true;
            FrameRateLimit = 60;
            QualityLevel = 2;
            IsPostProcessingEnabled = true;

            // 控制设置
            KeyboardMoveSpeed = 300f;
            MovementMode = 0;
            MobileCancel = true;

            // 语言设置
            Language = "zh-CN";

            // 界面设置
            IsLargeFontEnabled = false;
            LargeFontScaleFactor = 0;
            FontName = "kaiti";

            // 图形设置 - 阴影与画质
            IsShadowEnabled = true;
            QualityPreset = 2;

            // 清除PlayerPrefs
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // 应用设置
            ApplyAllSettings();
        }

        #endregion

        #region 分辨率操作
        /// <summary>
        /// 设置分辨率
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        public void SetResolution(int width, int height)
        {
            ScreenWidth = width;
            ScreenHeight = height;
            ApplyResolution();
        }

        /// <summary>
        /// 通过索引设置分辨率
        /// </summary>
        /// <param name="index">分辨率索引</param>
        public void SetResolutionByIndex(int index)
        {
            if (index >= 0 && index < SupportedResolutions.Count)
            {
                Resolution resolution = SupportedResolutions[index];
                ScreenWidth = resolution.width;
                ScreenHeight = resolution.height;
                CurrentResolutionIndex = index;
                ApplyResolution();
            }
        }

        /// <summary>
        /// 设置全屏状态
        /// </summary>
        /// <param name="isFullScreen">是否全屏</param>
        public void SetFullScreen(bool isFullScreen)
        {
            IsFullScreen = isFullScreen;
            Screen.fullScreen = IsFullScreen;
        }

        /// <summary>
        /// 切换全屏状态
        /// </summary>
        public void ToggleFullScreen()
        {
            IsFullScreen = !IsFullScreen;
            Screen.fullScreen = IsFullScreen;
        }
        #endregion

        #region 帧率操作
        /// <summary>
        /// 设置帧率限制
        /// </summary>
        /// <param name="frameRate">帧率</param>
        public void SetFrameRate(int frameRate)
        {
            FrameRateLimit = frameRate;
            ApplyGraphicsSettings();
        }

        /// <summary>
        /// 通过索引设置帧率
        /// </summary>
        /// <param name="index">帧率索引</param>
        public void SetFrameRateByIndex(int index)
        {
            if (index >= 0 && index < SupportedFrameRates.Count)
            {
                FrameRateLimit = SupportedFrameRates[index];
                CurrentFrameRateIndex = index;
                ApplyGraphicsSettings();
            }
        }
        #endregion

        #region 事件处理
        /// <summary>
        /// 处理游戏设置事件
        /// </summary>
        /// <param name="setting">设置接口</param>
        /// <param name="scenario">剧本</param>
        private void OnGameSetting(IVariablesSetting setting)
        {
            // 分辨率设置
            setting.AddBigTitle("显示设置");
#if UNITY_STANDALONE_WIN || UNITY_EDITOR


            // 创建分辨率选项列表
            List<string> resolutionOptions = new List<string>();
            foreach (Resolution resolution in SupportedResolutions)
            {
                resolutionOptions.Add($"{resolution.width} x {resolution.height}");
            }

            setting.AddDropdownItem("分辨率", CurrentResolutionIndex, resolutionOptions, (index) =>
            {
                SetResolutionByIndex(index);
            });

            setting.AddToggleItem("全屏模式", IsFullScreen, (value) =>
            {
                SetFullScreen(value);
            });

            // 图形设置
            setting.AddTitle("图形设置");

            setting.AddToggleItem("垂直同步", VSync, (value) =>
            {
                VSync = value;
            });

            // 创建帧率选项列表
            List<string> frameRateOptions = new List<string>();
            foreach (int frameRate in SupportedFrameRates)
            {
                frameRateOptions.Add($"{frameRate} FPS");
            }
#else
            List<string> frameRateOptions = new List<string>();
            frameRateOptions.Add("30 FPS");
            frameRateOptions.Add("60 FPS");
#endif
            setting.AddToggleItem("后处理效果", IsPostProcessingEnabled, (value) =>
            {
                IsPostProcessingEnabled = value;
                ApplyPostProcessingSettings();
            });

            // 画质预设选项
            List<string> qualityPresetOptions = new List<string> { "低", "中", "高" };
            setting.AddDropdownItem("画质预设", QualityPreset, qualityPresetOptions, (index) =>
            {
                QualityPreset = index;
                ApplyQualityPreset();
            });

            // 阴影开关
            //setting.AddToggleItem("阴影", IsShadowEnabled, (value) =>
            //{
            //    IsShadowEnabled = value;
            //    ApplyShadowSettings();
            //});


            setting.AddDropdownItem("帧率限制", CurrentFrameRateIndex, frameRateOptions, (index) =>
            {
                SetFrameRateByIndex(index);
            });

            // 界面设置
            setting.AddBigTitle("界面设置");

            // 创建字体选项列表
            List<string> fontOptions = new List<string>();
            foreach (FontData data in FontDatas)
            {
                fontOptions.Add(data.fontName);
            }
            setting.AddDropdownItem("字体", CurrentFontIndex, fontOptions, (index) =>
            {
                CurrentFontIndex = index;
                Text[] text = GameObject.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Font = FontDatas[CurrentFontIndex].Load();
                foreach (Text text2 in text)
                {
                    if (text2.fontSize > 0)
                    {
                        text2.font = Font;
                    }
                }

                ApplyLargeFontSettings();
            });

            setting.AddToggleItem("大字体", IsLargeFontEnabled, (value) =>
            {
                IsLargeFontEnabled = value;
                ApplyLargeFontSettings();
            });

            setting.AddSliderItem("大字体缩放倍率", LargeFontScaleFactor, 0, 12, (value) =>
            {
                LargeFontScaleFactor = value;
                ApplyLargeFontSettings();
            });

            // 音频设置
            setting.AddBigTitle("音频设置");

            setting.AddSliderItem("背景音乐", BgmVolume, 0f, 1f, (value) =>
            {
                SetBgmVolume(value);
            });

            setting.AddSliderItem("音效音量", SfxVolume, 0f, 1f, (value) =>
            {
                SetSfxVolume(value);
            });

            setting.AddSliderItem("语音音量", VoiceVolume, 0f, 1f, (value) =>
            {
                SetVoiceVolume(value);
            });

            setting.AddSliderItem("主音量", MasterVolume, 0f, 1f, (value) =>
            {
                SetMasterVolume(value);
            });

            // 控制设置
            setting.AddBigTitle("控制设置");
#if UNITY_ANDROID || UNITY_IPHONE
            setting.AddToggleItem("取消按钮", MobileCancel, (value) =>
            {
                MobileCancel = value;
                ApplyControlSettings();
            });
#endif
            setting.AddSliderItem("键盘移动速度", KeyboardMoveSpeed, 200f, 600f, (value) =>
            {
                KeyboardMoveSpeed = value;
            });

            // 移动方式选择
            setting.AddTitle("移动方式");
            setting.AddToggleGroupItem("移动方式", MovementMode, new List<string> { "手动", "自动" }, (value) =>
            {
                MovementMode = value;
            });
        }

        /// <summary>
        /// 处理剧本初始化事件
        /// </summary>
        /// <param name="scenario">剧本</param>
        private void OnScenarioInit(Scenario scenario)
        {
            // 设置键盘移动速度
            if (Sango.Render.MapRender.Instance != null)
            {
                Sango.Render.MapRender.Instance.SetKeyBoardMoveSpeed(KeyboardMoveSpeed);
            }
        }
#endregion

#region 应用设置
        /// <summary>
        /// 应用所有设置
        /// </summary>
        public void Apply()
        {
            ApplyAllSettings();
            GameEvent.OnGameSettingApply?.Invoke();

            // 更新MapRender的设置
            if (Sango.Render.MapRender.Instance != null)
            {
                Sango.Render.MapRender.Instance.SetKeyBoardMoveSpeed(KeyboardMoveSpeed);
            }
        }
#endregion

#region 音频操作
        /// <summary>
        /// 设置背景音乐音量
        /// </summary>
        /// <param name="volume">音量（0-1）</param>
        public void SetBgmVolume(float volume)
        {
            BgmVolume = Mathf.Clamp01(volume);
            ApplyAudioSettings();
        }

        /// <summary>
        /// 设置音效音量
        /// </summary>
        /// <param name="volume">音量（0-1）</param>
        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            ApplyAudioSettings();
        }

        /// <summary>
        /// 设置音效音量
        /// </summary>
        /// <param name="volume">音量（0-1）</param>
        public void SetVoiceVolume(float volume)
        {
            VoiceVolume = Mathf.Clamp01(volume);
            ApplyAudioSettings();
        }

        /// <summary>
        /// 设置主音量
        /// </summary>
        /// <param name="volume">音量（0-1）</param>
        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            ApplyAudioSettings();
        }
#endregion

#region 默认值和快照
        /// <summary>
        /// 还原默认设置
        /// </summary>
        public void RestoreDefaults()
        {
            // 分辨率设置
            ScreenWidth = 1920;
            ScreenHeight = 1080;
            IsFullScreen = true;
            CurrentResolutionIndex = 0;

            // 音频设置
            BgmVolume = 0.6f;
            SfxVolume = 1.0f;
            VoiceVolume = 1.0f;
            MasterVolume = 1.0f;

            // 图形设置
            VSync = true;
            FrameRateLimit = 60;
            CurrentFrameRateIndex = 1;
            QualityLevel = 2;
            IsPostProcessingEnabled = true;

            // 控制设置
            KeyboardMoveSpeed = 300f;
            MobileCancel = true;
            MovementMode = 0;

            // 语言设置
            Language = "zh-CN";

            // 界面设置
            IsLargeFontEnabled = false;
            LargeFontScaleFactor = 0;
            FontName = "kaiti";

            // 图形设置 - 阴影与画质
            IsShadowEnabled = true;
            QualityPreset = 2;

            // 清除PlayerPrefs
            //PlayerPrefs.DeleteAll();
            //PlayerPrefs.Save();

            // 应用设置
            ApplyAllSettings();
        }


        /// <summary>
        /// 创建设置快照
        /// </summary>
        /// <returns>设置快照</returns>
        public GameSettingSnapshot CreateSnapshot()
        {
            return new GameSettingSnapshot
            {
                ScreenWidth = ScreenWidth,
                ScreenHeight = ScreenHeight,
                IsFullScreen = IsFullScreen,
                CurrentResolutionIndex = CurrentResolutionIndex,
                BgmVolume = BgmVolume,
                SfxVolume = SfxVolume,
                MobileCancel = MobileCancel,
                MasterVolume = MasterVolume,
                VSync = VSync,
                FrameRateLimit = FrameRateLimit,
                CurrentFrameRateIndex = CurrentFrameRateIndex,
                QualityLevel = QualityLevel,
                IsPostProcessingEnabled = IsPostProcessingEnabled,
                KeyboardMoveSpeed = KeyboardMoveSpeed,
                MovementMode = MovementMode,
                Language = Language,
                IsLargeFontEnabled = IsLargeFontEnabled,
                LargeFontScaleFactor = LargeFontScaleFactor,
                FontName = FontName,
                IsShadowEnabled = IsShadowEnabled,
                QualityPreset = QualityPreset,
            };
        }

        /// <summary>
        /// 从快照还原设置
        /// </summary>
        /// <param name="snapshot">设置快照</param>
        public void RestoreFromSnapshot(GameSettingSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            // 还原设置
            ScreenWidth = snapshot.ScreenWidth;
            ScreenHeight = snapshot.ScreenHeight;
            IsFullScreen = snapshot.IsFullScreen;
            CurrentResolutionIndex = snapshot.CurrentResolutionIndex;
            BgmVolume = snapshot.BgmVolume;
            SfxVolume = snapshot.SfxVolume;
            VoiceVolume = snapshot.VoiceVolume;

            MasterVolume = snapshot.MasterVolume;
            VSync = snapshot.VSync;
            FrameRateLimit = snapshot.FrameRateLimit;
            CurrentFrameRateIndex = snapshot.CurrentFrameRateIndex;
            QualityLevel = snapshot.QualityLevel;
            IsPostProcessingEnabled = snapshot.IsPostProcessingEnabled;
            KeyboardMoveSpeed = snapshot.KeyboardMoveSpeed;
            MovementMode = snapshot.MovementMode;
            Language = snapshot.Language;
            IsLargeFontEnabled = snapshot.IsLargeFontEnabled;
            LargeFontScaleFactor = snapshot.LargeFontScaleFactor;
            FontName = snapshot.FontName;
            IsShadowEnabled = snapshot.IsShadowEnabled;
            QualityPreset = snapshot.QualityPreset;
            // 应用设置
            ApplyAllSettings();
        }
#endregion

        public void UpdateTargetGameObject(GameObject target)
        {
            if (Font != null)
            {
                Text[] text = target.GetComponentsInChildren<Text>(true);
                if (text != null)
                {
                    foreach (Text t in text)
                    {
                        if (t.fontSize == 0) continue;
                        t.font = Font;
                    }
                }
            }

            if (IsLargeFontEnabled)
            {
                float addSize = LargeFontScaleFactor;
                Text[] text = target.GetComponentsInChildren<Text>(true);
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
            }
        }
    }

    /// <summary>
    /// 设置快照类，用于保存和还原设置状态
    /// </summary>
    public class GameSettingSnapshot
    {
        /// <summary>
        /// 屏幕宽度
        /// </summary>
        public int ScreenWidth { get; set; }

        /// <summary>
        /// 屏幕高度
        /// </summary>
        public int ScreenHeight { get; set; }

        /// <summary>
        /// 是否全屏
        /// </summary>
        public bool IsFullScreen { get; set; }

        /// <summary>
        /// 当前分辨率索引
        /// </summary>
        public int CurrentResolutionIndex { get; set; }

        /// <summary>
        /// 背景音乐音量
        /// </summary>
        public float BgmVolume { get; set; }

        /// <summary>
        /// 音效音量
        /// </summary>
        public float SfxVolume { get; set; }

        /// <summary>
        /// 音效音量
        /// </summary>
        public float VoiceVolume { get; set; }

        /// <summary>
        /// 主音量
        /// </summary>
        public float MasterVolume { get; set; }

        /// <summary>
        /// 垂直同步
        /// </summary>
        public bool VSync { get; set; }

        /// <summary>
        /// 帧率限制
        /// </summary>
        public int FrameRateLimit { get; set; }

        /// <summary>
        /// 当前帧率索引
        /// </summary>
        public int CurrentFrameRateIndex { get; set; }

        /// <summary>
        /// 图形质量等级
        /// </summary>
        public int QualityLevel { get; set; }

        /// <summary>
        /// 后处理开关
        /// </summary>
        public bool IsPostProcessingEnabled { get; set; }

        /// <summary>
        /// 键盘移动速度
        /// </summary>
        public float KeyboardMoveSpeed { get; set; }
        public bool MobileCancel { get; set; }
        
        /// <summary>
        /// 移动方式（0=自动，1=手动）
        /// </summary>
        public int MovementMode { get; set; }

        /// <summary>
        /// 当前语言
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// 大字体开关
        /// </summary>
        public bool IsLargeFontEnabled { get; set; }

        /// <summary>
        /// 大字体缩放倍率
        /// </summary>
        public int LargeFontScaleFactor { get; set; }

        public string FontName { get; set; }

        /// <summary>
        /// 阴影开关
        /// </summary>
        public bool IsShadowEnabled { get; set; }

        /// <summary>
        /// 画质预设（0=低，1=中，2=高）
        /// </summary>
        public int QualityPreset { get; set; }
    }




}
