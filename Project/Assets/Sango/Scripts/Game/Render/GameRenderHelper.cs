using Sango.Loader;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Core

{
    public class GameRenderHelper
    {
        /// <summary>
        /// 建筑 / 兵种图标的老图集目录。
        /// 建筑图标已迁移到 <see cref="BuildingIconPath"/>，本目录现在只作兜底
        /// （兵种的 icon 仍是 "4846-8/4846-8_0" 这种老口径，尚未迁移）。
        /// </summary>
        public static string BuildingTypeIconPath = "Assets/UI/AtlasTexture";
        public static string TroopStatePath = "Assets/UI/AtlasTexture";

        public static string HeadIconPath = "Assets/Face";
        public static string CriticalImagePath = "Assets/CriticalImage";

        /// <summary>
        /// 剧本事件的图文演出（CG）资源目录。
        /// 放在 Assets 下并单独一个目录，是为了让 Mod 能用同样的相对路径覆盖（与 Face / CriticalImage 一致）。
        /// </summary>
        public static string EventPicturePath = "Assets/EventPicture";
        public static string TroopHeadbarRes = "Assets/UI/Prefab/window_troop_bar.prefab";
        public static string CityHeadbarRes = "Assets/UI/Prefab/window_city_bar.prefab";
        public static string BuildingHeadbarRes = "Assets/UI/Prefab/window_building_bar.prefab";
        public static string AnimationTextInfoRes = "Assets/UI/Prefab/window_aniTextInfo.prefab";
        public static string FireRes = "Assets/Effect/Prefab/ef_scene_fire.prefab";
        public static string[] CityResPath = new string[]{
        "Assets/Model/Prefab/p_city_1.prefab",
        "Assets/Model/Prefab/p_city_2.prefab",
        };

        public static Texture LoadHeadIcon(int id)
        {
            return LoadHeadIcon(id, 2);
        }
        public static Texture LoadHeadIcon(int id, int type)
        {
            string headPath = $"{HeadIconPath}/{id}_{type}";
            Texture headSpr = ObjectLoader.LoadObject<Texture>(headPath, "Face");
            if (headSpr == null)
            {
                headPath = "Assets/empty.png";
                headSpr = ObjectLoader.LoadObject<Texture>(headPath);
            }
            return headSpr;
        }

        public static string GetCityModelAsset(int type)
        {
            if (type < 0 || type >= CityResPath.Length)
                type = 0;
            return CityResPath[type];
        }

        /// <summary>
        /// 道具图标目录（宝物 / 装备）。
        /// 与头像（<see cref="HeadIconPath"/> = <c>Assets/Face</c>）同构：Assets 下的**顶层**目录，
        /// 不带 UI 层级，Mod 可以用同样的相对路径整体覆盖。
        /// 图标文件按道具 Id 命名，例如 Id = 107 的吴钩剑 → <c>Assets/ItemIcon/107.png</c>；
        /// 数据里写子目录形式（如 <c>4846-8/4846-8_5</c>）同样支持。
        ///
        /// 资源落地位置（两份，编辑器与发布各取所需）：
        ///   · <c>Project/Assets/Mods/Content/Assets/ItemIcon/</c> —— 进 Content 包，编辑器 AssetDatabase 命中
        ///   · <c>&lt;ContentRootPath&gt;/Content/Assets/ItemIcon/</c> —— 裸文件，开发期热改不用重新打包
        /// </summary>
        public static string ItemIconPath = "Assets/ItemIcon";

        /// <summary>取不到图标时的占位图（与头像共用同一张，避免各系统各搓一个占位）</summary>
        public static string EmptyIconPath = "Assets/empty.png";

        /// <summary>
        /// 按"模组私有资源引用"加载图标。
        ///
        /// 数据里的 icon 字段可以写 <c>mod://模组Id/相对路径</c>（推荐）或 <c>模组Id:相对路径</c>，
        /// 表示固定读取该模组 <c>Private</c> 目录下的文件，不参与全局搜索，也不会被其他模组覆盖。
        /// 不是私有引用（或该模组未登记）时返回 null，由调用方继续走原有的默认目录逻辑。
        /// </summary>
        /// <param name="resName">数据里的资源字段值</param>
        /// <returns>命中的贴图；非私有引用或加载失败返回 null</returns>
        public static Texture LoadModPrivateIcon(string resName)
        {
            if (!ModAssetPath.IsModUri(resName))
                return null;
            return ObjectLoader.LoadObject<Texture>(resName);
        }

        /// <summary>
        /// 按道具的 icon 字段加载图标纹理（配 <c>RawImage</c> 使用，与 <see cref="LoadHeadIcon"/> 同一套口径）。
        ///
        /// 容错口径与头像 / 事件 CG 相同：<c>icon</c> 可以只写 Id（"107"），也可以写子目录形式
        /// （"4846-8/4846-8_5"）或完整相对路径，**不必写扩展名**（文件名查找会自动补 .png）。
        /// 加载顺序：
        ///   ① Content 包（编辑器 = <c>Assets/Mods/Content/</c>，运行时 = Content.pkg）
        ///   ② ContentRootPath 下的裸文件（开发期热改走这条）
        ///   ③ 都取不到 → 占位图；**不返回 null**，UI 侧无需做空判断
        /// </summary>
        /// <param name="icon">道具的 icon 字段值</param>
        /// <returns>图标纹理；取不到返回占位图</returns>
        public static Texture LoadItemIcon(string icon)
        {
            Texture texture = null;

            // 模组私有资源引用(mod://模组Id/路径 或 模组Id:路径):
            // 固定只读该模组的 Private 目录,不再拼默认目录、也不受其他模组影响
            if (ModAssetPath.IsModUri(icon))
            {
                texture = LoadModPrivateIcon(icon);
            }
            else if (!string.IsNullOrEmpty(icon))
            {
                string itemPath = icon.StartsWith("Assets/") ? icon : $"{ItemIconPath}/{icon}";
                texture = ObjectLoader.LoadObject<Texture>(itemPath, "Content");
            }

            if (texture == null)
                texture = ObjectLoader.LoadObject<Texture>(EmptyIconPath);

            return texture;
        }

        /// <summary>
        /// 建筑图标目录。
        /// 与道具图标（<see cref="ItemIconPath"/>）同构：Assets 下的**顶层**目录，走 Content 包，
        /// Mod 可以用同样的相对路径整体覆盖。
        /// 图标文件按建筑类型 Id 命名，例如 Id = 4 的「阵」→ <c>Assets/BuildingIcon/4.png</c>。
        ///
        /// 资源落地位置（两份，编辑器与发布各取所需）：
        ///   · <c>Project/Assets/Mods/Content/Assets/BuildingIcon/</c> —— 进 Content 包，编辑器 AssetDatabase 命中
        ///   · <c>&lt;ContentRootPath&gt;/Content/Assets/BuildingIcon/</c> —— 裸文件，开发期热改不用重新打包
        /// </summary>
        public static string BuildingIconPath = "Assets/BuildingIcon";

        /// <summary>
        /// 按 icon 字段加载建筑 / 兵种图标纹理（配 <c>RawImage</c> 使用，与 <see cref="LoadItemIcon"/> 同一套口径）。
        ///
        /// 两级查找，兼容两批数据：
        ///   ① <see cref="BuildingIconPath"/> —— 建筑图标已迁移，icon 字段 = 建筑 Id
        ///   ② <see cref="BuildingTypeIconPath"/> 老图集 —— 兵种等仍是 "4846-8/4846-8_0" 老口径
        /// 都取不到 → 占位图；**不返回 null**，UI 侧无需做空判断。
        /// </summary>
        /// <param name="icon">icon 字段值（建筑 Id，或老口径的图集相对路径）</param>
        /// <returns>图标纹理；取不到返回占位图</returns>
        public static Texture LoadBuildingIcon(string icon)
        {
            Texture texture = null;

            // 模组私有资源引用:固定只读该模组的 Private 目录,不再拼默认目录、也不受其他模组影响
            if (ModAssetPath.IsModUri(icon))
            {
                texture = LoadModPrivateIcon(icon);
            }
            else if (!string.IsNullOrEmpty(icon))
            {
                if (icon.StartsWith("Assets/"))
                {
                    texture = ObjectLoader.LoadObject<Texture>(icon, "Content");
                }
                else
                {
                    // ① 新目录：icon 即建筑 Id
                    texture = ObjectLoader.LoadObject<Texture>($"{BuildingIconPath}/{icon}", "Content");

                    // ② 老图集兜底：icon 形如 "4846-8/4846-8_0"（兵种 / 未迁移数据）
                    if (texture == null && icon.IndexOf('/') >= 0)
                        texture = ObjectLoader.LoadObject<Texture>($"{BuildingTypeIconPath}/{icon}", "Content");
                }
            }

            if (texture == null)
                texture = ObjectLoader.LoadObject<Texture>(EmptyIconPath);

            return texture;
        }

        public static UnityEngine.Sprite LoadTroopStateIcon(string name)
        {
            return ObjectLoader.LoadObject<UnityEngine.Sprite>($"{TroopStatePath}/{name}.png");
        }

        /// <summary>
        /// 加载暴击图
        /// </summary>
        /// <param name="id">暴击图ID</param>
        /// <returns>暴击图纹理</returns>
        public static Texture LoadCriticalImage(string id)
        {
            // 模组私有资源引用:固定只读该模组的 Private 目录,不再拼暴击图目录
            if (ModAssetPath.IsModUri(id))
                return LoadModPrivateIcon(id);

            string criticalPath = $"{CriticalImagePath}/{id}";
            string extension = System.IO.Path.GetExtension(id);
            if (!string.IsNullOrEmpty(extension))
                criticalPath = criticalPath.Substring(0, criticalPath.LastIndexOf('.'));

            Texture criticalTexture = ObjectLoader.LoadObject<Texture>(criticalPath, "CriticalImage", false, false);
            if (criticalTexture == null)
            {
                criticalPath = criticalPath.Replace(".png", ".jpg");
                criticalTexture = ObjectLoader.LoadObject<Texture>(criticalPath, "CriticalImage");
                if (criticalTexture == null)
                {
                    criticalPath = $"{CriticalImagePath}/default.png";
                    criticalTexture = ObjectLoader.LoadObject<Texture>(criticalPath);
                }
            }
            return criticalTexture;
        }

        /// <summary>
        /// 加载剧本事件图文演出用的 CG。
        ///
        /// 容错的点在于"策划不必写扩展名"：<c>name</c> 可以写 "taoyuan"，
        /// 也可以写完整相对路径或 "taoyuan.png"。找不到时返回 null，
        /// 由窗口侧决定是显示占位还是干脆跳过 —— **绝不抛异常、也绝不让事件卡住**。
        /// </summary>
        /// <param name="name">资源名或相对路径（不含扩展名也可）</param>
        /// <returns>贴图；找不到返回 null</returns>
        public static Texture LoadEventPicture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // 模组私有资源引用:固定读取该模组 Private 目录,不再拼事件图目录,也不回落到公共图
            if (ModAssetPath.IsModUri(name))
                return LoadModPrivateIcon(name);

            string path = name.StartsWith("Assets/") ? name : $"{EventPicturePath}/{name}";

            Texture tex = ObjectLoader.LoadObject<Texture>(path, "EventPicture", false, false);
            if (tex != null) return tex;

            // 没写扩展名时补两种最常见的再试
            if (System.IO.Path.GetExtension(path).Length == 0)
            {
                tex = ObjectLoader.LoadObject<Texture>(path + ".png", "EventPicture", false, false);
                if (tex == null)
                    tex = ObjectLoader.LoadObject<Texture>(path + ".jpg", "EventPicture", false, false);
            }

            return tex;
        }
    }
}
