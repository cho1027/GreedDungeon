using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GreedDungeon.YeJun
{
    // This is intentionally isolated from the team's existing GameManager.
    // No other team files are modified.
    public sealed class GDYeJunTitleMenu : MonoBehaviour
    {
        [Header("Optional: Set the exact gameplay scene name once the team confirms it")]
        public string gameplaySceneName = "";

        [Header("Generated UI References")]
        public GameObject helpPanel;
        public Text statusText;

        private Font _systemFont;

        private void Awake()
        {
            Time.timeScale = 1f;

            // The project can be opened on Windows without packaging any font files.
            // Korean glyphs render using the user's installed Windows fonts.
            try
            {
                _systemFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
                if (_systemFont != null)
                {
                    foreach (Text label in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        label.font = _systemFont;
                }
            }
            catch (Exception)
            {
                // Unity's built-in default font remains available as a fallback.
            }

            if (helpPanel != null) helpPanel.SetActive(false);
            if (statusText != null) statusText.text = "";
        }

        public void StartGame()
        {
            if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                if (Application.CanStreamedLevelBeLoaded(gameplaySceneName))
                {
                    SceneManager.LoadScene(gameplaySceneName);
                    return;
                }

                ShowStatus("게임 씬 '" + gameplaySceneName + "'을(를) 빌드 목록에서 찾지 못했습니다.");
                return;
            }

            // To avoid accidentally opening a result or test scene, only auto-select
            // a stage when exactly one other scene is enabled in Build Settings.
            string candidate = null;
            int candidates = 0;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(sceneName, "GD_YeJun_Title", StringComparison.OrdinalIgnoreCase))
                    continue;
                candidates++;
                candidate = sceneName;
            }

            if (candidates == 1 && !string.IsNullOrEmpty(candidate))
            {
                SceneManager.LoadScene(candidate);
                return;
            }

            ShowStatus("게임 시작 연결 전: 팀장에게 1챕터 씬 이름을 확인해 주세요.");
        }

        public void OpenHelp()
        {
            if (helpPanel != null) helpPanel.SetActive(true);
        }

        public void CloseHelp()
        {
            if (helpPanel != null) helpPanel.SetActive(false);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowStatus(string message)
        {
            Debug.LogWarning("[YeJun Title] " + message);
            if (statusText != null) statusText.text = message;
        }
    }
}
