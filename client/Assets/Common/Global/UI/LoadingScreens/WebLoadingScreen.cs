using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;

namespace Global.UI
{
    // Экран загрузки из index.html: он виден с открытия страницы, пока Unity ещё качается,
    // и прячется только по вызову из игры.
    public class WebLoadingScreen : ILoadingScreen
    {
        [DllImport("__Internal")]
        private static extern void WebLoadingScreenShow();

        [DllImport("__Internal")]
        private static extern void WebLoadingScreenShowInstantly();

        [DllImport("__Internal")]
        private static extern void WebLoadingScreenHide();

        [DllImport("__Internal")]
        private static extern bool WebLoadingScreenIsShown();

        public async UniTask Show()
        {
            WebLoadingScreenShow();
            await UniTask.WaitUntil(() => WebLoadingScreenIsShown() == true);
        }

        public void ShowInstantly()
        {
            WebLoadingScreenShowInstantly();
        }

        public void Hide()
        {
            WebLoadingScreenHide();
        }
    }
}
