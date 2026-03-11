using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class moleScore : MonoBehaviour
{
    //public static moleScore instance;
    int score = 0;
    float upTime = 0;

    int lastSecond = -1;

    
    [SerializeField] int baseScore;
    [SerializeField] TextMeshProUGUI scoreText;

    int updateScore;
    // private void Awake()
    // {
    //     instance = this;
    // }

    void Start()
    {
        updateScore = baseScore;
    }
    public void clear()
    {
        upTime = 0;
        updateScore = 0;
    }

    public int addScore(int molePointsPerSecond)
    {
        upTime += Time.deltaTime;

        int totalSeconds = Mathf.CeilToInt(upTime);

        if (totalSeconds != lastSecond)
        {
            lastSecond = totalSeconds;
            score = score + updateScore;
            //scoreText.text = $"{score}";
            
            if (totalSeconds % 3 == 0)
            {
                updateScore = updateScore + 1;
            }

        }
        return score;   
    }
    void Update()
    {   
        // addScore();
    }
    public void hit()
    {
        // score --;
        clear();
        if (GameData.LocalRole == RoleManager.Role.Mole)
        {
            FindObjectOfType<damageEffect>().FlashDamage();
        }
    }
    public void updateText(int curScore)
    {
        scoreText.text = $"{curScore}";
    }
}
