using UnityEngine;
using System.Collections.Generic;
using Sango.Loader;

namespace Sango.Manager
{
    /// <summary>
    /// 音效管理器
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        /// <summary>
        /// 音效声道数量
        /// </summary>
        private const int SFX_CHANNEL_COUNT = 50;

        /// <summary>
        /// 背景音乐AudioSource
        /// </summary>
        private AudioSource _bgmSource;

        /// <summary>
        /// 音效AudioSource列表
        /// </summary>
        private List<AudioSource> _sfxSources;

        /// <summary>
        /// 空闲的音效声道索引
        /// </summary>
        private Queue<int> _freeSfxChannels;

        /// <summary>
        /// 音效资源缓存
        /// </summary>
        private Dictionary<string, AudioClip> _audioClipCache;

        /// <summary>
        /// 背景音乐音量
        /// </summary>
        public float BgmVolume { get; set; } = 1f;

        /// <summary>
        /// 音效音量
        /// </summary>
        public float SfxVolume { get; set; } = 1f;

        /// <summary>
        /// 语音音量
        /// </summary>
        public float VoiceVolume { get; set; } = 1f;


        public float MaxBgmVolume { get; set; } = 0.5f;
        public float MaxSfxVolume { get; set; } = 0.8f;
        public float MaxVoiceVolume { get; set; } = 1f;

        /// <summary>
        /// 淡入淡出状态
        /// </summary>
        private enum FadeState
        {
            None,
            FadingOut,
            FadingIn
        }

        /// <summary>
        /// 当前淡入淡出状态
        /// </summary>
        private FadeState _fadeState = FadeState.None;

        /// <summary>
        /// 淡入淡出持续时间
        /// </summary>
        private float _fadeDuration = 1.0f;

        /// <summary>
        /// 淡入淡出当前时间
        /// </summary>
        private float _fadeTime = 0f;

        /// <summary>
        /// 目标背景音乐
        /// </summary>
        private string _targetBgmName;

        /// <summary>
        /// 目标背景音乐是否循环
        /// </summary>
        private bool _targetBgmLoop;

        /// <summary>
        /// 原始音量
        /// </summary>
        private float _originalVolume;

        /// <summary>
        /// 初始化音效管理器
        /// </summary>
        public void Init()
        {
            // 创建音效管理器GameObject
            GameObject audioManagerObj = new GameObject("AudioManager");
            GameObject.DontDestroyOnLoad(audioManagerObj);

            // 创建背景音乐AudioSource
            _bgmSource = audioManagerObj.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.volume = BgmVolume;

            // 初始化音效声道
            _sfxSources = new List<AudioSource>();
            _freeSfxChannels = new Queue<int>();

            for (int i = 0; i < SFX_CHANNEL_COUNT; i++)
            {
                AudioSource source = audioManagerObj.AddComponent<AudioSource>();
                source.volume = SfxVolume;
                _sfxSources.Add(source);
                _freeSfxChannels.Enqueue(i);
            }

            // 初始化音效缓存
            _audioClipCache = new Dictionary<string, AudioClip>();
        }

        /// <summary>
        /// 播放背景音乐
        /// </summary>
        /// <param name="audioName">音效名称</param>
        /// <param name="loop">是否循环</param>
        public void PlayBgm(string audioName, bool loop = true)
        {
            if (BgmVolume <= 0) return;
            AudioClip clip = LoadAudioClip(audioName);
            if (clip != null)
            {
                _bgmSource.clip = clip;
                _bgmSource.loop = loop;
                _bgmSource.volume = BgmVolume * MaxBgmVolume;
                _bgmSource.Play();
            }
        }

        /// <summary>
        /// 停止背景音乐
        /// </summary>
        public void StopBgm()
        {
            _bgmSource.Stop();
        }

        /// <summary>
        /// 暂停背景音乐
        /// </summary>
        public void PauseBgm()
        {
            _bgmSource.Pause();
        }

        /// <summary>
        /// 恢复背景音乐
        /// </summary>
        public void ResumeBgm()
        {
            _bgmSource.UnPause();
        }

        /// <summary>
        /// 切换背景音乐
        /// </summary>
        /// <param name="audioName">音效名称</param>
        /// <param name="loop">是否循环</param>
        public void SwitchBgm(string audioName, bool loop = true)
        {
            StopBgm();
            PlayBgm(audioName, loop);
        }

