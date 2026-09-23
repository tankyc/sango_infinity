using TKNewtonsoft.Json;
using Sango.Mod;
using System.Collections.Generic;
using System.IO;

namespace Sango.Core
{
    /// <summary>
    /// 多语言文本管理
    /// </summary>
    public class GameLanguage : Singleton<GameLanguage>
    {
        public string curLanguageName;
        string LanguageFileDir = "Language";
        public Dictionary<int, string> curLanguage = new Dictionary<int, string>();
        public List<string> languageFiles = new List<string>();
        internal void Init(string lanName)
        {
            curLanguageName = lanName;
            Directory.EnumFiles($"{Path.ContentRootPath}/{LanguageFileDir}", "*.json", SearchOption.AllDirectories, (file) =>
            {
                LoadFile(file);
            });

            for(int i = 0; i < languageFiles.Count; i++)
            {
                LoadFile(languageFiles[i]);
            }
        }

        /// <summary>
        /// 加载一个多语言文件,会覆盖相同ID
        /// </summary>
        /// <param name="filewhere"></param>
        private void LoadFile(string filewhere)
        {
            Sango.Log.Info($"Load  Language File: {filewhere}");
            using (StreamReader file = System.IO.File.OpenText(filewhere))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                while (reader.Read()) // Advances to the next token in the JSON stream.
                {
                    if (reader.TokenType == JsonToken.StartObject) // Check for start of an object in the JSON stream.
                    {
                        int id;
                        if (!string.IsNullOrEmpty(reader.Path))
                        {
                            if (int.TryParse(reader.Path, out id))
                            {
                                while (reader.Read())
                                {
                                    if (reader.TokenType == JsonToken.PropertyName && reader.Value.ToString() == curLanguageName)
                                    {
                                        curLanguage[id] = reader.ReadAsString().Replace("\\n", "\n");
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 添加一个多语言文件,在切换语言的时候会读取文件内对应语言的文本
        /// </summary>
        /// <param name="lanFile"></param>
        private void _AddFile(string lanFile)
        {
            if (!languageFiles.Contains(lanFile))
                languageFiles.Add(lanFile);
        }

        private void _ChangeLanguage(string language)
        {
            Init(language);
        }

        private string _GetString(int id)
        {
            if (curLanguage.TryGetValue(id, out var str))
                return str;
            return $"text not find: {id}";
        }

        /// <summary>
        /// 获取一个多语言文本
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string GetString(int id)
        {
            return Instance._GetString(id);
        }

        /// <summary>
        /// 切换多语言
        /// </summary>
        /// <param name="language"></param>
        public static void ChangeLanguage(string language)
        {
            Instance._ChangeLanguage(language);
        }

        /// <summary>
        /// 添加一个多语言文件
        /// </summary>
        /// <param name="lanFile"></param>
        public static void AddFile(string lanFile)
        {
            Instance._AddFile(lanFile);
        }
    }
}
