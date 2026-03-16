using UnityEngine;
using TMPro;

public class canvasControl : MonoBehaviour
{
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
    {
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
