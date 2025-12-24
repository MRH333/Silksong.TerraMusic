using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using NVorbis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WavLib;

namespace SilksongCustomAudio
{
    [BepInPlugin("io.github.MRH333.TerraMusic", "TerraMusic", "1.0.0")]
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

            //初始化Boss音频管理器与游戏音频事件监听器
            BossAudioManager.Initialize();
            //GameAudioEventListener.Initialize();

            // 使用Harmony自动Patch所有标记的方法
            Harmony harmony = new Harmony("io.github.MRH333.TerraMusic");
            harmony.PatchAll(typeof(CustomAudio));
            //额外Patch PlayMaker相关类
            harmony.PatchAll(typeof(PlayMakerHooks));

            harmony.PatchAll(typeof(GameAudioEventListener));
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
        /// 加载音频文件到字典（支持WAV和OGG格式）
        /// </summary>
        private void LoadAudio()
        {
            int wavCount = 0;
            int streamingOggCount = 0;

            //加载WAV文件
            foreach (string filePath in Directory.GetFiles(audioDirectory, "*.wav", SearchOption.AllDirectories))
            {
                if (LoadWavFile(filePath))
                    wavCount++;
            }

            //加载OGG文件（只创建流式AudioClip，不预加载到内存）
            foreach (string filePath in Directory.GetFiles(audioDirectory, "*.ogg", SearchOption.AllDirectories))
            {
                //只创建流式AudioClip引用，不立即解码
                if (RegisterStreamingOgg(filePath))
                    streamingOggCount++;
            }

            Logger.LogInfo($"已加载 {wavCount} 个WAV音频文件和 {streamingOggCount} 个流式OGG音频文件");
        }

        /// <summary>
        /// 加载WAV文件
        /// </summary>
        private bool LoadWavFile(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // 排除日志文件
            if (fileName == "AudioLog")
                return false;

            try
            {
                using (FileStream stream = File.OpenRead(filePath))
                {
                    WavData wavData = new WavData();
                    wavData.Parse(stream);

                    if (wavData == null)
                    {
                        Logger.LogWarning($"Failed loading {filePath}");
                        return false;
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

                    //添加到字典，如果存在同名文件，OGG优先
                    if (!AudioDictionary.ContainsKey(fileName) || Path.GetExtension(filePath).ToLower() == ".ogg")
                    {
                        AudioDictionary[fileName] = audioClip;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"加载WAV失败： {filePath}");
                Logger.LogError(ex);
            }

            return false;
        }

        ///<summary>
        ///注册流式OGG文件（不加载数据，只创建引用）
        /// </summary>
        private bool RegisterStreamingOgg(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            // 排除日志文件
            if (fileName == "AudioLog")
                return false;
            try
            {

                //创建流式AudioClip
                AudioClip clip = StreamingOggManager.CreateStreamingClip(filePath);

                //添加到字典，OGG文件优先
                if (clip != null)
                {
                    AudioDictionary[fileName] = clip;//流式clip，没有预加载数据

                    var fileInfo = new FileInfo(filePath);
                    Logger.LogDebug($"已注册流式OGG：{fileName} （{fileInfo.Length / 1024}KB）");

                    return true;
                }
                else
                {
                   Logger.LogWarning($"注册流式OGG失败，无法创建AudioClip：{fileName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"注册流式OGG失败： {filePath}");
                Logger.LogError(ex);
            }
            return false;
        }

        ///<summary>
        ///加载OGG文件
        /// </summary>
        private bool LoadOggFile(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            // 排除日志文件
            if (fileName == "AudioLog")
                return false;

            //+++++调试+++++
            Logger.LogInfo($"尝试加载OGG文件：{filePath}");

            try
            {
                using (var vorbis = new VorbisReader(filePath))
                {
                    //获取音频信息
                    int channels = vorbis.Channels;
                    int sampleRate = vorbis.SampleRate;
                    long samplesPerChannel = vorbis.TotalSamples;//TotalSamples已经是每声道样本数
                    long totalSamples = samplesPerChannel * channels;//计算总样本数（用于缓冲区大小）
                    //int samplesPerChannel = (int)(totalSamples / channels);

                    //+++++调试+++++
                    Logger.LogInfo($" - 报告总样本数：{totalSamples}");
                    Logger.LogInfo($" - 每声道总样本数：{samplesPerChannel}");

                    //创建缓冲区并读取所有样本
                    float[] buffer = new float[totalSamples];
                    int samplesRead = vorbis.ReadSamples(buffer, 0, (int)totalSamples);

                    if (samplesRead <= 0)
                    {
                        Logger.LogWarning($"OGG文件读取失败或为空：{filePath}");
                        return false;
                    }

                    //降低音量到25%
                    float volumeScale = 0.25f;//25%音量
                    for (int i = 0; i < samplesRead; i++)
                    {
                        buffer[i] *= volumeScale;
                    }

                    //创建Unity音频剪辑
                    AudioClip audioClip = AudioClip.Create(
                        fileName,
                        //samplesRead / channels,
                        (int)samplesPerChannel,//每声道样本数
                        channels,
                        sampleRate,
                        false
                        );
                    audioClip.SetData(buffer, 0);

                    //添加到字典，OGG文件优先
                    AudioDictionary[fileName] = audioClip;

                    //记录文件大小对比
                    var fileInfo = new FileInfo(filePath);
                    Logger.LogDebug($"已加载OGG：{fileName} （{fileInfo.Length / 1024}KB，" +
                        $"{channels}声道，{sampleRate}Hz）");


                    //+++++调试+++++
                    Logger.LogInfo($" - 实际创建AudioClip时长：{samplesPerChannel / (double)sampleRate:F2}秒");
                    //+++++++++++++

                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"加载OGG失败： {filePath}");
                Logger.LogError(ex);
            }
            return false;
        }

        ///<summary>
        ///获取所有支持的音频扩展名（用于调试）
        /// </summary>
        public static string[] GetSupportedExtensions()
        {
            return new string[] { ".wav", ".ogg" };
        }


        /// <summary>
        /// 尝试替换AudioSource的音频剪辑
        /// </summary>
        private static bool TryReplaceAudio(AudioSource source, out AudioClip replacementClip)
        {
            //先尝试场景特定音频
            replacementClip = null;
            if (source?.clip == null) return false;

            string originalName = source.clip.name;
            string currentScene = SceneManager.GetActiveScene().name;

            if (!string.IsNullOrEmpty(currentScene))
            {
                string sceneSpecificName = $"{originalName}_{currentScene}";
                if (AudioDictionary.TryGetValue(sceneSpecificName, out replacementClip))
                {
                    staticLogger?.LogInfo($"使用场景特定音频：{sceneSpecificName}");
                    return true;
                }
            }


            //再尝试通用音频
            if (AudioDictionary.TryGetValue(source.clip.name, out replacementClip))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 尝试替换音频剪辑
        /// </summary>
        private static bool TryReplaceAudio(AudioClip originalClip, out AudioClip replacementClip)
        {
            replacementClip = null;
            if (originalClip == null) return false;

            string originalName = originalClip.name;
            string currentScene = SceneManager.GetActiveScene().name;

            if (!string.IsNullOrEmpty(currentScene))
            {
                string sceneSpecificName = $"{originalName}_{currentScene}";
                if (AudioDictionary.TryGetValue(sceneSpecificName, out replacementClip))
                {
                    staticLogger?.LogInfo($"使用场景特定音频：{sceneSpecificName}");
                    return true;
                }
            }

            if (AudioDictionary.TryGetValue(originalClip.name, out replacementClip))
            {
                return true;
            }

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