using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SilksongCustomAudio
{
    ///<summary>
    ///辅助类：音频淡入淡出
    ///</summary>
    public static class AudioFader
    {
        private static MonoBehaviour coroutineRunner;

        /// <summary>
        /// 初始化AudioFader
        /// </summary>
        public static void Initialize()
        {
            GameObject runnerObj = new GameObject("AudioFaderCoroutineRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            coroutineRunner = runnerObj.AddComponent<AudioFaderRunner>();
        }

        /// <summary>
        /// 淡出音频
        /// </summary>
        public static void FadeOut(AudioSource source, float duration)
        {
            if (source == null || !source.isPlaying) return;

            coroutineRunner.StartCoroutine(FadeOutCoroutine(source, duration));
        }

        /// <summary>
        /// 淡入音频
        /// </summary>
        public static void FadeIn(AudioSource source, float duration)
        {
            if (source == null) return;

            coroutineRunner.StartCoroutine(FadeInCoroutine(source, duration));
        }

        ///<summary>
        ///交叉淡入淡出（一个音频淡出，另一个淡入）
        ///</summary>
        public static void CrossFade(AudioSource fadeOutSource, AudioSource fadeInSource, float duration)
        {
            coroutineRunner.StartCoroutine(CrossfadeCoroutine(fadeOutSource, fadeInSource, duration));
        }

        private static IEnumerator FadeOutCoroutine(AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                source.volume = Mathf.Lerp(source.volume, 0f, timer / duration);
                yield return null;
            }
            source.Stop();
            source.volume = startVolume;//恢复原始音量
        }

        private static IEnumerator FadeInCoroutine(AudioSource source, float duration)
        {
            float targetVolume = 1f;

            if (!source.isPlaying)
            {
                source.volume = 0f;
                source.Play();
            }

            float startVolume = source.volume;
            float timer = 0f;

            while(timer < duration)
            {
                timer += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, targetVolume, timer / duration);
                yield return null;
            }
            source.volume = targetVolume;
        }

        private static IEnumerator CrossfadeCoroutine(AudioSource fadeOutSource, AudioSource fadeInSource, float duration)
        {
            if (fadeInSource != null && !fadeInSource.isPlaying)
            {
                fadeInSource.volume = 0f;
                fadeInSource.Play();
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;


                if (fadeOutSource != null)
                {
                    fadeOutSource.volume = Mathf.Lerp(1f, 0f, t);
                }
                if (fadeInSource != null)
                {
                    fadeInSource.volume = Mathf.Lerp(0f, 1f, t);
                }

                yield return null;
            }

            if (fadeOutSource != null)
            {
                fadeOutSource.Stop();
                fadeOutSource.volume = 1f;
            }
            if (fadeInSource != null)
            {
                fadeInSource.volume = 1f;
            }
        }

        private class AudioFaderRunner : MonoBehaviour { }

    }
}