        /// <summary>
        /// 带淡入淡出的切换背景音乐
        /// </summary>
        /// <param name="audioName">音效名称</param>
        /// <param name="loop">是否循环</param>
        /// <param name="fadeDuration">淡入淡出持续时间</param>
        public void SwitchBgmWithFade(string audioName, bool loop = true, float fadeDuration = 1.0f)
        {
            if (_fadeState != FadeState.None) return;

            _targetBgmName = audioName;
            _targetBgmLoop = loop;
            _fadeDuration = fadeDuration;
            _fadeTime = 0f;
            _originalVolume = _bgmSource.volume;
            _fadeState = FadeState.FadingOut;
        }

        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="audioName">音效名称</param>
        /// <param name="volume">音量</param>
        /// <returns>播放音效的声道索引</returns>
        public int PlaySfx(string audioName, float volume = 1.0f)
        {
            if (SfxVolume <= 0) return -1;
            AudioClip clip = LoadAudioClip(audioName);
            if (clip == null)
                return -1;

            int channelIndex = GetFreeSfxChannel(clip);
            if (channelIndex >= 0)
            {
                AudioSource source = _sfxSources[channelIndex];
                source.clip = clip;
                source.volume = SfxVolume * volume * MaxSfxVolume;
                source.Play();
            }

            return channelIndex;
        }

        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="audioName">音效名称</param>
        /// <param name="volume">音量</param>
        /// <returns>播放音效的声道索引</returns>
        public int PlaySfxLoop(string audioName, float volume = 1.0f)
        {
            if (SfxVolume <= 0) return -1;
            AudioClip clip = LoadAudioClip(audioName);
            if (clip == null)
                return -1;

            int channelIndex = GetFreeSfxChannel(clip);
            if (channelIndex >= 0)
            {
                AudioSource source = _sfxSources[channelIndex];
                source.clip = clip;
                source.loop = true;
                source.volume = SfxVolume * volume * MaxSfxVolume;
                source.Play();
            }

            return channelIndex;
        }

        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="audioName">音效名称</param>
        /// <param name="volume">音量</param>
        /// <returns>播放音效的声道索引</returns>
        public int PlayVoice(string audioName, float volume = 1.0f)
        {
            if (SfxVolume <= 0) return -1;
            AudioClip clip = LoadAudioClip(audioName);
            if (clip == null)
                return -1;

            int channelIndex = GetFreeSfxChannel(clip);
            if (channelIndex >= 0)
            {
                AudioSource source = _sfxSources[channelIndex];
                source.clip = clip;
                source.volume = volume * MaxVoiceVolume;
                source.Play();
            }

            return channelIndex;
        }


        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="audioName">音效名称</param>
        /// <param name="volume">音量</param>
        /// <returns>播放音效的声道索引</returns>
        public int PlayDelayedSfx(string audioName, float delay = 0f)
        {
            if (SfxVolume <= 0) return -1;
            AudioClip clip = LoadAudioClip(audioName);
            if (clip == null)
                return -1;

            int channelIndex = GetFreeSfxChannel(clip);
            if (channelIndex >= 0)
            {
                AudioSource source = _sfxSources[channelIndex];
                source.clip = clip;
                source.volume = SfxVolume * MaxSfxVolume;
                source.PlayDelayed(delay);
            }

            return channelIndex;
        }



        /// <summary>
        /// 播放3D音效
        /// </summary>
        /// <param name="audioName">音效名称</param>
        /// <param name="position">位置</param>
        /// <param name="volume">音量</param>
        /// <returns>播放音效的声道索引</returns>
        public int PlaySfx3D(string audioName, Vector3 position, float volume = 1.0f)
        {
            if (SfxVolume <= 0) return -1;
            AudioClip clip = LoadAudioClip(audioName);
            if (clip == null)
                return -1;

            int channelIndex = GetFreeSfxChannel(clip);
            if (channelIndex >= 0)
            {
                AudioSource source = _sfxSources[channelIndex];
                source.clip = clip;
                source.volume = SfxVolume * volume * MaxSfxVolume; ;
                source.spatialBlend = 1.0f; // 3D音效
                source.transform.position = position;
                source.Play();
            }

            return channelIndex;
        }

        /// <summary>
        /// 停止音效
        /// </summary>
        /// <param name="channelIndex">声道索引</param>
        public void StopSfx(int channelIndex)
        {
            if (channelIndex >= 0 && channelIndex < _sfxSources.Count)
            {
                AudioSource source = _sfxSources[channelIndex];
                source.Stop();
                _freeSfxChannels.Enqueue(channelIndex);
            }
        }

