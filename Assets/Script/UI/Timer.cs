using UnityEngine;
using System.Collections;
using TMPro;

public class Timer : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    // void Start()
    // {
        
    // }
    [SerializeField] TextMeshProUGUI timerText;
    int lastDisplayedSecond = -1;
    bool startTimer = false;
    float remainingTime;

    bool warning = false;
    // Update is called once per frame
    void Update()
    {   if (startTimer)
            timerUpdate();
    }
    public void startGame()
    {
        remainingTime = GameData.GameDuration;
        startTimer = true;
    }
    public void timerUpdate()
    {
        if (remainingTime <= 0)
        {
            remainingTime = 0;
            startTimer = false;
            // Notify the authority ScoreManager — it will stop scoring and
            // broadcast a game-over message. Both clients then call End() via
            // ScoreManager.OnGameOver (wired in canvasControl.Start()).
            FindFirstObjectByType<ScoreManager>().NotifyTimerExpired();
            return;         
        }

        if (remainingTime < 10.0 && warning == false)
        {
            timerText.color = Color.red;
            timerText.fontStyle = FontStyles.Bold;
            timerText.fontSize = 55;
            warning = true;
        }

        remainingTime -= Time.deltaTime;

        int totalSeconds = Mathf.CeilToInt(remainingTime);

        // Only update text if the second changed
        if (totalSeconds != lastDisplayedSecond)
        {
            lastDisplayedSecond = totalSeconds;

            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            timerText.text = $"{minutes:00}:{seconds:00}";

            // if (totalSeconds%5 == 0)
            // {
            //     FindObjectOfType<moleScore>().hit();
            //     FindObjectOfType<hammerScore>().hit();
            // }
        }
    }
}
