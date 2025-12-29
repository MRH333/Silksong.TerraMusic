using NVorbis;
using System;
using System.Collections.Concurrent;
using System.IO;
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

        //+++++神吞BGM循环点样本位置+++++
        private static int loopEndBufferSamples = (int)(0.5f * 48000 * 2);//立体声
        private static long DevourerPhase2_LoopStartSample = (long)(10.428f * 48000 * 1);//立体声

        public static int DevourerPhase2SeekedCount = 0;
        public static bool isDevourerPhase2 = false;

        /// <summary>
        /// 为OGG文件创建一个流式AudioClip（延迟加载）
        /// </summary>
        public static AudioClip CreateStreamingClip(string filePath)
        {
            string clipName = Path.GetFileNameWithoutExtension(filePath);

            try
            {
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

                //CustomAudio.staticLogger?.LogInfo($"创建流式OGG AudioClip：{clipName}（{info.SamplesPerChannel / info.SampleRate:F1}）秒");

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

                //=====处理文件结束（神吞BGM循环）======
                if (samplesRead == 0 && isDevourerPhase2)
                {
                    //CustomAudio.staticLogger?.LogInfo($"神吞BGM再次循环结束，重置到循环起点");
                    reader.SeekTo(DevourerPhase2_LoopStartSample);
                    samplesRead = reader.ReadSamples(data, 0, data.Length);
                }
                //================================

                //应用25%音量降低
                const float volumeScale = 0.25f;
                for (int i = 0; i < samplesRead; i++)
                {
                    data[i] *= volumeScale;
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
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                isDevourerPhase2 = fileName == "DevourerofGodsPhase2";

                if (GetOrCreateReader(filePath, out VorbisReader reader))
                {
                    //+++++判断是否是神吞BGM并处理+++++
                    if (isDevourerPhase2)
                    {
                        //CustomAudio.staticLogger?.LogInfo($"神吞BGM检测到Seek请求: {position}样本");
                        if (DevourerPhase2SeekedCount == 1)
                        {
                            reader.SeekTo(DevourerPhase2_LoopStartSample);
                            //CustomAudio.staticLogger?.LogInfo($"神吞BGM第二次Seek，定位到循环点: {DevourerPhase2_LoopStartSample}");
                            DevourerPhase2SeekedCount++;

                            return;
                        }
                        else if (DevourerPhase2SeekedCount > 1)
                        {
                            //reader.SeekTo(DevourerPhase2_LoopStartSample);
                            //CustomAudio.staticLogger?.LogInfo($"神吞BGM多次Seek，不做任何操作");
                            return;
                        }
                        else
                        {
                            //CustomAudio.staticLogger?.LogInfo($"神吞BGM首次Seek，允许正常定位");
                            DevourerPhase2SeekedCount++;
                        }
                    }
                    //+++++++++++++

                    // 添加调试日志
                    CustomAudio.staticLogger?.LogDebug($"Seek请求: {position}样本, " +
                                                      $"文件: {Path.GetFileName(filePath)}, " +
                                                      $"总样本: {reader.TotalSamples}, " +
                                                      $"声道: {reader.Channels}");

                    reader.SeekTo(position);
                }
            }
            catch (Exception e)
            {
                CustomAudio.staticLogger?.LogWarning($"OGG seek失败：{e.Message}");
            }
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

        /////<summary>
        /////清理所有资源
        ///// </summary>
        //public static void CleanupAll()
        //{
        //    foreach (var reader in activeReaders.Values)
        //    {
        //        reader.Dispose();
        //    }
        //    activeReaders.Clear();
        //    fileInfoCache.Clear();
        //}
    }
}
