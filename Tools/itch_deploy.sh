#!/usr/bin/env bash
# itch.io 배포: WebGL(html5) 빌드 → butler 업로드
#
#   bash Tools/itch_deploy.sh test      # 팀 테스트 빌드(is_dev=1) → 채널 html5-test
#   bash Tools/itch_deploy.sh release   # 출시 빌드(is_dev=0)      → 채널 html5
#   bash Tools/itch_deploy.sh test --skip-build   # 이미 만든 Builds/Web_Test를 올리기만
#
# 전제
# - Unity 에디터가 이 프로젝트를 열고 있지 않을 것(배치모드는 같은 프로젝트를 동시에 못 연다)
# - butler 로그인 완료(`butler login`, 키는 ~/.config/itch/butler_creds)
# - 버전은 ProjectSettings의 bundleVersion을 쓴다(테스트는 뒤에 -test)
# - 첫 업로드 뒤 itch 편집 페이지에서 Kind=HTML, 업로드의 "played in the browser" 체크가 필요하다
set -euo pipefail

MODE="${1:-}"
SKIP_BUILD="${2:-}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/c/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe}"
BUTLER="${BUTLER:-/c/Users/user/butler/butler.exe}"
TARGET="mullet9711/dragon-was-here"

case "$MODE" in
  test)    METHOD=BuildTestBatch;    OUT=Web_Test;    CHANNEL=html5-test; SUFFIX=-test ;;
  release) METHOD=BuildReleaseBatch; OUT=Web_Release; CHANNEL=html5;      SUFFIX= ;;
  *) echo "사용법: $0 test|release [--skip-build]"; exit 1 ;;
esac

VERSION="$(grep -m1 'bundleVersion:' "$ROOT/ProjectSettings/ProjectSettings.asset" | awk '{print $2}')"
LOG="$ROOT/Logs/itch_build_$MODE.log"

if [ "$SKIP_BUILD" != "--skip-build" ]; then
  echo "[1/2] WebGL $MODE 빌드 (v$VERSION) … 로그: $LOG"
  "$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" -buildTarget WebGL \
    -executeMethod "DWH.Analytics.Editor.DwhWebBuild.$METHOD" -logFile "$LOG" \
    || { echo "빌드 실패. 로그의 [DWH Build]·error 줄을 확인하세요."; grep -n "\[DWH Build\]\|error CS" "$LOG" | tail -10; exit 1; }
fi

[ -f "$ROOT/Builds/$OUT/index.html" ] || { echo "Builds/$OUT/index.html이 없습니다."; exit 1; }

echo "[2/2] butler push → $TARGET:$CHANNEL ($VERSION$SUFFIX)"
"$BUTLER" push "$ROOT/Builds/$OUT" "$TARGET:$CHANNEL" --userversion "$VERSION$SUFFIX"
"$BUTLER" status "$TARGET:$CHANNEL"
