using Sango.Manager;
using Sango.Mod;
using System.Collections.Generic;
using TKNewtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GameMedia : Singleton<GameMedia>
    {

        public string buttonSfx = "Assets/Sound/button.mp3";
        public string menuClickSfx = "Assets/Sound/btn2.mp3";
        public string subMenuClickSfx = "Assets/Sound/btn3.mp3";
        public string cancelSfx = "Assets/Sound/cancel.mp3";
        public string doactionSfx = "Assets/Sound/doaction.mp3";
        public string newTrunSfx = "Assets/Sound/new_turn.mp3";

        [JsonObject(MemberSerialization.OptOut)]
        public class MediaConfig
        {
            public int Id;
            public string res;
        }

        [JsonProperty]
        public Dictionary<int, MediaConfig> MediaData = new Dictionary<int, MediaConfig>();

        public void Load()
        {
            Load(Path.ContentRootPath + "/Data/MediaData.json");
            ModManager.Instance.EnumFiles("Data/MediaData.json", file =>
            {
                Load(file);
            });
        }

        /// <summary>
        /// 加载json数据
        /// </summary>
        /// <param name="file"></param>
        public void Load(string file)
        {
            if (File.Exists(file))
            {
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), this);
            }
        }

        public void Init()
        {
            AudioManager.Instance.Init();
        }

        public void Update()
        {
            AudioManager.Instance.Update();
        }

        public int PlayVoice(int id)
        {
            if (AudioManager.Instance.BgmVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
#if SANGO_DEBUG
                Sango.Log.Info($"播放语音: {result.res}");
#endif
                return AudioManager.Instance.PlayVoice(result.res);
            }
            return -1;
        }

        public int PlayVoice(int id, float volume)
        {
            if (AudioManager.Instance.BgmVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
#if SANGO_DEBUG
                Sango.Log.Info($"播放语音: {result.res}");
#endif
                return AudioManager.Instance.PlayVoice(result.res, volume);
            }
            return -1;
        }

        public int PlaySfx(int id)
        {
            if (AudioManager.Instance.BgmVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
#if SANGO_DEBUG
                Sango.Log.Info($"播放音效: {result.res}");
#endif
                return AudioManager.Instance.PlaySfx(result.res);
            }
            return -1;
        }

        public int PlaySfx(int id, float volume)
        {
            if (AudioManager.Instance.BgmVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
#if SANGO_DEBUG
                Sango.Log.Info($"播放音效: {result.res}");
#endif
                return AudioManager.Instance.PlaySfx(result.res, volume);
            }
            return -1;
        }

        public int PlayDelayedSfx(int id, float delay)
        {
            if (AudioManager.Instance.BgmVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
#if SANGO_DEBUG
                Sango.Log.Info($"延迟播放音效: {result.res}, delay:{delay}");
#endif
                return AudioManager.Instance.PlayDelayedSfx(result.res, delay);
            }
            return -1;
        }
        public void PlayBgm(int id, bool loop = true)
        {
            if (AudioManager.Instance.BgmVolume <= 0 || id <= 0) return;
            if (MediaData.TryGetValue(id, out var result))
            {
#if SANGO_DEBUG
                Sango.Log.Info($"播放背景音乐: {result.res}");
#endif
                AudioManager.Instance.PlayBgm(result.res, loop);
            }
        }

        public void StopSfx(int channel)
        {
            AudioManager.Instance.StopSfx(channel);
        }

        public void StopBgm()
        {
            AudioManager.Instance.StopBgm();
        }

        /// <summary>
        /// 暂停背景音乐
        /// </summary>
        public void PauseBgm()
        {
            AudioManager.Instance.PauseBgm();
        }

        public void ResumeBgm()
        {
            AudioManager.Instance.ResumeBgm();
        }

        public int PlayButtonSfx()
        {
            return PlaySfx(3);
        }

        public int PlayCancelSfx()
        {
            return PlaySfx(4);
        }

        public int PlayDoAcitonSfx()
        {
            return PlaySfx(5);
        }

        public int PlayMenuClickSfx()
        {
            return PlaySfx(4);
        }
        public int PlaySubMenuClickSfx()
        {
            return PlaySfx(4);
        }

        public int PlayNewTurnSfx()
        {
            return PlaySfx(8);
        }

        float voiceVolume = 2.4f;
        int[] mapVoice = new int[] { 3, 2, 1, 0, 5, 4, 0, 2 };
        public int PlayPersonSay(Person person, int sayId)
        {
            /*
             * 1.选择吕布和诸葛亮语音会自动指向男鲁莽和男冷静，修改该位置语音无用
                2.男武将高武和低武音声一样
                3.女武将，判断武将的武、统、智、政中是否"武力"最高，判断是高武还是低武，这4个中武力最高就算高武
            男鲁莽0		男刚胆1		男冷静2		男小心3		女刚胆4		女冷静5		吕布6	诸葛亮7
高武	低武	低武	高武	低武	高武	低武	高武	高武	低武	低武	高武	基本上没用																											
3132	3133	3134	3135	3136	3137	3138	3139	3140	3141	3142	3143	3144	3145

             * */
            int voic = mapVoice[person.voice];
            if (person.sex == 1) //女
            {
                if (person.IsHighStength())
                {
                    if (voic == 4)
                    {
                        return PlayVoice(sayId + voic * 2, voiceVolume);
                    }
                    else
                    {
                        return PlayVoice(sayId + voic * 2 + 1, voiceVolume);
                    }
                }
                else
                {
                    if (voic == 4)
                    {
                        return PlayVoice(sayId + voic * 2 + 1, voiceVolume);
                    }
                    else
                    {
                        return PlayVoice(sayId + voic * 2, voiceVolume);
                    }
                }
            }
            else
            {
                return PlayVoice(sayId + voic * 2, voiceVolume);
            }
        }
    }
}
