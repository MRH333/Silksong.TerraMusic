using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WavLib;

namespace SilksongCustomAudio
{
    [BepInPlugin("com.sonyo.customaudio", "Custom Audio", "1.1.2")]
    public class CustomAudio : BaseUnityPlugin
    {
        internal static readonly Dictionary<string, AudioClip> AudioDictionary = new Dictionary<string, AudioClip>();
        internal static ManualLogSource staticLogger;

        // 音频文件存储目录
        private readonly string audioDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Hollow Knight Silksong_Data",
            "Mods",
            "CustomAudio");

        private void Awake()
        {
            staticLogger = Logger;
            CreateCustomAudioDirectory();
            LoadAudio();

            //初始化Boss音频管理器
            BossAudioManager.Initialize();

            // 使用Harmony自动Patch所有标记的方法
            Harmony harmony = new Harmony("com.sonyo.customaudio");
            harmony.PatchAll(typeof(CustomAudio));
            //额外Patch PlayMaker相关类
            harmony.PatchAll(typeof(PlayMakerHooks));
        }

        /// <summary>
        /// 创建自定义音频目录
        /// </summary>
        private void CreateCustomAudioDirectory()
        {
            if (!Directory.Exists(audioDirectory))
            {
                Directory.CreateDirectory(audioDirectory);
                Logger.LogInfo("Created CustomAudio directory");
            }
        }

        /// <summary>
        /// 加载音频文件到字典
        /// </summary>
        private void LoadAudio()
        {
            foreach (string filePath in Directory.GetFiles(audioDirectory))
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                // 排除日志文件
                if (fileName == "AudioLog")
                    continue;

                try
                {
                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        WavData wavData = new WavData();
                        wavData.Parse(stream);

                        if (wavData == null)
                        {
                            Logger.LogWarning($"Failed loading {filePath}");
                            continue;
                        }

                        // 提取音频样本数据
                        float[] samples = wavData.GetSamples();

                        // 创建Unity音频剪辑
                        AudioClip audioClip = AudioClip.Create(
                            fileName,
                            samples.Length / (int)wavData.FormatChunk.NumChannels,
                            (int)wavData.FormatChunk.NumChannels,
                            (int)wavData.FormatChunk.SampleRate,
                            false
                        );

                        audioClip.SetData(samples, 0);
                        AudioDictionary[fileName] = audioClip;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed loading {filePath}");
                    Logger.LogError(ex);
                }
            }

            Logger.LogInfo($"Loaded {AudioDictionary.Count} custom audio files");
        }

        /// <summary>
        /// 尝试替换AudioSource的音频剪辑
        /// </summary>
        private static bool TryReplaceAudio(AudioSource source, out AudioClip replacementClip)
        {
            if (source?.clip != null &&
                AudioDictionary.TryGetValue(source.clip.name, out replacementClip))
            {
                return true;
            }

            replacementClip = null;
            return false;
        }

        /// <summary>
        /// 尝试替换音频剪辑
        /// </summary>
        private static bool TryReplaceAudio(AudioClip originalClip, out AudioClip replacementClip)
        {
            if (originalClip != null &&
                AudioDictionary.TryGetValue(originalClip.name, out replacementClip))
            {
                return true;
            }

            replacementClip = null;
            return false;
        }

        #region AudioSource Hook方法

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), "Play", new Type[] { })]
        private static void Play_Prefix(AudioSource __instance)
        {
            if (TryReplaceAudio(__instance, out AudioClip replacement))
            {
                __instance.clip = replacement;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), "Play", new Type[] { typeof(ulong) })]
        private static void Play_Delayed_Prefix(AudioSource __instance, ulong delay)
        {
            if (__instance.clip != null)
            {
                staticLogger.LogInfo($"Play with delay {delay} for clip: {__instance.clip.name}");
            }

            if (TryReplaceAudio(__instance, out AudioClip replacement))
            {
                __instance.clip = replacement;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), "PlayDelayed", new Type[] { typeof(float) })]
        private static void PlayDelayed_Prefix(AudioSource __instance, float delay)
        {
            if (TryReplaceAudio(__instance, out AudioClip replacement))
            {
                __instance.clip = replacement;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), "PlayScheduled", new Type[] { typeof(double) })]
        private static void PlayScheduled_Prefix(AudioSource __instance, double time)
        {
            if (TryReplaceAudio(__instance, out AudioClip replacement))
            {
                __instance.clip = replacement;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), "PlayOneShot", new Type[] { typeof(AudioClip), typeof(float) })]
        private static void PlayOneShot_Prefix(AudioSource __instance, ref AudioClip clip, float volumeScale)
        {
            if (TryReplaceAudio(clip, out AudioClip replacement))
            {
                clip = replacement;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), "PlayClipAtPoint", new Type[] { typeof(AudioClip), typeof(Vector3), typeof(float) })]
        private static void PlayClipAtPoint_Prefix(ref AudioClip clip, Vector3 position, float volume)
        {
            if (TryReplaceAudio(clip, out AudioClip replacement))
            {
                clip = replacement;
            }
        }

        #endregion

        /// <summary>
        /// 场景切换时重新播放设置了playOnAwake的音频
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "OnNextLevelReady", new Type[] { })]
        private static void OnNextLevelReady_Postfix()
        {
            AudioSource[] allAudioSources = Resources.FindObjectsOfTypeAll<AudioSource>();

            foreach (AudioSource source in allAudioSources)
            {
                if (source.playOnAwake && source.isPlaying)
                {
                    // 重新播放以应用音频替换
                    source.Play();
                }
            }
        }
    }
}