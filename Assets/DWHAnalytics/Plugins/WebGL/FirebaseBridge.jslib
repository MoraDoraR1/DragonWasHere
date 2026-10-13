// Unity(C#) → 브라우저 연결. 실제 Firebase 호출은 WebGL 템플릿 index.html의 DwhAnalyticsBridge가 한다.
// 템플릿이 다른 것으로 바뀌어도 게임이 멈추지 않도록, 브리지가 없으면 콘솔에만 남긴다.
mergeInto(LibraryManager.library, {
  DwhFb_Init: function (userIdPtr, propsJsonPtr, isDev) {
    var uid = UTF8ToString(userIdPtr);
    var props = {};
    try { props = JSON.parse(UTF8ToString(propsJsonPtr)); } catch (e) {}
    if (window.DwhAnalyticsBridge) window.DwhAnalyticsBridge.init(uid, props, isDev === 1);
    else console.warn("[DWH] DwhAnalyticsBridge 없음: WebGL 템플릿을 DragonFirebase로 설정하세요.");
  },

  DwhFb_LogEvent: function (namePtr, paramsJsonPtr) {
    var name = UTF8ToString(namePtr);
    var params = {};
    try { params = JSON.parse(UTF8ToString(paramsJsonPtr)); } catch (e) {}
    if (window.DwhAnalyticsBridge) window.DwhAnalyticsBridge.log(name, params);
    else console.log("[DWH]", name, params);
  },

  DwhFb_SetUserProps: function (propsJsonPtr) {
    var props = {};
    try { props = JSON.parse(UTF8ToString(propsJsonPtr)); } catch (e) {}
    if (window.DwhAnalyticsBridge) window.DwhAnalyticsBridge.setUserProps(props);
  },

  // 이탈(page_exit) 때 쓸 지금 화면과 공통 파라미터
  DwhFb_SetContext: function (contextJsonPtr) {
    var ctx = {};
    try { ctx = JSON.parse(UTF8ToString(contextJsonPtr)); } catch (e) {}
    if (window.DwhAnalyticsBridge && window.DwhAnalyticsBridge.setContext) window.DwhAnalyticsBridge.setContext(ctx);
  }
});
