using UnityEngine;

public class soundManager : MonoBehaviour
{
    public AudioSource audioSource;

    public AudioClip hitClip;
    public AudioClip missClip;

    public void PlaySound(bool hit)
    {
        if (hit)
        {
            audioSource.clip = hitClip;
        } else
        {
            audioSource.clip = missClip;
        }
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        audioSource.Play();
    }
}