        public void StopLoopSfx()
        {
            if (SfxVolume <= 0) return;

            // 查找正在播放完成的声道
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                AudioSource source = _sfxSources[i];
                if (source.isPlaying && source.loop == true)
                {
                    source.loop = false;
                    source.Stop();
                }
            }
        }

        public void StopSfx(string audioName)
        {
            if (SfxVolume <= 0) return;
            AudioClip clip = LoadAudioClip(audioName);
            if (clip == null)
                return;

            // 查找正在播放完成的声道
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                AudioSource source = _sfxSources[i];
                if (source.isPlaying && source.clip == clip)
                {
                    source.loop = false;
                    source.Stop();
                    return;
                }
            }
        }

        /// <summary>
        /// 停止所有音效
        /// </summary>
        public void StopAllSfx()
        {
            foreach (AudioSource source in _sfxSources)
            {
                source.Stop();
            }

            // 重新初始化空闲声道队列
            _freeSfxChannels.Clear();
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                _freeSfxChannels.Enqueue(i);
            }
        }

        /// <summary>
        /// 设置背景音乐音量
        /// </summary>
        /// <param name="volume">音量（0-1）</param>
        public void SetBgmVolume(float volume)
        {
            BgmVolume = Mathf.Clamp01(volume);
            _bgmSource.volume = BgmVolume;
        }

        /// <summary>
        /// 设置音效音量
        /// </summary>
        /// <param name="volume">音量（0-1）</param>
        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            foreach (AudioSource source in _sfxSources)
            {
                source.volume = SfxVolume;
            }
        }

        /// <summary>
        /// 设置背景音乐音量
        /// </summary>
        /// <param name="volume">音量（0-1）</param>
        public void SetVoiceVolume(float volume)
        {
            VoiceVolume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// 加载音频剪辑
        /// </summary>
        /// <param name="audioName">音频名称</param>
        /// <returns>音频剪辑</returns>
        private AudioClip LoadAudioClip(string audioName)
        {
            // 检查缓存
            if (_audioClipCache.TryGetValue(audioName, out AudioClip cachedClip))
            {
                return cachedClip;
            }

            // 使用ObjectLoader加载音频
            AudioClip clip = ObjectLoader.LoadObject<AudioClip>(audioName);
            if (clip != null)
            {
                // 缓存音频
                _audioClipCache[audioName] = clip;
            }

            return clip;
        }


        /// <summary>
        /// 获取空闲的音效声道
        /// </summary>
        /// <returns>声道索引，-1表示没有空闲声道</returns>
        private int GetFreeSfxChannel(string resName)
        {
            if (!string.IsNullOrEmpty(resName))
            {
                // 查找正在播放完成的声道
                for (int i = 0; i < _sfxSources.Count; i++)
                {
                    AudioSource source = _sfxSources[i];
                    if (source.isPlaying && source.clip.name == resName)
                    {
                        return i;
                    }
                }
            }

            // 检查是否有空闲声道
            if (_freeSfxChannels.Count > 0)
            {
                return _freeSfxChannels.Dequeue();
            }

            // 查找正在播放完成的声道
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                if (!_sfxSources[i].isPlaying)
                {
                    return i;
                }
            }


            // 没有空闲声道，返回-1
            return -1;
        }

        private int GetFreeSfxChannel(AudioClip clip)
        {
            if (clip != null)
            {
                int rs = -1;
                // 查找正在播放完成的声道
                for (int i = 0; i < _sfxSources.Count; i++)
                {
                    AudioSource source = _sfxSources[i];
                    if (source.isPlaying)
                    {
                        if (source.clip != clip && source.loop == true)
                        {
                            source.loop = false;
                            source.Stop();
                        }
                        else if (source.clip == clip)
                            rs = i;
                    }
                }

                if (rs >= 0)
                    return rs;
            }

            // 检查是否有空闲声道
            if (_freeSfxChannels.Count > 0)
            {
                return _freeSfxChannels.Dequeue();
            }

            // 查找正在播放完成的声道
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                if (!_sfxSources[i].isPlaying)
                {
                    return i;
                }
            }


            // 没有空闲声道，返回-1
            return -1;
        }


        /// <summary>
        /// 更新逻辑
        /// </summary>
        public void Update()
        {
            // 处理淡入淡出逻辑
            HandleFade();

            // 回收完成播放的音效声道
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                if (!_sfxSources[i].isPlaying && !_freeSfxChannels.Contains(i))
                {
                    _freeSfxChannels.Enqueue(i);
                }
            }
        }

        /// <summary>
        /// 处理淡入淡出逻辑
        /// </summary>
        private void HandleFade()
        {
            if (_fadeState == FadeState.None) return;

            _fadeTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(_fadeTime / _fadeDuration);

            switch (_fadeState)
            {
                case FadeState.FadingOut:
                    // 音量从原始值逐渐减小到0
                    _bgmSource.volume = Mathf.Lerp(_originalVolume, 0, normalizedTime);

                    if (normalizedTime >= 1)
                    {
                        // 淡出完成，切换音乐
                        _bgmSource.Stop();
                        AudioClip clip = LoadAudioClip(_targetBgmName);
                        if (clip != null)
                        {
                            _bgmSource.clip = clip;
                            _bgmSource.loop = _targetBgmLoop;
                            _bgmSource.volume = 0;
                            _bgmSource.Play();
                        }
                        _fadeState = FadeState.FadingIn;
                        _fadeTime = 0f;
                    }
                    break;

                case FadeState.FadingIn:
                    // 音量从0逐渐增加到原始值
                    _bgmSource.volume = Mathf.Lerp(0, _originalVolume, normalizedTime);

                    if (normalizedTime >= 1)
                    {
                        // 淡入完成
                        _fadeState = FadeState.None;
                    }
                    break;
            }


        }

    }
}
