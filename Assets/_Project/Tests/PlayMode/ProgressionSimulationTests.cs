#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Dragon;
using DWH.OutGame;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DWH.Tests
{
    /// <summary>
    /// 밸런스 검토용 진행 시뮬레이션 (M7). 봇이 업그레이드를 사고 자동 모드로 스테이지에 계속 도전한다.
    /// 구매 정책은 8.2 가정을 바탕으로 패시브 스킬(M6)을 더했다: 각성 40%, 체력 강화 20%, 기력·보조 노드 20%, 패시브·중량 20%.
    /// 결과는 Logs/balance_progression.csv에 세션별로 남긴다.
    /// 오래 걸리므로 일반 실행에서는 빠지고, 이름으로 지정할 때만 돈다:
    ///   -testPlatform PlayMode -testFilter "DWH.Tests.ProgressionSimulationTests"
    /// </summary>
    [Explicit("밸런스 시뮬레이션: 이름으로 지정해 실행")]
    [Category("Balance")]
    public class ProgressionSimulationTests
    {
        const int FrameRate = 60;
        const int MaxSessions = 60;
        const string StopAfterStage = "3-1";
        static readonly string Report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "balance_progression.csv");

        // 정책: (노드, 전체 사용 골드 대비 목표 비율). 순서대로 목표 비율보다 덜 쓴 노드를 먼저 산다
        static readonly (string node, double share)[] Policy =
        {
            ("DragonAwakening", 0.40),
            ("HpBoost", 0.20),
            ("StaminaBoost", 0.08),
            ("VictoryCry", 0.04),
            ("Devour", 0.04),
            ("Greed", 0.04),
            ("Skill_BodySlam", 0.05),
            ("Skill_FireBreath", 0.05),
            ("Skill_Whirlwind", 0.04),
            ("Skill_FireBall", 0.03),
            ("Weight", 0.03),
        };

        GameDatabase db;
        GameManager manager;

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            var art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            var go = new GameObject("GameManager");
            go.SetActive(false);
            manager = go.AddComponent<GameManager>();
            manager.database = db;
            manager.art = art;
            manager.enableStarter = false;   // 스타터(클릭 입력)는 StageStarterTests에서 따로 검증한다
            go.SetActive(true);
            manager.Initialize(new MemorySaveStorage(), _ => new AutoOnlyInput());
            Time.captureFramerate = FrameRate;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (manager.Battle != null) Object.Destroy(manager.Battle.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.Destroy(cam.gameObject);
            Object.Destroy(manager.gameObject);
        }

        void BuyUpgrades(Dictionary<string, double> spent)
        {
            var tree = manager.Upgrades;
            while (true)
            {
                double total = spent.Values.Sum();
                var candidates = Policy
                    .Select(p => (p.node, p.share, row: tree.Node(p.node)))
                    .Where(p => tree.Status(p.row) == NodeStatus.Affordable)
                    .OrderBy(p => (spent.GetValueOrDefault(p.node) - p.share * total) / p.share)
                    .ToList();
                if (candidates.Count == 0) return;
                var pick = candidates[0];
                double cost = tree.Cost(pick.row);
                if (!manager.BuyUpgrade(pick.row)) return;
                spent[pick.node] = spent.GetValueOrDefault(pick.node) + cost;
            }
        }

        [UnityTest, Timeout(3_000_000)]
        public IEnumerator Bot_ProgressesWithUpgrades()
        {
            var spent = new Dictionary<string, double>();
            var csv = new StringBuilder("session,stage,result,seconds,destroyed,total,bonus_golden,earned_gold,gold_after,attack,max_hp,max_stamina,awakening,hp_boost,stamina_boost,body_slam,fire_breath,whirlwind,fire_ball\n");
            var attemptsPerStage = new Dictionary<string, int>();

            for (int session = 1; session <= MaxSessions; session++)
            {
                BuyUpgrades(spent);
                var stats = manager.Upgrades.Stats();
                string stage = manager.Save.currentStage;
                attemptsPerStage[stage] = attemptsPerStage.GetValueOrDefault(stage) + 1;

                manager.StartStage();
                double seconds = 0;
                while (manager.State != GameState.Result)
                {
                    yield return null;
                    if (manager.Battle != null) seconds = manager.Battle.Dragon.Vitals.Elapsed;
                }

                var r = manager.LastResult;
                var tree = manager.Upgrades;
                csv.AppendLine(string.Join(",", session, stage, r.cleared ? "clear" : "fail", seconds.ToString("0.0"),
                    r.destroyedBuildings, r.totalBuildings, r.bonusEntered ? $"{r.bonusDestroyed}/{r.bonusTotal}" : "-",
                    r.earnedGold.ToString("0"), manager.Save.goldCoin.ToString("0"),
                    stats.AttackPower.ToString("0.0"), stats.MaxHp.ToString("0"), stats.MaxStamina.ToString("0"),
                    tree.Level(tree.Node("DragonAwakening")), tree.Level(tree.Node("HpBoost")), tree.Level(tree.Node("StaminaBoost")),
                    tree.Level(tree.Node("Skill_BodySlam")), tree.Level(tree.Node("Skill_FireBreath")),
                    tree.Level(tree.Node("Skill_Whirlwind")), tree.Level(tree.Node("Skill_FireBall"))));
                manager.ConfirmResult();
                yield return null;

                if (r.cleared && stage == StopAfterStage) break;
            }

            File.WriteAllText(Report, csv.ToString(), new UTF8Encoding(true));
            Debug.Log($"[DWH] 진행 시뮬레이션: 도달 {manager.Save.highestStage}, 스테이지별 도전 횟수 " +
                      string.Join(" ", attemptsPerStage.Select(kv => $"{kv.Key}×{kv.Value}")) + $"\n{csv}");
            Assert.IsNotEmpty(manager.Save.highestStage, "업그레이드를 사면 최소한 1-1은 넘어야 한다");
        }
    }
}
#endif
