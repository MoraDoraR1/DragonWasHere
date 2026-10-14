using System.Collections.Generic;

namespace DWH.Data
{
    /// <summary>
    /// 화면 문구(버튼, 안내, 결과 화면 …). string 테이블에 같은 키가 있으면 테이블을 쓰고, 없으면 아래 한/영 기본값을 쓴다.
    /// string 테이블에는 아직 UI 문구 키가 없다 → 이 목록을 엑셀 string 시트에 추가하면 코드 수정 없이 테이블 값으로 바뀐다.
    /// 문구 안의 {0}, {1}은 string.Format 자리다.
    /// </summary>
    public static class UiText
    {
        /// <summary>테이블 조회에 쓰는 데이터베이스. GameManager가 넣는다(없으면 기본값만 쓴다)</summary>
        public static GameDatabase Database;

        static readonly Dictionary<string, (string ko, string en)> Defaults = new()
        {
            // 공통
            ["ui_gold"] = ("골드", "Gold"),
            ["ui_rebirth_point"] = ("환생 포인트", "Rebirth Points"),
            ["ui_level"] = ("Lv {0}", "Lv {0}"),
            ["ui_max"] = ("MAX", "MAX"),
            ["ui_locked"] = ("잠김", "Locked"),
            ["ui_confirm"] = ("확인", "OK"),
            ["ui_cancel"] = ("취소", "Cancel"),
            ["ui_close"] = ("닫기", "Close"),
            ["ui_language"] = ("한국어 / English", "한국어 / English"),
            // 스타터
            ["ui_starter_start"] = ("Game Start!", "Game Start!"),
            ["ui_starter_aim"] = ("클릭해서 낙하 지점을 정하세요!", "Click to choose where to drop!"),
            ["ui_starter_gauge"] = ("클릭해서 바를 멈추세요!   <color=#9fd4ac>가운데 초록 Great</color>  ·  <color=#efc15a>양옆 금색 Good</color>",
                "Click to stop the bar!   <color=#9fd4ac>Green center: Great</color>  ·  <color=#efc15a>Gold sides: Good</color>"),
            // HUD
            ["ui_hud_remaining"] = ("남은 건물 {0}", "Buildings left {0}"),
            // UI 기획서 10쪽: 보너스 스테이지에서는 클리어 목표 자리에 남은 수 대신 이 문구
            ["ui_hud_bonus_goal"] = ("최대한 많이!!", "As many as you can!!"),
            ["ui_hud_fatigue"] = ("피로 ×{0} · 초당 기력 -{1}", "Fatigue ×{0} · Stamina -{1}/s"),
            ["ui_hud_bonus"] = ("보너스 스테이지!", "Bonus Stage!"),
            ["ui_hud_bonus_hint"] = ("황금 건물을 모두 부수세요! 기력이 2배로 줄어요", "Smash every golden building! Stamina drains twice as fast"),
            ["ui_hud_boss"] = ("보스", "BOSS"),
            ["ui_hud_combo"] = ("{0} COMBO!!", "{0} COMBO!!"),
            ["ui_hud_tip_move"] = ("마우스를 움직이면 용이 따라가요. 가만히 두면 알아서 가장 가까운 건물을 부숴요",
                "Move the mouse to guide the dragon. Leave it still and it smashes the nearest building"),
            ["ui_hud_tip_active"] = ("1·2·3 키로 액티브 스킬", "Keys 1·2·3: active skills"),
            ["ui_hud_air_down"] = ("{0} 격추! +{1}", "{0} down! +{1}"),
            ["ui_hud_landmark"] = ("랜드마크 파괴!", "Landmark down!"),
            ["ui_hud_golden"] = ("황금 건물!", "Golden building!"),
            ["ui_hud_mode_guided"] = ("마우스 유도", "Guided"),
            ["ui_hud_mode_auto"] = ("자동 이동", "Auto"),
            ["ui_hud_clear"] = ("다녀가용!", "Dragon Was Here!"),
            ["ui_hud_clear_sub"] = ("마을을 다녀갔어요", "Another town visited"),
            ["ui_hud_clear_bonus_sub"] = ("황금 마을을 다 털었어요", "Golden town looted"),
            ["ui_hud_clear_stamina"] = ("승전의 포효! 기력 +{0}", "Roar of Victory! Stamina +{0}"),
            ["ui_hud_dead_sub"] = ("체력이 바닥났어요", "Out of health"),
            ["ui_hud_exhausted_sub"] = ("용이 지쳐 버렸어요", "The dragon is worn out"),
            ["ui_hud_growth"] = ("{0}(으)로 성장!", "Grew into {0}!"),
            // 결과
            ["ui_result_clear"] = ("스테이지 클리어!", "Stage Clear!"),
            ["ui_result_defeated"] = ("쓰러졌다…", "Defeated…"),
            ["ui_result_exhausted"] = ("기력이 다했다…", "Out of stamina…"),
            ["ui_result_retired"] = ("세션 종료", "Run ended"),
            ["ui_result_retired_title"] = ("잠깐 둥지로 돌아왔어요", "Back to the nest for a break"),
            ["ui_result_gold"] = ("획득 골드", "Gold earned"),
            ["ui_result_destroyed"] = ("파괴", "Destroyed"),
            ["ui_result_bonus"] = ("보너스 스테이지", "Bonus stage"),
            ["ui_result_bonus_none"] = ("진입 못함", "Not reached"),
            ["ui_result_best"] = ("최고 스테이지", "Best stage"),
            ["ui_result_max_combo"] = ("최대 콤보", "Max combo"),
            ["ui_result_clear_bonus"] = ("보물 산더미", "Treasure Hoard"),
            ["ui_result_upgrades"] = ("지금 살 수 있는 업그레이드 {0}개", "{0} upgrades available now"),
            ["ui_result_to_base"] = ("둥지로", "To Lair"),
            ["ui_result_retry"] = ("바로 다시 출격", "Fly Again"),
            ["ui_result_ending"] = ("모든 시대를 정복했다!", "Every era conquered!"),
            // 아웃게임
            ["ui_depart"] = ("출격", "Depart"),
            ["ui_next_stage"] = ("다음 도전", "Next"),
            ["ui_tab_upgrade"] = ("업그레이드", "Upgrades"),
            ["ui_tab_rebirth"] = ("환생", "Rebirth"),
            ["ui_buy"] = ("구매", "Buy"),
            ["ui_buy_x10"] = ("×10", "×10"),
            ["ui_buy_max"] = ("최대", "Max"),
            ["ui_now"] = ("지금", "Now"),
            ["ui_next"] = ("다음", "Next"),
            ["ui_need_prereq"] = ("선행 노드를 먼저 배워야 해요", "Learn the previous node first"),
            ["ui_growth_next"] = ("다음 성장까지 {0}", "{0} to next growth"),
            ["ui_growth_max"] = ("최종 성장", "Fully grown"),
            ["ui_stats"] = ("현재 능력치", "Current Stats"),
            ["ui_stat_attack"] = ("공격력", "Attack"),
            ["ui_stat_hp"] = ("체력", "Health"),
            ["ui_stat_stamina"] = ("기력", "Stamina"),
            ["ui_stat_speed"] = ("이동 속도", "Speed"),
            ["ui_stat_crit"] = ("치명타", "Crit"),
            ["ui_stat_crit_damage"] = ("치명 피해", "Crit Dmg"),
            ["ui_stat_golden"] = ("황금화", "Golden"),
            ["ui_tree_hint"] = ("드래그로 이동 · 휠로 확대 · 선택한 노드를 한 번 더 누르면 구매", "Drag to pan · Wheel to zoom · Click a selected node again to buy"),
            ["ui_branch_1"] = ("공통", "Core"),
            ["ui_branch_2"] = ("체력·기력", "Vitality"),
            ["ui_branch_3"] = ("골드 수급", "Gold"),
            ["ui_branch_4"] = ("캐릭터 스탯", "Stats"),
            ["ui_branch_5"] = ("패시브 스킬", "Passive Skills"),
            ["ui_pause_hint"] = ("용이 잠깐 숨을 고르는 중이에요 · Esc / P로 계속", "The dragon is catching its breath · Esc / P to resume"),
            // 환생
            ["ui_rebirth_title"] = ("환생", "Rebirth"),
            ["ui_rebirth_gain"] = ("지금 환생하면 +{0} P", "Rebirth now: +{0} P"),
            ["ui_rebirth_locked"] = ("{0}에 도달하면 환생할 수 있어요", "Reach {0} to unlock rebirth"),
            ["ui_rebirth_desc"] = ("진행을 처음(1-1)부터 다시 시작하고 환생 포인트를 받아요.\n<b>초기화</b>: 업그레이드, 골드, 진행, 성장 단계\n<b>유지</b>: 환생 포인트, 환생 특성, 액티브 스킬",
                "Restart from 1-1 and earn Rebirth Points.\n<b>Reset</b>: upgrades, gold, progress, growth\n<b>Kept</b>: points, traits, active skills"),
            ["ui_rebirth_button"] = ("환생하기", "Rebirth"),
            ["ui_rebirth_confirm"] = ("정말 환생할까요? 업그레이드·골드·진행이 초기화되고 1-1부터 다시 시작해요",
                "Really rebirth? Upgrades, gold and progress reset and you restart from 1-1"),
            ["ui_rebirth_confirm_button"] = ("한 번 더 누르면 환생", "Click again to rebirth"),
            ["ui_rebirth_traits"] = ("환생 특성", "Rebirth Traits"),
            ["ui_rebirth_actives"] = ("액티브 스킬", "Active Skills"),
            ["ui_active_need"] = ("필요: {0}", "Requires: {0}"),
            ["ui_active_learn"] = ("습득", "Learn"),
            ["ui_active_uses"] = ("출격당 {0}회", "{0} per run"),
            ["ui_rebirth_count"] = ("환생 {0}회", "Rebirths: {0}"),
            // 일시정지·설정
            // 튜토리얼 (튜토리얼 기획서 Ver02, 문구는 기획서 화면 그대로)
            ["ui_tutorial_next"] = ("다음", "Next"),
            ["ui_tutorial_skip"] = ("SKIP", "SKIP"),
            ["ui_tutorial_close"] = ("닫기 X", "Close X"),
            ["ui_tutorial_starter_1"] = ("드래곤은 클릭하기 전까지 좌우로 이동합니다.\n클릭해서 낙하지점을 정해주세요!",
                "The dragon flies left and right until you click.\nClick to choose where it drops!"),
            ["ui_tutorial_starter_2"] = ("게이지에 맞춰 클릭해 드래곤이 더 강하게\n낙하하도록 도와주세요!",
                "Click in time with the gauge\nto make the dragon slam down harder!"),
            ["ui_tutorial_starter_3"] = ("낙하 게이지를 확정하면 용이 강하게 내리찍으며\n맞춘 부분에 따라 주변에 데미지를 줍니다.",
                "Once you lock the gauge, the dragon slams down\nand damages nearby buildings based on your timing."),
            ["ui_tutorial_ingame_2"] = ("플레이어는 마우스 방향으로 용을 유도할 수 있습니다.",
                "You can guide the dragon with your mouse."),
            ["ui_tutorial_ingame_1"] = ("게임이 시작되면 용이 마우스 방향으로 이동하며 건물을 공격합니다.",
                "Once the game starts, the dragon moves toward your mouse and attacks buildings."),
            ["ui_tutorial_ingame_2b"] = ("아래 영역에 마우스를 놓을 때는 유도되지 않습니다.",
                "The dragon isn't guided while your cursor is in the area below."),
            ["ui_tutorial_ingame_3"] = ("하단 영역에서는 마우스 유도 버튼을 클릭해 용을 자동 진행으로 변경할 수 있습니다.\n스페이스바로 변경할 수도 있습니다.",
                "Click the Guided button at the bottom to switch the dragon to Auto.\nYou can also switch with the Space bar."),
            ["ui_tutorial_ingame_3b"] = ("또한 보조스킬을 클릭하여 용을 도와줄 수 있습니다.\n1,2,3 번 숫자키로도 사용할 수 있습니다.",
                "You can also click support skills to help the dragon.\nNumber keys 1, 2, 3 work too."),
            ["ui_tutorial_settle_1"] = ("게임이 종료되면 정산창이 표시됩니다.\n플레이어가 인게임에서 획득한 금은보화들이 골드로 전환되어 표시되며,\n그 밖에도 파괴한 건물, 클리어 스테이지 등을 확인할 수 있습니다.",
                "When a run ends, the results screen appears.\nThe treasure you collected is shown converted to gold,\nalong with buildings destroyed, stages cleared, and more."),
            ["ui_tutorial_upgrade_1"] = ("둥지에서는 획득한 골드로 용의 강화를 진행할 수 있습니다.\n먼저 강화하고 싶은 노드를 선택해줍니다.",
                "In the nest, you can upgrade your dragon with the gold you earned.\nFirst, select the node you want to upgrade."),
            ["ui_tutorial_upgrade_2"] = ("오른쪽의 창에서 해당 노드의 정보를 볼 수 있으며 강화를 진행할 수 있습니다.\n강화를 통해 새 스킬을 얻거나 드래곤의 능력치를 올려보세요.",
                "The panel on the right shows the node's details and lets you upgrade it.\nUpgrade to learn new skills or raise your dragon's stats."),
            ["ui_tutorial_ingame_4"] = ("용이 적에게 공격당하면 HP가 닳으며 시간이 지날수록 기력이 줄어듭니다.\n둘중 하나라도 바닥날 경우 게임이 종료됩니다.",
                "The dragon loses HP when enemies hit it, and stamina drains over time.\nIf either runs out, the run ends."),
            ["ui_tutorial_rebirth_1"] = ("플레이어는 환생 탭에서 환생 포인트를 사용해 드래곤을 영구적으로\n강화하는 '환생 강화'를 진행할 수 있습니다.",
                "In the Rebirth tab, you can spend Rebirth Points on\n'rebirth upgrades' that strengthen your dragon permanently."),
            ["ui_tutorial_rebirth_2"] = ("플레이어는 환생을 통해 드래곤의 성장치만큼 환생 포인트를 획득할 수 있습니다.\n대신 드래곤의 성장치와 스테이지 진행도가 모두 초기화 됩니다.",
                "Rebirth gives you Rebirth Points based on how much your dragon has grown.\nIn exchange, growth and stage progress are reset."),
            ["ui_tutorial_rebirth_3"] = ("해당 창에서 강화 가능하며 액티브 스킬의 해금에는\n추가적인 조건이 필요합니다.",
                "You can upgrade them here.\nUnlocking active skills needs an extra condition."),
            ["ui_pause"] = ("일시정지", "Paused"),
            ["ui_resume"] = ("계속하기", "Resume"),
            ["ui_end_session"] = ("세션 끝내기", "End Run"),
            ["ui_end_session_confirm"] = ("이번 런을 끝내고 정산할까요?\n지금까지 모은 골드는 받아요.", "End this run and settle?\nYou keep the gold collected so far."),
            ["ui_end_session_yes"] = ("끝내기", "End Run"),
            ["ui_end_session_no"] = ("취소", "Cancel"),
            ["ui_give_up"] = ("포기하고 나가기", "Give up"),
            ["ui_settings"] = ("환경설정", "Settings"),
            ["ui_settings_language"] = ("언어", "Language"),
            ["ui_settings_screen"] = ("화면 모드", "Display"),
            ["ui_settings_fullscreen"] = ("전체 화면", "Fullscreen"),
            ["ui_settings_windowed"] = ("창 모드", "Windowed"),
            ["ui_settings_shake"] = ("화면 흔들림", "Screen shake"),
            ["ui_settings_on"] = ("켜기", "On"),
            ["ui_settings_off"] = ("끄기", "Off"),
            ["ui_to_title"] = ("타이틀로 이동", "Back to Title"),
            ["ui_to_title_hint"] = ("타이틀로 가면 이번 출격은 기록되지 않아요", "Leaving now discards this run"),
            // 타이틀
            ["ui_title_name"] = ("다녀가 용!", "Dragon Was Here!"),
            ["ui_title_new"] = ("새 게임", "New Game"),
            ["ui_title_continue"] = ("이어하기", "Continue"),
            ["ui_title_collection"] = ("도감\n<size=75%>(예정)</size>", "Collection\n<size=75%>(Soon)</size>"),
            ["ui_title_minigame"] = ("미니게임\n<size=75%>(예정)</size>", "Mini Game\n<size=75%>(Soon)</size>"),
            ["ui_title_version"] = ("버전 v{0}", "Version v{0}"),
            ["ui_title_warning"] = ("주의!", "Warning!"),
            ["ui_title_reset_body"] = ("저장된 기록을 초기화하고 다시 처음부터 하시겠습니까?", "Erase your saved progress and start over?"),
            ["ui_title_reset_sub"] = ("다시 되돌릴 수 없습니다", "This cannot be undone"),
            ["ui_title_yes"] = ("네!", "Yes!"),
            ["ui_title_no"] = ("아니요!", "No!"),
        };

        public static IEnumerable<string> Keys => Defaults.Keys;

        public static string Get(string key)
        {
            var db = Database;
            if (db != null && db.HasString(key)) return db.Text(key);
            if (!Defaults.TryGetValue(key, out var text)) return key;
            return Localization.Current == Language.En ? text.en : text.ko;
        }

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);
    }
}
