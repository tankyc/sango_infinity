using Sango.Loader;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Core

{
    public class GameRenderHelper
    {
        public static string BuildingTypeIconPath = "Assets/UI/AtlasTexture";
        public static string TroopStatePath = "Assets/UI/AtlasTexture";

        public static string HeadIconPath = "Assets/Face";
        public static string CriticalImagePath = "Assets/CriticalImage";

        /// <summary>
        /// 剧本事件的图文演出（CG）资源目录。
        /// 放在 Assets 下并单独一个目录，是为了让 Mod 能用同样的相对路径覆盖（与 Face / CriticalImage 一致）。
        /// </summary>
        public static string EventPicturePath = "Assets/UI/EventPicture";
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

        public static UnityEngine.Sprite LoadBuildingTypeIcon(string name)
        {
            string headPath = $"{BuildingTypeIconPath}/{name}.png";
            UnityEngine.Sprite headSpr = ObjectLoader.LoadObject<UnityEngine.Sprite>(headPath);
            if (headSpr == null)
            {
                headPath = $"{BuildingTypeIconPath}/4845_5_22.png";
                headSpr = ObjectLoader.LoadObject<UnityEngine.Sprite>(headPath);
            }
            return headSpr;
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
