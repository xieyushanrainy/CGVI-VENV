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
    [SerializeField] float remainingTime;
    int lastDisplayedSecond = -1;
    // Update is called once per frame
    void Update()
    {   
        timerUpdate();
    }
    public void timerUpdate()
    {
        if (remainingTime <= 0)
        {
            remainingTime = 0;
            return;
            // GameOver();
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
