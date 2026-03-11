using UnityEngine;
using TMPro;

public class canvasControl : MonoBehaviour
{
    public GameObject playUI;
    public GameObject endGameUI;
    public GameObject moleReady;
    [SerializeField] TextMeshProUGUI readyText_mole;

    void Start()
    {
        playUI.SetActive(false);
        endGameUI.SetActive(false);
        moleReady.SetActive(true);
        
    }

    public void moleClicked()
    {
        SetTextTransparency(readyText_mole, 1f);
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
        FindObjectOfType<Timer>().startGame();
    }

    public void End()
    {
        playUI.SetActive(false);
        endGameUI.SetActive(true);
        moleReady.SetActive(false);
    }
}
