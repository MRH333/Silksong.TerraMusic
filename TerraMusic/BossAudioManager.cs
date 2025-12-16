using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                            },
                            new TriggerCondition()
                            {
                                Type = ConditionType.BoolVariableSet,
                                VariableName = "Under HP Check",
                                TargetValue = true,
                            }
                        }
                    }
                }
            };

            bossConfigs[lostLaceConfig.BossName] = lostLaceConfig;
            bossStates[lostLaceConfig.BossName] = new BossState();
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
                foreach (var  condition in audioConfig.Conditions)
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
        private static void PlayCustomAudio(string audioName, float fadeTime)
        {
            if (CustomAudio.AudioDictionary.TryGetValue(audioName, out AudioClip clip))
            {
                //创建一个新的AudioSource来播放自定义音频
                GameObject audioObject = new GameObject($"BossAudio_{audioName}");
                AudioSource source = audioObject.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;

                if (fadeTime > 0)
                {
                    AudioFader.FadeIn(source, fadeTime);//使用改进的淡入
                }
                else
                {
                    source.volume = 1f;
                    source.Play();
                }

                CustomAudio.staticLogger?.LogInfo($"正在播放自制音频：{audioName}");
            }
            else
            {
                CustomAudio.staticLogger?.LogWarning($"没有找到匹配的音频：{audioName}");
            }
        }

        //添加一个交叉淡入淡出的方法
        public static void CrossfadeBossAudio(string bossName, string originalAudio, string customAudio, float duration = 1f)
        {
            AudioSource originalSource = FindAudioSource(originalAudio);
            AudioSource customSource = CreateAudioSource(customAudio);

            if (originalSource != null && customSource != null)
            {
                AudioFader.CrossFade(originalSource,customSource,duration);
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
    }
}
