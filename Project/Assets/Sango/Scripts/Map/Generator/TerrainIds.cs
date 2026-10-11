namespace Sango.MapGen
{
    /// <summary>
    /// 地形类型ID常量。
    /// 取值与 Content/Data/Common/TerrainTypes.json 一一对应，供生成器与 API 层共用，避免散落的魔法数字。
    /// </summary>
    public static class TerrainIds
    {
        /// <summary>无(不可通行)</summary>
        public const int None = 0;
        /// <summary>草地</summary>
        public const int Grass = 1;
        /// <summary>土</summary>
        public const int Soil = 2;
        /// <summary>砂地</summary>
        public const int Sand = 3;
        /// <summary>湿地</summary>
        public const int Marsh = 4;
        /// <summary>毒泉</summary>
        public const int Poison = 5;
        /// <summary>森</summary>
        public const int Forest = 6;
        /// <summary>川(不可通行的河流)</summary>
        public const int Stream = 7;
        /// <summary>河(可通行的河流)</summary>
        public const int River = 8;
        /// <summary>海</summary>
        public const int Sea = 9;
        /// <summary>荒地</summary>
        public const int Waste = 10;
        /// <summary>主径</summary>
        public const int RoadMain = 11;
        /// <summary>栈道</summary>
        public const int Plank = 12;
        /// <summary>渡所</summary>
        public const int Ford = 13;
        /// <summary>浅滩</summary>
        public const int Shallow = 14;
        /// <summary>岸</summary>
        public const int Shore = 15;
        /// <summary>崖(山地，不可通行)</summary>
        public const int Cliff = 16;
        /// <summary>都市</summary>
        public const int City = 17;
        /// <summary>港</summary>
        public const int Port = 18;
        /// <summary>关所</summary>
        public const int Gate = 19;
        /// <summary>小径</summary>
        public const int PathSmall = 20;
        /// <summary>远海(离陆地较远的开放海域，不可通行)</summary>
        public const int FarSea = 21;
        /// <summary>深海(离陆地很远的洋心，不可通行)</summary>
        public const int DeepSea = 22;

        /// <summary>地形类型总数</summary>
        public const int Count = 32;

        /// <summary>顶点材质调色板大小(与 MapData.color_map 一致)</summary>
        public const int MaterialLayerCount = 36;
    }
}
