using TKNewtonsoft.Json;
using Sango.Mod;
using System.Collections.Generic;

namespace Sango.Core
{
    public class GameCustomEdit : Singleton<GameCustomEdit>
    {
        /// <summary>
        /// 容貌区间性别分类
        /// </summary>
        public enum HeadSexType
        {
            /// <summary>男性容貌</summary>
            Male = 0,
            /// <summary>女性容貌</summary>
            Female = 1,
            /// <summary>自定义容貌（新武将）</summary>
            Custom = 2
        }

        /// <summary>
        /// 容貌ID区间配置。
        /// 每个区间定义一段连续的容貌ID范围及其性别属性，
        /// 支持配置多段区间以覆盖不连续的自定义头像ID段。
        /// </summary>
        public class HeadIdRange
        {
            /// <summary>区间名称，如"标准男性"、"自定义女性"</summary>
            public string name;

            /// <summary>起始ID（包含）</summary>
            public int startId;

            /// <summary>结束ID（包含）</summary>
            public int endId;

            /// <summary>性别分类</summary>
            public HeadSexType sexType;
        }

        public class HeadEditData
        {
            [JsonProperty]
            public List<HeadIdRange> HeadIdRanges = new List<HeadIdRange>();

        }

        HeadEditData headEditData = new HeadEditData();

        /// <summary>
        /// 所有容貌数据链表。
        /// 按区间配置顺序生成，先男后女排列。
        /// </summary>
        public List<int> headDataList = new List<int>();

        public int femaleStartIndex = 0;
        public int maleStartIndex = 0;

        public void LoadFaceData()
        {
            headEditData.HeadIdRanges.Clear();
            string file = Path.ContentRootPath + "/Data/FaceConfig.json";
            if (File.Exists(file))
            {
                HeadEditData data = new HeadEditData();
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), data);
                headEditData.HeadIdRanges.AddRange(data.HeadIdRanges);
            }
            // mod在前面
            ModManager.Instance.EnumFiles("Data/FaceConfig.json", file =>
            {
                HeadEditData data = new HeadEditData();
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), data);
                headEditData.HeadIdRanges.AddRange(data.HeadIdRanges);
            });

            headDataList.Clear();
            femaleStartIndex = -1;
            foreach (var range in headEditData.HeadIdRanges)
            {
                if (range == null) continue;
                if (range.sexType == HeadSexType.Male)
                {
                    for (int id = range.startId; id <= range.endId; id++)
                    {
                        if (!headDataList.Contains(id))
                            headDataList.Add(id);
                    }
                }
            }
            femaleStartIndex = headDataList.Count;
            foreach (var range in headEditData.HeadIdRanges)
            {
                if (range == null) continue;
                if (range.sexType == HeadSexType.Female)
                {
                    for (int id = range.startId; id <= range.endId; id++)
                    {
                        if (!headDataList.Contains(id))
                            headDataList.Add(id);
                    }
                }
            }
        }

        public ScenarioAddon CoreScenarioAddon { get; private set; }
        public ScenarioAddon ModScenarioAddon { get; private set; }
        public ScenarioAddon SelfScenarioAddon { get; private set; }

        public PersonLib TargetEditPerson;
        public List<PersonLib> ModedPersonLibList = new List<PersonLib>();
        public List<PersonLib> CorePersonLibList = new List<PersonLib>();


        public void Init()
        {
            needUpdate_all = true;

            CoreScenarioAddon = LoadScenarioAddon("Data/PersonLibrary.json");

            ModScenarioAddon = new ScenarioAddon();
            ModManager.Instance.EnumFiles("Data/CustomEdit/CustomPerson.json", file =>
            {
                ModScenarioAddon.Load(file);
            });

            SelfScenarioAddon = new ScenarioAddon();
            SelfScenarioAddon.Load(Path.SaveRootPath + "/CustomEdit/CustomPerson.json");

            ModScenarioAddon.PersonLibrary.ForEach(x => ModedPersonLibList.Add(x));
            CoreScenarioAddon.PersonLibrary.ForEach(x => CorePersonLibList.Add(x));

            LoadFaceData();
        }



        List<PersonLib> all_personLibs = new List<PersonLib>();
        bool needUpdate_all = true;
        /// <summary>
        /// 注意这个是每次都会新建一个List
        /// </summary>
        public List<PersonLib> AllPersonLibs
        {
            get
            {
                if (needUpdate_all)
                {
                    needUpdate_all = false;
                    all_personLibs.Clear();
                    all_personLibs.AddRange(CorePersonLibList);
                    all_personLibs.AddRange(ModedPersonLibList);
                    SelfScenarioAddon.PersonLibrary.ForEach(x => all_personLibs.Add(x));
                }
                return all_personLibs;
            }
        }

        public void NeedUpdatePersonLib()
        {
            needUpdate_all = true;
        }

        public ScenarioAddon LoadScenarioAddon(string path)
        {
            ScenarioAddon scenarioAddon = new ScenarioAddon();
            string dst = $"{Path.ContentRootPath}/{path}";
            if (File.Exists(dst))
            {
                scenarioAddon.Load(dst);
            }
            ModManager.Instance.EnumFiles(dst, file =>
            {
                scenarioAddon.Load(file);
            });
            return scenarioAddon;
        }

        /// <summary>
        /// 将当前自建武将数据序列化保存到本地文件。
        /// </summary>
        public void SaveScenarioAddon()
        {
            if (SelfScenarioAddon == null) return;
            string path = Sango.Path.SaveRootPath + "/CustomEdit/CustomPerson.json";
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            string json = JsonConvert.SerializeObject(SelfScenarioAddon, Formatting.Indented);
            File.WriteAllText(path, json);
            needUpdate_all = true;
        }
    }
}
