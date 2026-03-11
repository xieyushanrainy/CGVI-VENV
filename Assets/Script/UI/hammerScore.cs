using UnityEngine;
using System.Collections;
using TMPro;
public class hammerScore : MonoBehaviour
{
    //public static hammerScore instance;
    [SerializeField] int updateScore;
    [SerializeField] TextMeshProUGUI scoreText;
    private int score = 0;

    // private void Awake()
    // {
    //     instance = this;
    // }
    void Start()
    {
        scoreText.text = $"{score}";
    }

    public int hit()
    {
        score = score + updateScore;
        //scoreText.text = $"{score}";
        return score;
    }
}
