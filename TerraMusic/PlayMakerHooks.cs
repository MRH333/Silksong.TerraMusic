using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections;
using UnityEngine;

namespace SilksongCustomAudio
{
    public static class PlayMakerHooks
    {
        //Hook所有PlayMaker变量设置动作
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SetBoolValue), "OnEnter")]
        private static void SetBoolValue_OnEnter_PostFix(SetBoolValue __instance)
        {
            //检测是否是Boss相关的FSM
            string bossName = DetectBossName(__instance.Fsm);

            if (!string.IsNullOrEmpty(bossName))
            {
                //+++++记录调试信息+++++
                if (__instance.boolVariable.Name == "Pause 2" ||
                    __instance.boolVariable.Name == "Pause 3")
                {
                    CustomAudio.staticLogger?.LogInfo($"Boss {bossName} 状态变化：{__instance.boolVariable.Name} = {__instance.boolValue.Value}");
                }
                //+++++++++++++++

                BossAudioManager.OnVariableChanged(
                    bossName,
                    __instance.boolVariable.Name,
                    __instance.boolValue.Value
                    );
            }
        }

        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(SetFloatValue), "OnEnter")]
        //private static void SetFloatValue_OnEnter_Postfix(SetFloatValue __instance)
        //{
        //    string bossName = DetectBossName(__instance.Fsm);
        //    if (!string.IsNullOrEmpty(bossName))
        //    {
        //        BossAudioManager.OnVariableChanged(
        //            bossName,
        //            __instance.floatVariable.Name,
        //            __instance.floatValue.Value
        //            );
        //    }
        //}

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SendEventByName), "OnEnter")]
        private static void SendEventByName_OnEnter_Postfix(SendEventByName __instance)
        {
            //当选定boss的FSM发送STOP事件时淡出音频
            string bossName = DetectBossName( __instance.Fsm);
            if (!string.IsNullOrEmpty(bossName))
            {
                if (__instance.sendEvent.Value == "STOP")
                {
                    CustomAudio.staticLogger?.LogInfo("检测到STOP事件，触发音频淡出");
                    BossAudioManager.StopAllCustomAudio(0.5f);

                    //当选定boss是Lost Lace时有特殊逻辑
                    if (bossName == "Lost Lace")
                    {
                        CustomAudio.staticLogger?.LogInfo($"检测到Lost Lace的STOP，提前播放Phase 3音频");

                        //延迟一小会确保Phase 2音频完全停止
                        var gameManager = UnityEngine.Object.FindObjectOfType<GameManager>();
                        if (gameManager != null)
                        {
                            gameManager.StartCoroutine(DelayedPhase3Audio(0.2f, bossName));
                        }
                    }
                }
            }
        }
        private static IEnumerator DelayedPhase3Audio(float delay, string bossName)
        {
            yield return new WaitForSeconds(delay);
            BossAudioManager.PlayCustomAudio("CalamitasPhase3", 0.5f);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "OnNextLevelReady", new Type[] {})]
        private static void OnNextLevelReady_Postfix()
        {
            //场景切换时停止所有自制音频（不清理流式加载内存）
            BossAudioManager.StopAllCustomAudio(0.5f);
            BossAudioManager.CleanupInvalidSources();

            //切换场景时重置
            CustomAudio.staticLogger?.LogInfo("场景已切换，重置状态");
            BossAudioManager.ResetAllBossStates();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die),
            new Type[] { typeof(float?), typeof(AttackTypes), typeof(NailElements), typeof(GameObject), typeof(bool), typeof(float), typeof(bool), typeof(bool) })]
        private static void HealthManager_Die_Postfix()
        {
            CustomAudio.staticLogger?.LogInfo("Boss被击败，停止自制音频");
            BossAudioManager.StopAllCustomAudio(0.5f);
        }

        private static string DetectBossName(Fsm fsm)
        {
            if (fsm == null)
                return null;

            string gameObjectName = fsm.GameObjectName;

            if (gameObjectName.Contains("Lost Lace"))
                return "Lost Lace";

            //...待添加

            return null;
        }
    }
}
