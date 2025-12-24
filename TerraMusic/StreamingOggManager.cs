using NVorbis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SilksongCustomAudio
{
    /// <summary>
    /// OGG流式AudioClip管理器
    /// </summary>
    public static class StreamingOggManager
    {
        //存储文件信息（不立即创建Reader）（避免重复读取文件头）
        private static readonly ConcurrentDictionary<string, OggFileInfo> fileInfoCache = new ConcurrentDictionary<string, OggFileInfo>();

        //Reader缓存（每个播放实例需要独立的Reader）
        private static readonly ConcurrentDictionary<string, VorbisReader> activeReaders = new ConcurrentDictionary<string, VorbisReader>();

        private class OggFileInfo
        {
            public int Channels { get; set; }
            public int SampleRate { get; set; }
            public long SamplesPerChannel { get; set; }
            public long TotalSamples { get; set; }
        }

        /// <summary>
        /// 为OGG文件创建一个流式AudioClip（延迟加载）
        /// </summary>
        public static AudioClip CreateStreamingClip(string filePath)
        {
            string clipName = Path.GetFileNameWithoutExtension(filePath);

            try
            {
                //VorbisReader reader = new VorbisReader(filePath);
                //int channels = reader.Channels;
                //int sampleRate = reader.SampleRate;
                //long samplesPerChannel = reader.TotalSamples;
                //long totalSamples = samplesPerChannel * channels;

                //1.读取文件头信息（不创建Reader)
                OggFileInfo info = GetOrReadFileInfo(filePath);

                //2.创建流式AudioClip
                AudioClip clip = AudioClip.Create(
                    clipName,
                    (int)info.SamplesPerChannel,
                    info.Channels,
                    info.SampleRate,
                    true,//流式模式
                    (float[] data) => OnAudioRead(filePath, data),//数据回调
                    (int position) => OnAudioSeek(filePath, position)//定位回调
                    );

                //存储映射关系
                //activeReaders[filePath] = reader;
                //clipToPathMap[clip] = filePath;

                CustomAudio.staticLogger?.LogInfo($"创建流式OGG AudioClip：{clipName}（{info.SamplesPerChannel / info.SampleRate:F1}）秒");

                return clip;
            }
            catch (Exception e)
            {
                CustomAudio.staticLogger?.LogError($"创建流式OGG失败：{filePath}：{e.Message}");
                return null;
            }
        }

        ///<summary>
        ///获取或读取OGG文件头信息
        /// </summary>
        private static OggFileInfo GetOrReadFileInfo(string filePath)
        {
            if (!fileInfoCache.TryGetValue(filePath, out OggFileInfo info))
            {
                //第一次读取文件头
                using (VorbisReader reader = new VorbisReader(filePath))
                {
                    info = new OggFileInfo
                    {
                        Channels = reader.Channels,
                        SampleRate = reader.SampleRate,
                        SamplesPerChannel = reader.TotalSamples,
                        TotalSamples = reader.TotalSamples * reader.Channels,
                    };
                    fileInfoCache[filePath] = info;
                }
            }
            return info;
        }

        ///<summary>
        ///音频数据回调（播放时才创建Reader）
        /// </summary>
        private static void OnAudioRead(string filePath, float[] data)
        {
            //获得或创建Reader（第一次播放时）
            if (GetOrCreateReader(filePath, out VorbisReader reader))
            {
                int samplesRead = reader.ReadSamples(data, 0, data.Length);

                //应用25%音量降低
                const float volumeScale = 0.25f;
                for (int i = 0; i < samplesRead; i++)
                {
                    data[i] *= volumeScale;
                }

                //填充剩余部分为静音
                for (int i = samplesRead; i < data.Length; i++)
                {
                    data[i] = 0f;
                }
            }
            else
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        ///<summary>
        ///音频定位回调
        /// </summary>
        private static void OnAudioSeek(string filePath, int position)
        {
            try
            {
                if (GetOrCreateReader(filePath, out VorbisReader reader))
                {
                    // 添加调试日志
                    CustomAudio.staticLogger?.LogDebug($"Seek请求: {position}样本, " +
                                                      $"文件: {Path.GetFileName(filePath)}, " +
                                                      $"总样本: {reader.TotalSamples}, " +
                                                      $"声道: {reader.Channels}");

                    // 检查position范围
                    if (position < 0 || position >= reader.TotalSamples)
                    {
                        CustomAudio.staticLogger?.LogWarning($"Seek位置超出范围: {position}, " +
                                                            $"有效范围: 0-{reader.TotalSamples - 1}");
                        return;
                    }

                    reader.SeekTo(position);
                }

            }
            catch (Exception e)
            {
                CustomAudio.staticLogger?.LogWarning($"OGG seek失败：{e.Message}");
            }
        }

        ///<summary>
        ///获取声道数
        /// </summary>
        private static int GetChannelCount(string filePath)
        {
            if (fileInfoCache.TryGetValue(filePath, out OggFileInfo info))
            {
                return info.Channels;
            }
            return 2;//默认立体声
        }

        ///<summary>
        ///获取或创建VorbisReader（每个文件一个Reader实例）
        /// </summary>
        private static bool GetOrCreateReader(string filePath, out VorbisReader reader)
        {
            if (activeReaders.TryGetValue(filePath, out reader))
            {
                return true;
            }

            try
            {
                reader = new VorbisReader(filePath);
                activeReaders[filePath] = reader;
                return true;
            }
            catch (Exception ex)
            {
                CustomAudio.staticLogger?.LogWarning($"OGG创建Reader失败：{ex.Message}");
                reader = null;
                return false;
            }
        }

        ///<summary>
        ///清理特定文件的资源
        /// </summary>
        private static void CleanupFile(string filePath)
        {
            if (activeReaders.TryRemove(filePath, out VorbisReader reader))
            {
                reader?.Dispose();
            }
            fileInfoCache.TryRemove(filePath, out _);
        }

        ///<summary>
        ///清理所有资源
        /// </summary>
        public static void CleanupAll()
        {
            foreach (var reader in activeReaders.Values)
            {
                reader.Dispose();
            }
            activeReaders.Clear();
            fileInfoCache.Clear();
        }
    }
}
