using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace SilksongCustomAudio
{
    ///<summary>
    ///游戏音频事件监听器——使自制音频跟随游戏音频系统
    ///</summary>
    public static class GameAudioEventListener
    {
        private static bool isGamePaused = false;
        private static float lastMasterVolume = 1f;
        private static float lastMusicVolume = 1f;
        private static float pauseVolumeMultiplier = 0.3f;//暂停时音量降低到30%

        private static float currentBaseVolume = 1f;//新增：存储当前音量
        public static float CurrentBaseVolume => currentBaseVolume;

        ///<summary>
        ///初始化事件监听
        ///</summary>
        public static void Initialize()
        {
            //CustomAudio.staticLogger?.LogInfo("初始化游戏音频事件监听器");

            //初始获取当前音量设置
            UpdateVolumeFromGameSettings();

            UpdateCustomAudioVolumes();
            //CustomAudio.staticLogger?.LogInfo("初始音量已应用到自制音频");
        }

        //======功能1：角色死亡时停止======
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerDead))]
        private static void HealthManeger_Die_Postfix()
        {
            //CustomAudio.staticLogger?.LogInfo("玩家死亡，立刻停止自制音频");
            BossAudioManager.StopAllCustomAudio(0.1f);
        }

        //======功能2：跟随音乐音量和总音量======
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MenuAudioSlider), "SetMasterLevel")]
        private static void MenuAudioSlider_SetMasterLevel_Postfix(float masterLevel)
        {
            lastMasterVolume = ConvertSliderToPerceivedVolume(masterLevel / 10f);
            //CustomAudio.staticLogger?.LogInfo($"总音量变化：原始= {masterLevel}，转换后= {lastMasterVolume}");
            UpdateCustomAudioVolumes();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MenuAudioSlider), "SetMusicLevel")]
        private static void MenuAudioSlider_SetMusicLevel_Postfix(float musicLevel)
        {
            lastMusicVolume = ConvertSliderToPerceivedVolume(musicLevel / 10f);
            //CustomAudio.staticLogger?.LogInfo($"音乐音量变化：原始= {musicLevel}，转换后= {lastMusicVolume}");
            UpdateCustomAudioVolumes();
        }

        /// <summary>
        /// 将滑动条值转换为感知音量（匹配游戏曲线）
        /// </summary>
        private static float ConvertSliderToPerceivedVolume(float normalizedValue)
        {
            if (normalizedValue < 0f) return 0f;
            if (normalizedValue > 1f) return 1f;

            float dB = Mathf.Lerp(-80f, 0f, Mathf.Sqrt(normalizedValue));
            float linearFromDB = Mathf.Pow(10f, dB / 20f);

            return linearFromDB;
        }

        //=====功能3：暂停游戏时音量变小======
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "PauseGameToggle")]
        private static void GameManager_PauseGameToggle_Postfix()
        {
            bool isNowPaused = IsGameActuallyPaused();

            if (!isNowPaused)
            {
                //游戏刚刚暂停
                //CustomAudio.staticLogger?.LogInfo("游戏暂停，降低自制音频音量");
                SetCustomAudioVolumeMultiplier(pauseVolumeMultiplier);
            }
            else if (isNowPaused)
            {
                //CustomAudio.staticLogger?.LogInfo("游戏恢复，恢复自制音频音量");
                SetCustomAudioVolumeMultiplier(1f);
            }
        }
        //可靠地检测游戏是否暂停
        private static bool IsGameActuallyPaused()
        {
            try
            {
                var gameManager = UnityEngine.Object.FindObjectOfType<GameManager>();
                if (gameManager != null)
                {
                    var pausedField = typeof(GameManager).GetField("isPaused",
                        BindingFlags.Instance | BindingFlags.Public);
                    if (pausedField != null)
                    {
                        return (bool)pausedField.GetValue(gameManager);
                    }
                    else CustomAudio.staticLogger?.LogWarning("找不到isPaused字段");
                }
            }
            catch (Exception e)
            {
                CustomAudio.staticLogger?.LogWarning($"检测暂停状态失败：{e.Message}");
            }
            return Time.timeScale == 0f;
        }

        //=========辅助方法==========

        ///<summary>
        ///从游戏设置获取当前音量
        ///</summary>
        private static void UpdateVolumeFromGameSettings()
        {
            try
            {
                object gameSettings = GetGameSettings();
                if (gameSettings != null)
                {
                    var masterField = gameSettings.GetType().GetField("masterVolume",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    var musicField = gameSettings.GetType().GetField("musicVolume",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (masterField != null)
                    {
                        lastMasterVolume = ConvertSliderToPerceivedVolume((float)masterField.GetValue(gameSettings) / 10f);
                    }
                    if (musicField != null)
                    {
                        lastMusicVolume = ConvertSliderToPerceivedVolume((float)musicField.GetValue(gameSettings) / 10f);
                    }

                    CustomAudio.staticLogger?.LogInfo($"获取游戏音量设置：" +
                        $"总音量= {lastMasterVolume}，音乐音量= {lastMusicVolume}");
                }
            }
            catch (Exception ex)
            {
                CustomAudio.staticLogger?.LogWarning($"无法获取音量设置：{ex.Message}");

                lastMasterVolume = 1f;
                lastMusicVolume = 1f;

                CustomAudio.staticLogger?.LogWarning($"使用默认音量：总音量= {lastMasterVolume}，音乐音量= {lastMusicVolume}");
            }
        }

        ///<summary>
        ///更新自制音频音量
        ///</summary>
        private static void UpdateCustomAudioVolumes()
        {
            currentBaseVolume = lastMasterVolume * lastMusicVolume;
            //CustomAudio.staticLogger?.LogInfo($"更新存储的音量：{currentBaseVolume}");

            BossAudioManager.SetAllCustomAudioBaseVolume(currentBaseVolume);

            //如果游戏暂停，应用暂停音量乘数
            if (isGamePaused)
            {
                SetCustomAudioVolumeMultiplier(pauseVolumeMultiplier);
            }
        }

        ///<summary>
        ///设置自制音频音量乘数（用于暂停时降低音量）
        ///</summary>
        private static void SetCustomAudioVolumeMultiplier(float multiplier)
        {
            BossAudioManager.SetAllCustomAudioVolumeMultiplier(multiplier);
        }

        ///<summary>
        ///获取GameSettings实例
        ///</summary>
        private static object GetGameSettings()
        {
            try
            {
                //方法1：查找GameSettings单例
                var gameSettingsType = AccessTools.TypeByName("GameSettings");
                if (gameSettingsType != null)
                {
                    var instanceProp = gameSettingsType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    if (instanceProp != null)
                    {
                        return instanceProp.GetValue(null);
                    }

                    //方法2：通过反射查找实例
                    var instances = Resources.FindObjectsOfTypeAll(gameSettingsType);
                    if (instances != null && instances.Length > 0)
                    {
                        return instances[0];
                    }
                }
            }
            catch (Exception ex)
            {
                CustomAudio.staticLogger?.LogWarning($"获取GameSettings失败：{ex.Message}");
            }

            return null;
        }
    }
}
