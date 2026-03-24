using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FireworkSpawner : MonoBehaviour
{
    public GameObject fireworkPrefab; // Your FireworkUI prefab
    public int fireworksCount = 5;    // Number of fireworks
    public RectTransform canvasRect;  // Reference to your Canvas RectTransform
    public float delayBetween = 0.3f; // Delay in seconds between fireworks

    public void PlayMultipleFireworksWithDelay()
    {
        StartCoroutine(SpawnFireworksCoroutine());
    }

    private IEnumerator SpawnFireworksCoroutine()
    {
        for (int i = 0; i < fireworksCount; i++)
        {
            // Random position inside the canvas
            float x = Random.Range(-canvasRect.rect.width / 2f, canvasRect.rect.width / 2f);
            float y = Random.Range(-canvasRect.rect.height / 2f, canvasRect.rect.height / 2f);
            Vector3 spawnPos = new Vector3(x, y, 0);

            // Instantiate Firework prefab
            GameObject fw = Instantiate(fireworkPrefab, canvasRect);

            // Prevent firework graphics from blocking UI input.
            foreach (Graphic graphic in fw.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            CanvasGroup cg = fw.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.blocksRaycasts = false;

            fw.GetComponent<RectTransform>().anchoredPosition = spawnPos;

            // Play the firework
            fw.GetComponent<FireworkUIController>().PlayFirework();

            // Wait for delay before spawning the next one
            yield return new WaitForSeconds(delayBetween);
        }
    }
}