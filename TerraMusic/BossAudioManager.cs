using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongCustomAudio
{
    /// <summary>
    /// 管理Boss特殊音频切换的中央管理器
    /// </summary>
    public static class BossAudioManager
    {
        //Boss配置数据结构
        public class BossAudioConfig
        {
            public string BossName { get; set; }
            public Dictionary<string, PhaseAudioConfig> PhaseConfigs { get; set; } = new Dictionary<string, PhaseAudioConfig>();
        }
        public class PhaseAudioConfig
        {
            public string OriginalAudioName { get; set; }//要停止的音频名称
            public string CustomAudioName { get; set; }//要播放的自定义音频
            public List<TriggerCondition> Conditions { get; set; } = new List<TriggerCondition>();//触发条件
            public bool StopOriginalAudio { get; set; } = true;
            public float FadeOutTime { get; set; } = 0.5f;//淡出时间
            public float FadeInTime { get; set; } = 0.5f;//淡入时间
        }
        public enum ConditionType
        {
            BoolVariableSet,
            FloatVariableChange,
            StateTransition,
            EventFired,
            HPThreshold,
        }
        public class TriggerCondition
        {
            public ConditionType Type { get; set; }
            public string VariableName { get; set; }
            public object TargetValue { get; set; }
            public object PreviousValue { get; set; }
        }

        //存储Boss配置
        private static readonly Dictionary<string, BossAudioConfig> bossConfigs = new Dictionary<string, BossAudioConfig>();

        //记录Boss当前状态
        private static readonly Dictionary<string, BossState> bossStates = new Dictionary<string, BossState>();

        private class BossState
        {
            public string CurrentPhase { get; set; } = "Phase 1";
            public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
            public bool IsAudioSwitching { get; set; }
        }

        //添加跟踪自制音频的字典（被自制音频信息字典更改）
        private static readonly Dictionary<string, CustomAudioSourceInfo> customAudioSourceInfos = new Dictionary<string, CustomAudioSourceInfo>();
        //++++++自制音频信息（用于调节音量适配游戏）+++++
        public class CustomAudioSourceInfo
        {
            public AudioSource Source { get; set; }
            public float BaseVolume { get; set; } = 1f;//基础音量（基于游戏设置）
            public float VolumeMultiplier { get; set; } = 1f;//音量乘数（用于暂停）

            public void UpdateVolume()
            {
                if (Source != null)
                {
                    Source.volume = BaseVolume * VolumeMultiplier;
                }
            }
        }
        //添加音量控制方法
        public static void SetAllCustomAudioBaseVolume(float volume)
        {
            foreach (var kvp in customAudioSourceInfos)
            {
                if (kvp.Value != null && kvp.Value.Source != null)
                {
                    kvp.Value.BaseVolume = volume;
                    kvp.Value.UpdateVolume();
                }
            }
        }
        public static void SetAllCustomAudioVolumeMultiplier(float multiplier)
        {
            foreach (var kvp in customAudioSourceInfos)
            {
                if (kvp.Value != null && kvp.Value.Source != null)
                {
                    kvp.Value.VolumeMultiplier = multiplier;
                    kvp.Value.UpdateVolume();
                }
            }
        }
        public static void SetCustomAudioBaseVolume(string audioName, float volume)
        {
            if (customAudioSourceInfos.TryGetValue(audioName, out var info) && info != null)
            {
                info.BaseVolume = volume;
                info.UpdateVolume();
            }
        }
        //+++++++++++++++++++++++++++++++++++

        ///<summary>
        ///初始化管理器
        ///</summary>
        public static void Initialize()
        {
            //初始化AudioFader
            AudioFader.Initialize();

            LoadDefaultConfigs();
        }

        /// <summary>
        /// 加载默认的Boss配置
        /// </summary>
        private static void LoadDefaultConfigs()
        {
            var lostLaceConfig = new BossAudioConfig()
            {
                BossName = "Lost Lace",
                PhaseConfigs =
                {
                    ["Phase 2"] = new PhaseAudioConfig()
                    {
                        OriginalAudioName = "Final Fight v2-10 Phase 1 v2",
                        CustomAudioName = "CalamitasPhase2",
                        StopOriginalAudio = true,
                        Conditions =
                        {
                            new TriggerCondition()
                            {
                                Type = ConditionType.BoolVariableSet,
                                VariableName = "Phase 2",
                                TargetValue = true,
                            }
                        }
                    }
                }
            };
            bossConfigs[lostLaceConfig.BossName] = lostLaceConfig;
            bossStates[lostLaceConfig.BossName] = new BossState();

            var dockGuardConfig = new BossAudioConfig()
            {
                BossName = "Dock Guard",
                PhaseConfigs =
                {
                    ["Phase 2"] = new PhaseAudioConfig()
                    {
                        OriginalAudioName = "H92 Chorus_Dock_09",
                        CustomAudioName = "Leviathan",
                        StopOriginalAudio = true,
                        Conditions =
                        {
                            new TriggerCondition()
                            {
                                Type = ConditionType.BoolVariableSet,
                                VariableName = "TO P4",
                                TargetValue = true
                            }
                        }
                    }
                }
            };
            bossConfigs[dockGuardConfig.BossName] = dockGuardConfig;
            bossStates[dockGuardConfig.BossName] = new BossState();

            var bellEaterConfig = new BossAudioConfig()
            {
                BossName = "Bell Eater",
                PhaseConfigs =
                {
                    ["Phase 2"] = new PhaseAudioConfig()
                    {
                        OriginalAudioName = "H177 Bell Beast with Live-17_Bellway_Centipede_Arena",
                        CustomAudioName = "DevourerofGodsPhase2",
                        StopOriginalAudio = true,
                        Conditions =
                        {
                            new TriggerCondition()
                            {
                                Type = ConditionType.BoolVariableSet,
                                VariableName = "NEXT",
                                TargetValue = true
                            }
                        }
                    }
                }
            };
            bossConfigs[bellEaterConfig.BossName] = bellEaterConfig;
            bossStates[bellEaterConfig.BossName] = new BossState();

            var fireFatherConfig = new BossAudioConfig()
            {
                BossName = "Fire Father",
                PhaseConfigs =
                {
                    ["Phase 2"] = new PhaseAudioConfig()
                    {
                        OriginalAudioName = "Creepy Main_Belltown_08",
                        CustomAudioName = "Providence",
                        StopOriginalAudio = true,
                        Conditions =
                        {
                            new TriggerCondition()
                            {
                                Type = ConditionType.BoolVariableSet,
                                VariableName = "CORE DAMAGE READY",
                                TargetValue = true
                            }
                        }
                    }
                }
            };
            bossConfigs[fireFatherConfig.BossName] = fireFatherConfig;
            bossStates[fireFatherConfig.BossName] = new BossState();
        }

        ///<summary>
        ///添加自定义Boss配置
        ///</summary>
        public static void AddBossConfig(BossAudioConfig config)
        {
            if (!bossConfigs.ContainsKey(config.BossName))
            {
                bossConfigs[config.BossName] = config;
                bossStates[config.BossName] = new BossState();
                CustomAudio.staticLogger?.LogInfo($"已添加音频配置到Boss：{config.BossName}");
            }
        }

        ///<summary>
        ///处理变量变化，触发音频切换
        ///</summary>
        public static void OnVariableChanged(string bossName, string variableName, object newValue)
        {
            if (!bossStates.TryGetValue(bossName, out var state))
                return;

            if (!bossConfigs.TryGetValue(bossName, out var config))
                return;

            //记录变量变化
            state.Variables[variableName] = newValue;
            //object oldValue = state.Variables.ContainsKey(variableName)
            //    ? state.Variables[variableName] 
            //    : null;

            foreach (var phaseConfig in config.PhaseConfigs)
            {
                string phaseName = phaseConfig.Key;
                PhaseAudioConfig audioConfig = phaseConfig.Value;

                //如果已经是这个Phase，跳过
                if (state.CurrentPhase == phaseName)
                    continue;

                //检查所有条件是否满足
                bool allConditionsMet = true;
                foreach (var condition in audioConfig.Conditions)
                {
                    object valueToCheck = null;

                    //确定用哪个值检查条件
                    if (condition.VariableName == variableName)
                    {
                        valueToCheck = newValue;
                    }
                    else if (state.Variables.ContainsKey(condition.VariableName))
                    {
                        valueToCheck = state.Variables[condition.VariableName];
                    }
                    else
                    {
                        //变量未出现过，不满足
                        allConditionsMet = false;
                        break;
                    }

                    //检查条件
                    if (!IsConditionMet(condition, valueToCheck))
                    {
                        allConditionsMet = false;
                        break;
                    }
                }

                if (allConditionsMet)
                {
                    SwitchBossPhase(bossName, phaseName, audioConfig);
                    break;
                }
            }
        }

        private static bool IsConditionMet(TriggerCondition condition, object currentValue)
        {
            switch (condition.Type)
            {
                case ConditionType.BoolVariableSet:
                    if (condition.TargetValue is bool targetBool && currentValue is bool currentBool)
                        return currentBool == targetBool;
                    break;

                case ConditionType.HPThreshold:
                    if (condition.TargetValue is float targetHP && currentValue is float currentHP)
                        return currentHP == targetHP;
                    break;
            }
            return false;
        }

        ///<summary>
        ///切换Boss阶段音频
        ///</summary>
        private static void SwitchBossPhase(string bossName, string phaseName, PhaseAudioConfig config)
        {
            if (!bossStates.TryGetValue(bossName, out var state))
                return;

            if (state.IsAudioSwitching)
                return;

            state.IsAudioSwitching = true;
            state.CurrentPhase = phaseName;

            CustomAudio.staticLogger?.LogInfo($"正在切换Boss {bossName} 到 {phaseName} 音频");

            //停止原音频
            if (config.StopOriginalAudio && !string.IsNullOrEmpty(config.OriginalAudioName))
            {
                StopOriginalAudio(config.OriginalAudioName, config.FadeOutTime);
            }

            //播放原音频
            if (!string.IsNullOrEmpty(config.CustomAudioName))
            {
                PlayCustomAudio(config.CustomAudioName, config.FadeInTime);
            }

            state.IsAudioSwitching = false;
        }

        private static void StopOriginalAudio(string audioName, float fadeTime)
        {
            AudioSource[] allSources = GameObject.FindObjectsOfType<AudioSource>();
            foreach (var source in allSources)
            {
                if (source.isPlaying && source.clip != null &&
                    source.clip.name.Contains(audioName))
                {
                    if (fadeTime > 0)
                    {
                        AudioFader.FadeOut(source, fadeTime);//使用改进的淡出
                    }
                    else
                    {
                        source.Stop();
                    }

                    CustomAudio.staticLogger?.LogInfo($"停止音频：{source.clip.name}");
                }
            }
        }
        public static void PlayCustomAudio(string audioName, float fadeTime)
        {
            if (!CustomAudio.AudioDictionary.TryGetValue(audioName, out AudioClip clip))
            {
                CustomAudio.staticLogger?.LogWarning($"没有找到匹配的音频：{audioName}");
                return;
            }

            //检查是否已存在相同音频的AudioSource
            if (customAudioSourceInfos.TryGetValue(audioName, out var existingInfo))
            {
                if (existingInfo != null && existingInfo.Source != null)
                {
                    if (existingInfo.Source.isPlaying)
                    {
                        CustomAudio.staticLogger?.LogInfo($"音频已在播放：{audioName}");
                        return;
                    }

                    existingInfo.Source.clip = clip;
                    existingInfo.Source.loop = true;

                    //设置初始音量（基于当前游戏设置）
                    existingInfo.BaseVolume = 1f;//将在UpdateCustomAudioVolumes更新
                    existingInfo.VolumeMultiplier = 1f;
                    existingInfo.UpdateVolume();

                    if (fadeTime > 0)
                    {
                        AudioFader.FadeIn(existingInfo.Source, fadeTime);
                    }
                    else
                    {
                        existingInfo.Source.Play();
                    }

                    CustomAudio.staticLogger?.LogInfo($"重新播放自制音频：{audioName}");
                    return;
                }
                else
                {
                    //清除无效引用
                    customAudioSourceInfos.Remove(audioName);
                }
            }

            //创建一个新的AudioSource来播放自定义音频
            GameObject audioObject = new GameObject($"BossAudio_{audioName}");
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;

            //创建信息对象
            var audioInfo = new CustomAudioSourceInfo()
            {
                Source = source,
                BaseVolume = GameAudioEventListener.CurrentBaseVolume,//使用游戏音量
                VolumeMultiplier = 1f,
            };

            customAudioSourceInfos[audioName] = audioInfo;
            audioInfo.UpdateVolume();//立即应用音量

            if (fadeTime > 0)
            {
                AudioFader.FadeIn(source, fadeTime);//使用改进的淡入
            }
            else
            {
                source.Play();
            }

            CustomAudio.staticLogger?.LogInfo($"正在播放自制音频：{audioName}");
        }


        //添加一个交叉淡入淡出的方法
        public static void CrossfadeBossAudio(string bossName, string originalAudio, string customAudio, float duration = 1f)
        {
            AudioSource originalSource = FindAudioSource(originalAudio);
            AudioSource customSource = CreateAudioSource(customAudio);

            if (originalSource != null && customSource != null)
            {
                AudioFader.CrossFade(originalSource, customSource, duration);
            }
        }
        private static AudioSource FindAudioSource(string audioName)
        {
            AudioSource[] allSources = GameObject.FindObjectsOfType<AudioSource>();
            foreach (var source in allSources)
            {
                if (source.isPlaying && source.clip != null &&
                    source.clip.name.Contains(audioName))
                    return source;
            }
            return null;
        }
        private static AudioSource CreateAudioSource(string audioName)
        {
            if (!CustomAudio.AudioDictionary.TryGetValue(audioName, out AudioClip clip))
                return null;

            GameObject audioObject = new GameObject($"BossAudio_{audioName}");
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            return source;
        }

        //+++++停止自制音频、清理无效音频源功能+++++
        public static void StopAllCustomAudio(float fadeTime = 0.5f)
        {
            foreach (var kvp in customAudioSourceInfos.ToList())
            {
                if (kvp.Value != null && kvp.Value.Source != null && kvp.Value.Source.isPlaying)
                {
                    //+++++判断是否停止了神吞BGM++++++
                    if (kvp.Key == "DevourerofGodsPhase2")
                    {
                        StreamingOggManager.isDevourerPhase2 = false;
                        StreamingOggManager.DevourerPhase2SeekedCount = 0;
                    }
                    //+++++++++++++++++++++++++++

                    if (fadeTime > 0)
                    {
                        AudioFader.FadeOut(kvp.Value.Source, fadeTime);
                    }
                    else
                    {
                        kvp.Value.Source.Stop();
                    }
                    CustomAudio.staticLogger?.LogInfo($"停止自制音频：{kvp.Key}");
                }
            }
        }

        public static void CleanupInvalidSources()
        {
            var keysToRemove = new List<string>();
            foreach (var kvp in customAudioSourceInfos)
            {
                if (kvp.Value == null || kvp.Value.Source == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                customAudioSourceInfos.Remove(key);
            }
        }

        //重置状态功能
        public static void ResetAllBossStates()
        {
            foreach (var kvp in bossStates)
            {
                kvp.Value.CurrentPhase = "Phase 1";
                kvp.Value.Variables.Clear();
                kvp.Value.IsAudioSwitching = false;
            }

            CustomAudio.staticLogger?.LogInfo("重置所有Boss状态");
        }

        ////+++++获取所有自定义音频源（供事件监听器使用）+++++
        //public static List<AudioSource> GetAllCustomAudioSources()
        //{
        //    List<AudioSource> sources = new List<AudioSource>();
        //    foreach (var kvp in customAudioSourceInfos)
        //    {
        //        if (kvp.Value != null && kvp.Value.Source != null)
        //        {
        //            sources.Add(kvp.Value.Source);
        //        }
        //    }
        //    return sources;
        //}
        ////+++++++++++++++++++++++++++++++
    }
}
