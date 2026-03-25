using UnityEngine;
using TMPro;

/// <summary>
/// UI controller for the tutorial exploration ready-check.
/// Subscribes to <see cref="TutorialReadyManager"/> events and updates
/// local/opponent status texts and panel visibility.
/// Ready state is driven externally by the XR right-hand activate action.
/// </summary>
public class TutorialReadyUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("Manager Reference")]
    [Tooltip("TutorialReadyManager in the scene. Auto-found if left empty.")]
    public TutorialReadyManager readyManager;

    [Header("UI Elements")]
    [Tooltip("Text label showing both local and opponent ready states on two lines.")]
    public TextMeshProUGUI statusText;

    [Tooltip("Seconds to show 'Go!' before hiding the status text.")]
    public float hideDelay = 1.5f;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        if (!readyManager)
            readyManager = FindFirstObjectByType<TutorialReadyManager>();

        if (readyManager == null)
        {
            Debug.LogWarning("[TutorialReadyUI] No TutorialReadyManager found in scene. " +
                             "Attach TutorialReadyManager to a GameObject and ensure it " +
                             "is active before this UI initialises.");
            return;
        }

        // Subscribe
        readyManager.OnLocalReadyChanged    += HandleLocalReadyChanged;
        readyManager.OnOpponentReadyChanged += HandleOpponentReadyChanged;
        readyManager.OnCountdownTick        += HandleCountdownTick;
        readyManager.OnBothReady            += HandleBothReady;

        // Reflect whatever state already exists (e.g. after a scene reload)
        UpdateStatusText(readyManager.IsLocalReady, readyManager.IsOpponentReady);
    }

    private void OnDestroy()
    {
        if (readyManager == null) return;

        readyManager.OnLocalReadyChanged    -= HandleLocalReadyChanged;
        readyManager.OnOpponentReadyChanged -= HandleOpponentReadyChanged;
        readyManager.OnCountdownTick        -= HandleCountdownTick;
        readyManager.OnBothReady            -= HandleBothReady;
    }

    // -------------------------------------------------------------------------
    //  Event handlers
    // -------------------------------------------------------------------------

    private void HandleLocalReadyChanged(bool isReady)
    {
        UpdateStatusText(isReady, readyManager.IsOpponentReady);
    }

    private void HandleOpponentReadyChanged(bool opponentReady)
    {
        UpdateStatusText(readyManager.IsLocalReady, opponentReady);
    }

    private void UpdateStatusText(bool localReady, bool opponentReady)
    {
        if (statusText == null) return;
        string localLine    = localReady    ? "You: Ready \u2713"       : "You: Not Ready — press A to ready up";
        string opponentLine = opponentReady ? "Opponent: Ready \u2713" : "Opponent: Not Ready";
        statusText.text = localLine + "\n" + opponentLine;
    }

    private void HandleCountdownTick(int secondsRemaining)
    {
        if (statusText != null)
            statusText.text = $"Both ready!\nStarting in {secondsRemaining}...";
    }

    private void HandleBothReady()
    {
        if (statusText != null)
            statusText.text = "Go!";

        // Hide just the status text after the delay, not the entire panel
        Invoke(nameof(HideStatusText), hideDelay);
    }

    private void HideStatusText()
    {
        if (statusText != null)
            statusText.gameObject.SetActive(false);
    }
}
