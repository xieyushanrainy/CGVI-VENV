using UnityEngine;
using TMPro;

// =============================================================================
//  TutorialReadyUI.cs
//
//  Drives the Tutorial Ready panel UI:
//    • Local and opponent ready state text (no button — ready is triggered via
//      the XR right-hand activate action wired in TutorialReadyManager)
//    • Hides the panel once both players are ready
//
//  SETUP
//  -----
//  1. Attach this component to any GameObject in the arena scene
//     (e.g. the root of your Tutorial Ready canvas panel).
//  2. Wire the Inspector fields:
//       readyManager       → TutorialReadyManager in the scene (auto-found if empty)
//       localStatusText    → Text showing the local player's ready state
//       opponentStatusText → Text showing the opponent's ready state (optional)
//       tutorialPanel      → Root GameObject of the whole panel (optional — hides on both ready)
//  3. No further code changes needed.
// =============================================================================

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
        string localLine    = localReady    ? "You: Ready \u2713"       : "You: Not Ready — press trigger to ready up";
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
