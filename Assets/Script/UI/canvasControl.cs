using UnityEngine;
using TMPro;

public class canvasControl : MonoBehaviour
{
    private ScoreManager scoreManager;
    private TutorialReadyManager tutorialReadyManager;
    public GameObject molePlayUI;
    public GameObject hammerPlayUI;
    public GameObject endGameUI;
    [SerializeField] TextMeshProUGUI winner;
    [SerializeField] GameObject hammerMesh;

    void Start()
    {
        endGameUI.SetActive(false);
        molePlayUI.SetActive(GameData.LocalRole == RoleManager.Role.Mole);
        hammerPlayUI.SetActive(GameData.LocalRole != RoleManager.Role.Mole);

        scoreManager = FindObjectOfType<ScoreManager>();
        if (scoreManager != null)
            scoreManager.OnGameOver += HandleGameOver;

        tutorialReadyManager = FindObjectOfType<TutorialReadyManager>();
        if (tutorialReadyManager != null)
            tutorialReadyManager.OnBothReady += StartGame;
    }

    private void OnDestroy()
    {
        if (scoreManager != null)
            scoreManager.OnGameOver -= HandleGameOver;

        if (tutorialReadyManager != null)
            tutorialReadyManager.OnBothReady -= StartGame;
    }

    private void HandleGameOver(ScoreUpdateMessage final)
    {
        End();
    }

    public void moleClicked() { }
    public void hammerClicked() { }

    public void StartGame()
    {
        // Play UIs are already visible; no layout change needed when game starts.
        if (scoreManager != null) scoreManager.BeginGame();
        FindObjectOfType<Timer>().startGame();
    }

    public void End()
    {        // Guard: if End() is somehow called twice (e.g. from a stale timer path)
        // don't show the end screen a second time.
        if (endGameUI.activeSelf) return;
        string text = scoreManager != null ? scoreManager.GetWinner() : "Draw";
        if (text == "Draw")
        {
            winner.text = text;
        } else
        {
            winner.text = $"The winner is {text}!";
        }
        
        molePlayUI.SetActive(false);
        hammerPlayUI.SetActive(false);
        endGameUI.SetActive(true);
        if (hammerMesh != null && GameData.LocalRole == RoleManager.Role.Hammer)
            hammerMesh.SetActive(false);
        FindObjectOfType<FireworkSpawner>().PlayMultipleFireworksWithDelay();
        // tab
    }
}
