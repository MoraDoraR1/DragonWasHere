// 브라우저 localStorage 읽기·쓰기 (진행 저장 DragonWasHere.DWH.Save, 지표 값 DragonWasHere.dwh.*).
// Unity WebGL의 PlayerPrefs는 IndexedDB 안에서 "페이지 주소" 별로 따로 저장된다. itch.io는 업로드마다 주소가
// (html-classic.itch.zone/html/<업로드 번호>/) 바뀌어 새 빌드에서 진행이 사라진다. localStorage는 출처(도메인) 단위라
// 주소가 바뀌어도 그대로 남는다. 막혀 있으면(사파리 개인정보 모드 등) 실패를 돌려주고 게임은 PlayerPrefs로 대신한다.
mergeInto(LibraryManager.library, {
  DwhStorage_Get: function (keyPtr) {
    try {
      var value = window.localStorage.getItem(UTF8ToString(keyPtr));
      if (value === null) return null;
      var size = lengthBytesUTF8(value) + 1;
      var buffer = _malloc(size);
      stringToUTF8(value, buffer, size);
      return buffer;
    } catch (e) {
      return null;
    }
  },

  DwhStorage_Set: function (keyPtr, valuePtr) {
    try {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr));
      return 1;
    } catch (e) {
      return 0;
    }
  },

  DwhStorage_Remove: function (keyPtr) {
    try {
      window.localStorage.removeItem(UTF8ToString(keyPtr));
    } catch (e) {}
  }
});
