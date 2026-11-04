using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 씬 안의 UI 와 GameManager 를 이어주는 중계 컴포넌트 (결과 화면, 버튼용).
/// GameManager 는 씬이 바뀌어도 남는 오브젝트라서, 씬의 버튼 OnClick 에 직접 연결하면 다시 불러온 뒤 연결이 끊긴다.
/// 대신 이 컴포넌트를 씬(결과 패널, 타이틀 등)에 붙이고 버튼을 여기 함수에 연결한다.
///
///  - 판이 끝나면 On Stage Cleared / On Game Over 실행 (결과 패널 켜기 등)
///  - Kill Count Text 를 넣으면 처치 수를 표시
///  - 버튼: StartGame / Restart / ToTitle / Quit
/// </summary>
public class RunEventRelay : MonoBehaviour
{
    [Header("판 종료 시")]
    [SerializeField] private UnityEvent _onStageCleared = new UnityEvent();
    [SerializeField] private UnityEvent _onGameOver = new UnityEvent();

    [Header("결과 표시 (선택)")]
    [SerializeField] private TMP_Text _killCountText;
    [SerializeField] private string _killCountFormat = "처치 수: {0}";
    [SerializeField] private TMP_Text _goldText;
    [SerializeField] private string _goldFormat = "골드: {0}";

    [Tooltip("켜면 시작할 때 이미 끝난 판의 결과를 바로 표시 (결과 화면을 별도 씬으로 만들 때)")]
    [SerializeField] private bool _showLastResultOnStart = false;

    private void OnEnable()
    {
        GameManager.Instance.RunEnded += OnRunEnded;
    }

    private void OnDisable()
    {
        if (GameManager.HasInstance) GameManager.Instance.RunEnded -= OnRunEnded;
    }

    private void Start()
    {
        if (_showLastResultOnStart && GameManager.Instance.IsRunOver)
            OnRunEnded(GameManager.Instance.LastResult);
    }

    private void OnRunEnded(RunResult result)
    {
        if (_killCountText != null) _killCountText.text = string.Format(_killCountFormat, result.KillCount);
        if (_goldText != null) _goldText.text = string.Format(_goldFormat, result.Gold);

        if (result.IsCleared) _onStageCleared?.Invoke();
        else _onGameOver?.Invoke();
    }

    // ───────────────────────── 버튼 OnClick 용 ─────────────────────────

    public void StartGame() => GameManager.Instance.StartGame();
    public void Restart() => GameManager.Instance.RestartRun();
    public void ToTitle() => GameManager.Instance.GoToTitle();
    public void Quit() => GameManager.Instance.QuitGame();
}
