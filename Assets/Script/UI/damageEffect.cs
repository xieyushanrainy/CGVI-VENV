using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class damageEffect : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private Image damageOverlay; // Assign the red Image here
    [SerializeField] private float flashDuration = 0.5f; // How long the red stays visible
    [SerializeField] private float fadeSpeed = 2f; // How fast it fades back

    private Coroutine currentFlash;

    public void FlashDamage()
    {
        // Stop previous flash if it's still running
        if (currentFlash != null)
            StopCoroutine(currentFlash);

        currentFlash = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        // Immediately set overlay fully visible
        damageOverlay.color = new Color(0.8f, 0f, 0f, 0.7f); // Red with 80% opacity

        float timer = 0f;

        // Wait for flashDuration while keeping it visible
        while (timer < flashDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // Fade back to transparent
        while (damageOverlay.color.a > 0f)
        {
            Color c = damageOverlay.color;
            c.a -= Time.deltaTime * fadeSpeed;
            damageOverlay.color = c;
            yield return null;
        }

        damageOverlay.color = new Color(0.8f, 0f, 0f, 0f); // Ensure fully transparent
        currentFlash = null;
    }
}
