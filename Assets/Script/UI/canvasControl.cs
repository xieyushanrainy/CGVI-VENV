using UnityEngine;
using TMPro;

public class canvasControl : MonoBehaviour
{
    private ScoreManager scoreManager;
    private TutorialReadyManager tutorialReadyManager;
    public GameObject playUI;
    public GameObject endGameUI;
    public GameObject moleReady;
    public GameObject hammerReady;
    [SerializeField] TextMeshProUGUI readyText_mole;
    [SerializeField] TextMeshProUGUI readyText_hammer;
    [SerializeField] TextMeshProUGUI winner;

    void Start()
    {
        playUI.SetActive(false);
        endGameUI.SetActive(false);
        if (GameData.LocalRole == RoleManager.Role.Mole)
        {
            moleReady.SetActive(true);
            hammerReady.SetActive(false);
        } else
        {
            moleReady.SetActive(false);
            hammerReady.SetActive(true);
        }

        // Subscribe to the authoritative game-over event so End() is called on
        // BOTH clients from the same Ubiq-synced trigger, not from each client's
        // independent timer expiry.
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

    public void moleClicked()
    {
        SetTextTransparency(readyText_mole, 1f);
    }

    public void hammerClicked()
    {
        SetTextTransparency(readyText_hammer, 1f);
    }

    private void SetTextTransparency(TextMeshProUGUI text, float alpha)
    {
        Color c = text.color;
        c.a = alpha;
        text.color = c;
    }

    public void StartGame()
    {
        playUI.SetActive(true);
        endGameUI.SetActive(false);
        moleReady.SetActive(false);
        hammerReady.SetActive(false);
        FindObjectOfType<Timer>().startGame();
    }

    public void End()
    {        // Guard: if End() is somehow called twice (e.g. from a stale timer path)
        // don't show the end screen a second time.
        if (endGameUI.activeSelf) return;
        string text = FindObjectOfType<ScoreManager>().GetWinner();
        if (text == "Draw")
        {
            winner.text = text;
        } else
        {
            winner.text = $"The winner is {text}!";
        }
        
        playUI.SetActive(false);
        endGameUI.SetActive(true);
        moleReady.SetActive(false);
        hammerReady.SetActive(false);
        FindObjectOfType<FireworkSpawner>().PlayMultipleFireworksWithDelay();
        // tab
    }
}
