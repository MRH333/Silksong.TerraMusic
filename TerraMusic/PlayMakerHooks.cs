using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
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
                BossAudioManager.OnVariableChanged(
                    bossName,
                    __instance.boolVariable.Name,
                    __instance.boolValue.Value
                    );
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
        [HarmonyPatch(typeof(CompareHPBool), "OnEnter")]
        private static void CompareHPBool_OnEnter_Postfix(CompareHPBool __instance)
        {
            if (__instance.lessThanBool.Name == "Under HP Check" &&
                __instance.lessThanBool.Value == true)
            {
                CustomAudio.staticLogger?.LogInfo("失心蕾丝已触发低血量检测");

                //切换到Phase 2音频
                BossAudioManager.OnVariableChanged("Lost Lace", "Under HP Check", true);
                BossAudioManager.OnVariableChanged("Lost Lace", "Phase 2", true);
            }
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
