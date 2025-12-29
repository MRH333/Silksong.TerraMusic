using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongCustomAudio
{
    public static class PlayMakerHooks
    {
        //+++++不应用boss死亡立刻停止音频的场景+++++
        private static string dualBossSceneName = "Dock_09";
        private static string bellBossSceneName = "Bellway_Centipede_Arena";
        //+++++++++++++++

        //Hook设置布尔值SetBoolValue行为
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

        //Hook检查多个布尔值是否符合预期BoolTestMulti行为
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BoolTestMulti), nameof(BoolTestMulti.OnEnter))]
        private static void BoolTestMulti_OnEnter_Postfix(BoolTestMulti __instance)
        {
            string bossName = DetectBossName(__instance.Fsm);

            if (!string.IsNullOrEmpty(bossName))
            {
                bool allConditionsMet = true;

                for (int i = 0; i < __instance.boolVariables.Length; i++)
                {
                    if (__instance.boolVariables[i]?.Value != __instance.boolStates[i]?.Value)
                    {
                        allConditionsMet = false;
                        break;
                    }
                }

                if (allConditionsMet && !string.IsNullOrEmpty(__instance.trueEvent.Name))
                {
                    //CustomAudio.staticLogger?.LogInfo($"Boss {bossName} BoolTestMulti 触发事件：{__instance.trueEvent.Name}");

                    BossAudioManager.OnVariableChanged(
                        bossName,
                        __instance.trueEvent.Name,
                        true
                        );
                }
            }
        }

        //Hook检查多个布尔值是否为true BoolAllTrue行为
        //控制监工兄弟音乐停止
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BoolAllTrue), nameof(BoolAllTrue.OnEnter))]
        private static void BoolAllTrue_OnEnter_Postfix(BoolAllTrue __instance)
        {
            //检查当前场景
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != dualBossSceneName) return;

            //检查是否是控制音乐停止的FSM
            string fsmName = __instance.Fsm.Name;
            if (fsmName != "Music End") return;

            //检查两个Boss死亡状态
            bool allDefeated = true;
            foreach (var variable in __instance.boolVariables)
            {
                if (variable?.Value != true)
                {
                    allDefeated = false;
                    break;
                }
            }

            if (allDefeated)
            {
                //CustomAudio.staticLogger?.LogInfo("双人Boss都已击败，音乐END事件触发，停止自制音频");

                BossAudioManager.StopAllCustomAudio(0.5f);
            }
        }
        //控制BellEater音频切换
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BoolAllTrue), nameof(BoolAllTrue.OnUpdate))]
        private static void BoolAllTrue_OnUpdate_Postfix(BoolAllTrue __instance)
        {

            string bossName = DetectBossName(__instance.Fsm);
            if (bossName != "Bell Eater") return;

            //检查两个条件是否都为真
            bool allTrue = true;

            foreach (var variable in __instance.boolVariables)
            {
                if (variable?.Value != true)
                {
                    allTrue = false;
                    break;
                }
            }
            if (allTrue)
            {
                BossAudioManager.OnVariableChanged(
                    bossName,
                    __instance.sendEvent.Name,
                    true
                    );
            }
        }

        //Hook从全局对象池中放置对象SpawnObjectFromGlobalPool行为
        //控制BellEater音频停止
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SpawnObjectFromGlobalPool), "OnEnter")]
        private static void PlayAudioEvent_OnEnter_Postfix(SpawnObjectFromGlobalPool __instance)
        {
            string bossName = DetectBossName(__instance.Fsm);
            if (bossName != "Bell Eater") return;

            //检查播放的音频事件名称
            GameObject prefab = __instance.gameObject?.Value;
            if (prefab != null)
            {
                string prefabName = prefab.name;

                if (prefabName == "Boss Death FinalHit")
                {
                    //CustomAudio.staticLogger?.LogInfo("检测到Bell Eater死亡特效生成，停止自制音频");
                    BossAudioManager.StopAllCustomAudio(0f);
                }
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

        //Hook发送事件SendEventByName行为
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SendEventByName), "OnEnter")]
        private static void SendEventByName_OnEnter_Postfix(SendEventByName __instance)
        {
            //当选定boss的FSM发送STOP事件时淡出音频
            string bossName = DetectBossName(__instance.Fsm);
            if (!string.IsNullOrEmpty(bossName))
            {
                if (__instance.sendEvent.Value == "STOP")
                {
                    //CustomAudio.staticLogger?.LogInfo("检测到STOP事件，触发音频淡出");
                    BossAudioManager.StopAllCustomAudio(0.5f);

                    //当选定boss是Lost Lace时有特殊逻辑
                    if (bossName == "Lost Lace")
                    {
                        //CustomAudio.staticLogger?.LogInfo($"检测到Lost Lace的STOP，提前播放Phase 3音频");

                        //延迟一小会确保Phase 2音频完全停止
                        var gameManager = UnityEngine.Object.FindObjectOfType<GameManager>();
                        if (gameManager != null)
                        {
                            gameManager.StartCoroutine(DelayedPhase3Audio(0.2f, bossName));
                        }
                    }
                }

                if(__instance.sendEvent.Value == "CORE DAMAGE READY")
                {
                    //CustomAudio.staticLogger?.LogInfo("检测到炽焰核心事件，触发亵渎天神音频播放");
                    BossAudioManager.OnVariableChanged(
                        bossName,
                        __instance.sendEvent.Value,
                        true
                        );
                }

                if (__instance.sendEvent.Value == "FINAL BREAK")
                {
                    //CustomAudio.staticLogger?.LogInfo("检测到炽焰之父死亡，音频停止");
                    BossAudioManager.StopAllCustomAudio(0.5f);
                }
            }
        }
        private static IEnumerator DelayedPhase3Audio(float delay, string bossName)
        {
            yield return new WaitForSeconds(delay);
            BossAudioManager.PlayCustomAudio("CalamitasPhase3", 0.5f);
        }

        //Hook场景切换停止音频：GameManager的OnNextLevelReady方法
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "OnNextLevelReady", new Type[] { })]
        private static void OnNextLevelReady_Postfix()
        {
            //场景切换时停止所有自制音频（不清理流式加载内存）
            BossAudioManager.StopAllCustomAudio(0.5f);
            BossAudioManager.CleanupInvalidSources();

            //切换场景时重置
            //CustomAudio.staticLogger?.LogInfo("场景已切换，重置状态");
            BossAudioManager.ResetAllBossStates();
        }

        //Hook Boss死亡停止音频：HealthManager的Die方法
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die),
            new Type[] { typeof(float?), typeof(AttackTypes), typeof(NailElements), typeof(GameObject), typeof(bool), typeof(float), typeof(bool), typeof(bool) })]
        private static void HealthManager_Die_Postfix(HealthManager __instance)
        {
            string sceneName = SceneManager.GetActiveScene().name;

            //如果不是双Boss场景则停止所有自制音频
            if (sceneName != dualBossSceneName && sceneName != bellBossSceneName)
            {
                //CustomAudio.staticLogger?.LogInfo("Boss被击败，停止自制音频");
                BossAudioManager.StopAllCustomAudio(0.5f);
                return;
            }

            //双Boss场景特殊处理
            //CustomAudio.staticLogger?.LogInfo($"{__instance.gameObject.name}被击败，但不停止音频");

        }

        private static string DetectBossName(Fsm fsm)
        {
            if (fsm == null)
                return null;

            string gameObjectName = fsm.GameObjectName;

            if (gameObjectName.Contains("Lost Lace"))
                return "Lost Lace";

            if (gameObjectName.Contains("Dock Guard Slasher"))
                return "Dock Guard";

            if (gameObjectName.Contains("Centipede Control"))
                return "Bell Eater";

            if(gameObjectName.Contains("Wisp Pyre Effigy"))
                return "Fire Father";

            //...待添加

            return null;
        }
    }
}
