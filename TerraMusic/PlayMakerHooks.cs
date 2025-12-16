using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;

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
                    CustomAudio.staticLogger?.LogInfo(
                        $"Boss {bossName} 状态变化：{__instance.boolVariable.Name} = {__instance.boolValue.Value}");
                }
                //+++++++++++++++

                BossAudioManager.OnVariableChanged(
                    bossName,
                    __instance.boolVariable.Name,
                    __instance.boolValue.Value
                    );

                //如果进入Pause 3，停止所有自制音频
                if (__instance.boolVariable.Name == "Pause 3" && __instance.boolVariable.Value == true)
                { 
                    BossAudioManager.StopAllCustomAudio(1.0f); 
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SetFloatValue), "OnEnter")]
        private static void SetFloatValue_OnEnter_Postfix(SetFloatValue __instance)
        {
            string bossName = DetectBossName(__instance.Fsm);
            if (!string.IsNullOrEmpty(bossName))
            {
                BossAudioManager.OnVariableChanged(
                    bossName,
                    __instance.floatVariable.Name,
                    __instance.floatValue.Value
                    );
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SendEventByName), "OnEnter")]
        private static void SendEventByName_OnEnter_Postfix(SendEventByName __instance)
        {
            //当发送STOP事件时淡出音频
            if (__instance.sendEvent.Value == "STOP")
            {
                CustomAudio.staticLogger?.LogInfo("检测到STOP事件，触发音频淡出");
                BossAudioManager.StopAllCustomAudio(0.5f);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "OnNextLevelReady", new Type[] {})]
        private static void OnNextLevelReady_Postfix()
        {
            //场景切换时停止所有自制音频
            BossAudioManager.StopAllCustomAudio(0.5f);
            BossAudioManager.CleanupInvalidSources();
        }

        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(CompareHPBool), "OnEnter")]
        //private static void CompareHPBool_OnEnter_Postfix(CompareHPBool __instance)
        //{
        //    if (__instance.lessThanBool.Name == "Under HP Check" &&
        //        __instance.lessThanBool.Value == true)
        //    {
        //        CustomAudio.staticLogger?.LogInfo("失心蕾丝已触发低血量检测");

        //        //切换到Phase 2音频
        //        BossAudioManager.OnVariableChanged("Lost Lace", "Under HP Check", true);
        //        BossAudioManager.OnVariableChanged("Lost Lace", "Phase 2", true);
        //    }
        //}

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
