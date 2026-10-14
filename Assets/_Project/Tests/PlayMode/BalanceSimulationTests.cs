#if UNITY_EDITOR
using System.Collections;
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
    /// 밸런스 봇 시뮬레이션 (가이드 9장). 실제 전투를 자동 모드로 돌리며 처음부터 엔딩까지 진행한다.
    /// - 정산 후 살 수 있는 노드 중 가장 싼 것을 골드가 떨어질 때까지 산다(그리디).
    /// - 환생이 해금된 벽 스테이지에서 3판 연속 실패하면 환생하고, 포인트로 특성을 가장 싼 것부터 산다.
    /// - 액티브 스킬은 습득만 하고 전투 2초에 모두 쓴다. 스타터는 끈다.
    /// 결과 표는 Logs/balance_sim.md. 오래 걸려서 [Explicit]이다.
    /// </summary>
    public class BalanceSimulationTests
    {
        const int FrameRate = 60;
        const int MaxRuns = 120;
        const int FailsBeforeRebirth = 3;

        GameDatabase db;
        double atkScale = 1;
        GameManager manager;
        Camera camera;

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            // 실험용: 환경 변수 DWH_SIM_ATK_SCALE로 건물·공중 유닛 공격력을 배율 조정한 복사본으로 돌린다 (데이터 파일은 그대로)
            string scaleText = System.Environment.GetEnvironmentVariable("DWH_SIM_ATK_SCALE");
            if (double.TryParse(scaleText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double scale) && scale > 0)
            {
                db = Object.Instantiate(db);
                db.Invalidate();
                // DWH_SIM_LM_SCALE가 있으면 랜드마크(유형 3)만 따로 배율
                double lm = double.TryParse(System.Environment.GetEnvironmentVariable("DWH_SIM_LM_SCALE"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double l) && l > 0 ? l : scale;
                // DWH_SIM_LM_HP_SCALE가 있으면 랜드마크 체력도 배율
                double lmHp = double.TryParse(System.Environment.GetEnvironmentVariable("DWH_SIM_LM_HP_SCALE"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double h) && h > 0 ? h : 1;
                // DWH_SIM_ATKB_HP_SCALE가 있으면 공격 건물 체력도 배율
                double atkHp = double.TryParse(System.Environment.GetEnvironmentVariable("DWH_SIM_ATKB_HP_SCALE"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double ah) && ah > 0 ? ah : 1;
                foreach (var b in db.buildings)
                {
                    b.atk *= b.buildingType == BuildingType.Landmark ? lm : scale;
                    if (b.buildingType == BuildingType.Landmark) b.hp *= lmHp;
                    if (b.buildingType == BuildingType.Attack) b.hp *= atkHp;
                }
                foreach (var u in db.units) u.atk *= scale;
                atkScale = scale;
            }
            ApplyStageOverrides();
            camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            var go = new GameObject("GameManager");
            go.SetActive(false);
            manager = go.AddComponent<GameManager>();
            manager.database = db;
            manager.art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            manager.uiTheme = AssetDatabase.LoadAssetAtPath<DWH.UI.UiTheme>("Assets/_Project/Data/UiTheme.asset");
            manager.enableStarter = false;
            go.SetActive(true);
            manager.Initialize(new MemorySaveStorage(), _ => new AutoOnlyInput());
            Time.captureFramerate = FrameRate;
        }

        /// <summary>
        /// 실험용 스테이지별 배율: 환경 변수 DWH_SIM_OVERRIDES = 파일 경로. 한 줄에 "stage_id 항목 배율" (예: stg202 nhp 0.6).
        /// 항목: nhp(일반·황금 건물 체력) ahp/aatk(공격 건물) lhp/latk(랜드마크) uhp/uatk(공중 유닛). 데이터 파일은 그대로, 메모리 복사본만 바꾼다
        /// </summary>
        void ApplyStageOverrides()
        {
            string path = System.Environment.GetEnvironmentVariable("DWH_SIM_OVERRIDES");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (atkScale == 1) { db = Object.Instantiate(db); db.Invalidate(); }
            overrideName = Path.GetFileNameWithoutExtension(path);
            foreach (var raw in File.ReadAllLines(path))
            {
                var p = raw.Split('#')[0].Split(new[] { ' ', '	' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 3) continue;
                double k = double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
                foreach (var b in db.buildings.Where(b => b.stageId == p[0]))
                {
                    bool attack = b.buildingType == BuildingType.Attack, landmark = b.buildingType == BuildingType.Landmark;
                    switch (p[1])
                    {
                        case "nhp": if (!attack && !landmark) b.hp *= k; break;
                        case "ahp": if (attack) b.hp *= k; break;
                        case "aatk": if (attack) b.atk *= k; break;
                        case "lhp": if (landmark) b.hp *= k; break;
                        case "latk": if (landmark) b.atk *= k; break;
                    }
                }
                foreach (var u in db.units.Where(u => u.stageId == p[0]))
                {
                    if (p[1] == "uhp") u.hp *= k;
                    if (p[1] == "uatk") u.atk *= k;
                }
            }
        }

        string overrideName;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (manager.Battle != null) Object.Destroy(manager.Battle.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            Object.Destroy(camera.gameObject);
            Object.Destroy(manager.gameObject);
        }

        void BuyGreedy()
        {
            var tree = manager.Upgrades;
            while (true)
            {
                var node = db.upgrades.Where(n => tree.Status(n) == NodeStatus.Affordable).OrderBy(tree.Cost).FirstOrDefault();
                if (node == null || manager.BuyUpgrade(node, 1) == 0) return;
            }
        }

        void SpendPoints()
        {
            var save = manager.Save;
            while (true)
            {
                foreach (var a in db.activeSkills) manager.BuyActive(a.Id);
                var trait = db.rebirthTraits.Where(t => !Rebirth.TraitMaxed(t, Rebirth.TraitLevel(save, t)))
                    .OrderBy(t => Rebirth.TraitCost(t, Rebirth.TraitLevel(save, t))).FirstOrDefault();
                if (trait == null || !manager.BuyTrait(trait.traitId)) return;
            }
        }

        [UnityTest, Explicit("밸런스 확인용. 수십 판을 실제로 돌려 오래 걸린다"), Timeout(7200000)]
        public IEnumerator Bot_PlaysToEnding()
        {
            var log = new StringBuilder("| 판 | 회차 | 스테이지 | 결과 | 시간(초) | 파괴 | 체력 | 기력 | 보너스 | 획득 골드 | 보유 골드 | 공격력 | 성장 |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            int failsHere = 0;
            string lastStage = null;
            float totalSeconds = 0;
            int runs = 0;
            for (; runs < MaxRuns && !manager.Save.gameCompleted; runs++)
            {
                var stats = manager.Upgrades.Stats();
                manager.StartStage();
                bool usedActives = false;
                for (int i = 0; i < FrameRate * 400 && manager.State != GameState.Result; i++)
                {
                    yield return null;
                    if (!usedActives && manager.Battle != null && manager.Battle.Dragon.Vitals.Elapsed > 2)
                    {
                        usedActives = true;
                        foreach (var a in db.activeSkills) manager.Battle.Actives.Use(a.Id);
                    }
                }
                Assert.AreEqual(GameState.Result, manager.State, "판이 끝나지 않았다");
                var r = manager.LastResult;
                totalSeconds += r.playSeconds;
                log.AppendLine($"| {runs + 1} | {manager.Save.rebirthCount + 1} | {db.StageLabel(r.stageId)} | {(r.cleared ? "클리어" : r.end == SessionEnd.Defeated ? "체력 0" : "기력 0")} | {r.playSeconds:0} | {r.destroyedBuildings}/{r.totalBuildings} | {r.hpLeft:0} | {r.staminaLeft:0} | {(r.bonusEntered ? $"{r.bonusDestroyed}/{r.bonusTotal}" : "-")} | {r.earnedGold:0} | {manager.Save.goldCoin:0} | {stats.AttackPower:0} | {(int)stats.Growth.growthStage} |");
                manager.ConfirmResult();

                failsHere = r.cleared || r.stageId != lastStage ? (r.cleared ? 0 : 1) : failsHere + 1;
                lastStage = r.stageId;
                if (failsHere >= FailsBeforeRebirth && Rebirth.IsUnlocked(db, manager.Save))
                {
                    double points = manager.PerformRebirth();
                    log.AppendLine($"| | **환생 {manager.Save.rebirthCount}** | {db.StageLabel(r.stageId)}에서 | +{points:0} RP | | | | | | | | | |");
                    SpendPoints();
                    failsHere = 0;
                }
                int doomBefore = manager.Save.GetNodeLevel("upg513");
                BuyGreedy();
                // 종말의 용(재사용 대기시간) 처음 산 때를 남긴다 (10-14 E안 확인용)
                if (doomBefore == 0 && manager.Save.GetNodeLevel("upg513") > 0)
                    log.AppendLine($"| | **종말의 용 구매** | {db.StageLabel(manager.Save.currentStage)} 출발 전 | {manager.Save.rebirthCount + 1}회차 | | | | | | | | | |");
            }
            log.AppendLine($"\n총 {runs}판, 전투 시간 합계 {totalSeconds / 60:0.0}분(전투만), 엔딩 {(manager.Save.gameCompleted ? "도달" : "못 함")}");
            File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", overrideName != null ? $"balance_sim_{overrideName}.md" : atkScale == 1 ? "balance_sim.md" : $"balance_sim_atk{atkScale:0.##}_lm{System.Environment.GetEnvironmentVariable("DWH_SIM_LM_SCALE") ?? "same"}_lmhp{System.Environment.GetEnvironmentVariable("DWH_SIM_LM_HP_SCALE") ?? "1"}_abhp{System.Environment.GetEnvironmentVariable("DWH_SIM_ATKB_HP_SCALE") ?? "1"}.md"), $"적 공격력 배율 {atkScale}\n\n" + log);
            Debug.Log("[DWH] 밸런스 시뮬레이션\n" + log);
        }
    }
}
#endif
