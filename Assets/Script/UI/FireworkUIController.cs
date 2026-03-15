using UnityEngine;
using UnityEngine.UI;
using System.Collections;


public class FireworkUIController : MonoBehaviour
{
    public GameObject particlePrefab; // small UI Image
    public int count = 30;            // number of particles per firework
    public float minSpeed = 80f;      // minimum speed
    public float maxSpeed = 140f;     // maximum speed
    public float duration = 3f;       // how long particles last

    public void PlayFirework()
    {
        for (int i = 0; i < count; i++)
        {
            GameObject p = Instantiate(particlePrefab, transform);
            p.transform.localPosition = Vector3.zero;

            Vector2 dir = Random.insideUnitCircle.normalized;          // random direction
            float speed = Random.Range(minSpeed, maxSpeed);           // random speed for each particle

            StartCoroutine(MoveAndFade(p.GetComponent<RectTransform>(), dir, speed));
        }
        particlePrefab.SetActive(false);
    }

    private IEnumerator MoveAndFade(RectTransform rt, Vector2 direction, float speed)
    {
        Image img = rt.GetComponent<Image>();
        float elapsed = 0f;


        while (elapsed < duration)
        {
            float delta = Time.deltaTime;
            elapsed += delta;

            // move particle with its own speed
            rt.anchoredPosition += direction * (speed-elapsed * 10) * delta;

            // fade out gradually
            img.color = new Color(img.color.r, img.color.g, img.color.b, 1f - (elapsed / duration * 0.7f));

            yield return null;
        }

        Destroy(rt.gameObject);
    }

}
