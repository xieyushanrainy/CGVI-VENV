using UnityEngine;
using System.Collections.Generic;
using TMPro;
public class hammerScore : MonoBehaviour
{
    [SerializeField] int updateScore;
    [SerializeField] List<TextMeshProUGUI> scoreTexts;
    private int score = 0;

    // private void Awake()
    // {
    //     instance = this;
    // }
    void Start()
    {
        score = 0;
        updateText(0);
    }

    public int hit()
    {
        score = score + updateScore;
        //scoreText.text = $"{score}";
        return score;
    }
    public void updateText(int curScore)
    {
        foreach (var t in scoreTexts)
            if (t != null) t.text = $"{curScore}";
    }
}
