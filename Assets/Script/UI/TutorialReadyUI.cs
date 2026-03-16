using UnityEngine;
using UnityEngine.UI;

// =============================================================================
//  TutorialReadyUI.cs
//
//  Drives the Tutorial Ready panel UI:
//    • Ready / Un-ready toggle button
//    • Opponent status text
//    • Hides (or disables) the panel once both players are ready
//
//  SETUP
//  -----
//  1. Attach this component to any GameObject in the arena scene
//     (e.g. the root of your Tutorial Ready canvas panel).
//  2. Wire the Inspector fields:
//       readyManager      → TutorialReadyManager in the scene (auto-found if empty)
//       readyButton       → Your existing Ready Button
//       readyButtonLabel  → Text child of the Ready Button (shows "Ready"/"Un-ready")
//       opponentStatusText→ Text showing the opponent's ready state (optional)
//       tutorialPanel     → Root GameObject of the whole panel (optional — hides on start)
//  3. No further code changes needed — the button is wired automatically.
// =============================================================================

/// <summary>
/// UI controller for the tutorial exploration ready-check.
/// Subscribes to <see cref="TutorialReadyManager"/> events and updates
/// the ready button label, opponent status text, and panel visibility.
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
    [Tooltip("The Ready / Un-ready toggle button (your existing UI button).")]
    public Button readyButton;

    [Tooltip("Text label INSIDE the ready button. " +
             "Switches between 'Ready' and 'Un-ready'.")]
    public Text readyButtonLabel;

    [Tooltip("(Optional) Text that shows the opponent's current ready state.")]
    public Text opponentStatusText;

    [Tooltip("(Optional) Root panel GameObject. " +
             "Hidden automatically after a short delay once both players are ready.")]
    public GameObject tutorialPanel;

    [Header("Behaviour")]
    [Tooltip("Seconds to wait after both players are ready before hiding the panel.")]
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

        // Default the panel to this GameObject so you can skip wiring it
        // in the Inspector when this component lives on the panel root.
        if (tutorialPanel == null)
            tutorialPanel = gameObject;

        // Subscribe
        readyManager.OnLocalReadyChanged    += HandleLocalReadyChanged;
        readyManager.OnOpponentReadyChanged += HandleOpponentReadyChanged;
        readyManager.OnBothReady            += HandleBothReady;

        // Wire button
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyClicked);

        // Reflect whatever state already exists (e.g. after a scene reload)
        HandleLocalReadyChanged(readyManager.IsLocalReady);
        HandleOpponentReadyChanged(readyManager.IsOpponentReady);
    }

    private void OnDestroy()
    {
        if (readyManager == null) return;

        readyManager.OnLocalReadyChanged    -= HandleLocalReadyChanged;
        readyManager.OnOpponentReadyChanged -= HandleOpponentReadyChanged;
        readyManager.OnBothReady            -= HandleBothReady;
    }

    // -------------------------------------------------------------------------
    //  Event handlers
    // -------------------------------------------------------------------------

    private void HandleLocalReadyChanged(bool isReady)
    {
        // Toggle button label
        if (readyButtonLabel != null)
            readyButtonLabel.text = isReady ? "Un-ready" : "Ready";
    }

    private void HandleOpponentReadyChanged(bool opponentReady)
    {
        if (opponentStatusText != null)
            opponentStatusText.text = opponentReady
                ? "Opponent: Ready \u2713"
                : "Opponent: Not Ready";
    }

    private void HandleBothReady()
    {
        // Disable the button — no un-readying once the game starts
        if (readyButton != null)
            readyButton.interactable = false;

        if (readyButtonLabel != null)
            readyButtonLabel.text = "Starting...";

        // Auto-hide the panel after a short delay so players can see the state
        if (tutorialPanel != null)
            Invoke(nameof(HidePanel), hideDelay);
    }

    private void HidePanel()
    {
        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);
    }

    // -------------------------------------------------------------------------
    //  Button callback
    // -------------------------------------------------------------------------

    private void OnReadyClicked()
    {
        if (readyManager == null) return;

        if (readyManager.IsLocalReady)
            readyManager.RequestUnready();
        else
            readyManager.RequestReady();
    }
}
