using System.Collections.Generic;
using System.IO;
using TKNewtonsoft.Json;
using Sango.Mod;

namespace Sango.Core
{
    /// <summary>
    /// 技能配置管理器
    /// </summary>
    public class SkillConfigManager : Singleton<SkillConfigManager>
    {
        /// <summary>
        /// 技能配置映射
        /// </summary>
        private SangoObjectMap<Skill> _skillConfigs = new SangoObjectMap<Skill>();
        
        /// <summary>
        /// Buff配置映射
        /// </summary>
        private SangoObjectMap<Buff> _buffConfigs = new SangoObjectMap<Buff>();

        /// <summary>
        /// 初始化技能配置管理器
        /// </summary>
        public void Init()
        {
            Sango.Log.Info("SkillConfigManager.Init()");
            LoadSkillConfigs();
            LoadBuffConfigs();
        }

        /// <summary>
        /// 加载技能配置
        /// </summary>
        /// <returns>技能配置映射</returns>
        public SangoObjectMap<Skill> LoadSkillConfigs()
        {
            if (_skillConfigs != null && _skillConfigs.Count > 0)
                return _skillConfigs;

            _skillConfigs = new SangoObjectMap<Skill>();
            string skillConfigFileName = "Data/Skill/Skills.json";
            ModManager.Instance.LoadFile(skillConfigFileName, file =>
            {
                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new SangoObjectMapConverter<Skill>());
                SangoObjectMap<Skill> skills = JsonConvert.DeserializeObject<SangoObjectMap<Skill>>(File.ReadAllText(file), jsonSerializerSettings);
                if (skills != null)
                {
                    skills.ForEach(skill =>
                    {
                        _skillConfigs.Add(skill);
                    });
                }
            });

            return _skillConfigs;
        }

        /// <summary>
        /// 加载Buff配置
        /// </summary>
        /// <returns>Buff配置映射</returns>
        public SangoObjectMap<Buff> LoadBuffConfigs()
        {
            if (_buffConfigs != null && _buffConfigs.Count > 0)
                return _buffConfigs;

            _buffConfigs = new SangoObjectMap<Buff>();
            string buffConfigFileName = "Data/Skill/Buffs.json";
            ModManager.Instance.LoadFile(buffConfigFileName, file =>
            {
                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new SangoObjectMapConverter<Buff>());
                SangoObjectMap<Buff> buffs = JsonConvert.DeserializeObject<SangoObjectMap<Buff>>(File.ReadAllText(file), jsonSerializerSettings);
                if (buffs != null)
                {
                    buffs.ForEach(buff =>
                    {
                        _buffConfigs.Add(buff);
                    });
                }
            });

            return _buffConfigs;
        }

        /// <summary>
        /// 获取技能
        /// </summary>
        /// <param name="id">技能ID</param>
        /// <returns>技能对象</returns>
        public Skill GetSkill(int id)
        {
            return _skillConfigs.Get(id);
        }

        /// <summary>
        /// 获取Buff
        /// </summary>
        /// <param name="id">Buff ID</param>
        /// <returns>Buff对象</returns>
        public Buff GetBuff(int id)
        {
            return _buffConfigs.Get(id);
        }

        /// <summary>
        /// 添加技能
        /// </summary>
        /// <param name="skill">技能对象</param>
        public void AddSkill(Skill skill)
        {
            if (skill != null)
            {
                _skillConfigs.Add(skill);
            }
        }

        /// <summary>
        /// 添加Buff
        /// </summary>
        /// <param name="buff">Buff对象</param>
        public void AddBuff(Buff buff)
        {
            if (buff != null)
            {
                _buffConfigs.Add(buff);
            }
        }

        /// <summary>
        /// 加载模组技能配置
        /// </summary>
        /// <param name="mod">模组对象</param>
        public void LoadModSkillConfigs(Mod.Mod mod)
        {
            string skillConfigPath = mod.GetFullPath("Data/Skill/Skills.json");
            if (File.Exists(skillConfigPath))
            {
                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new SangoObjectMapConverter<Skill>());
                SangoObjectMap<Skill> modSkills = JsonConvert.DeserializeObject<SangoObjectMap<Skill>>(File.ReadAllText(skillConfigPath), jsonSerializerSettings);
                if (modSkills != null)
                {
                    modSkills.ForEach(skill =>
                    {
                        _skillConfigs.Add(skill);
                    });
                }
            }

            string buffConfigPath = mod.GetFullPath("Data/Skill/Buffs.json");
            if (File.Exists(buffConfigPath))
            {
                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new SangoObjectMapConverter<Buff>());
                SangoObjectMap<Buff> modBuffs = JsonConvert.DeserializeObject<SangoObjectMap<Buff>>(File.ReadAllText(buffConfigPath), jsonSerializerSettings);
                if (modBuffs != null)
                {
                    modBuffs.ForEach(buff =>
                    {
                        _buffConfigs.Add(buff);
                    });
                }
            }
        }
    }
}
