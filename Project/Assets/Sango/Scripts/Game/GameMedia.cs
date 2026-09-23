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
        /// ����json����
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
            if (AudioManager.Instance.VoiceVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
                Sango.Log.Info($"��������: {result.res}");
                return AudioManager.Instance.PlayVoice(result.res);
            }
            return -1;
        }

        public int PlayVoice(int id, float volume)
        {
            if (AudioManager.Instance.VoiceVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
                Sango.Log.Info($"��������: {result.res}");
                return AudioManager.Instance.PlayVoice(result.res, volume);
            }
            return -1;
        }

        public int PlaySfx(int id)
        {
            if (AudioManager.Instance.SfxVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
                Sango.Log.Info($"������Ч: {result.res}");
                return AudioManager.Instance.PlaySfx(result.res);
            }
            return -1;
        }

        public int PlaySfxLoop(int id)
        {
            if (AudioManager.Instance.SfxVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
                Sango.Log.Info($"������Ч: {result.res}");
                return AudioManager.Instance.PlaySfxLoop(result.res);
            }
            return -1;
        }

        public int PlaySfx(int id, float volume)
        {
            if (AudioManager.Instance.SfxVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
                Sango.Log.Info($"������Ч: {result.res}");
                return AudioManager.Instance.PlaySfx(result.res, volume);
            }
            return -1;
        }

        public int PlayDelayedSfx(int id, float delay)
        {
            if (AudioManager.Instance.SfxVolume <= 0 || id <= 0) return -1;
            if (MediaData.TryGetValue(id, out var result))
            {
                Sango.Log.Info($"�ӳٲ�����Ч: {result.res}, delay:{delay}");
                return AudioManager.Instance.PlayDelayedSfx(result.res, delay);
            }
            return -1;
        }
        public void PlayBgm(int id, bool loop = true)
        {
            if (AudioManager.Instance.BgmVolume <= 0 || id <= 0) return;
            if (MediaData.TryGetValue(id, out var result))
            {
                Sango.Log.Info($"���ű�������: {result.res}");
                AudioManager.Instance.PlayBgm(result.res, loop);
            }
        }

        public void StopLoopSfx()
        {
            AudioManager.Instance.StopLoopSfx();
        }


        public void StopSfx(int id)
        {
            if (MediaData.TryGetValue(id, out var result))
            {
                Sango.Log.Info($"ֹͣ��Ч: {result.res}");
                AudioManager.Instance.StopSfx(result.res);
            }
        }

        public void StopBgm()
        {
            AudioManager.Instance.StopBgm();
        }

        /// <summary>
        /// ��ͣ��������
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
             * 1.ѡ�������������������Զ�ָ����³ç�����侲���޸ĸ�λ����������
                2.���佫����͵�������һ��
                3.Ů�佫���ж��佫���䡢ͳ���ǡ������Ƿ�"����"��ߣ��ж��Ǹ��仹�ǵ��䣬��4����������߾������
            ��³ç0		�иյ�1		���侲2		��С��3		Ů�յ�4		Ů�侲5		����6	�����7
����	����	����	����	����	����	����	����	����	����	����	����	������û��																											
3132	3133	3134	3135	3136	3137	3138	3139	3140	3141	3142	3143	3144	3145

             * */
            int voic = mapVoice[person.voice];
            if (person.sex == 1) //Ů
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
