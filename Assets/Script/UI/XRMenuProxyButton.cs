using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attached to each world-space proxy button spawned by <see cref="XRMenuProxySpawner"/>.
/// Listens to the <see cref="XRSimpleInteractable.selectEntered"/> event and
/// forwards it to the correct <see cref="EndGameController"/> method.
///
/// The enum value and the controller reference are set by XRMenuProxySpawner
/// at spawn time; you can also configure them manually in the Inspector when
/// using a prefab that already carries this component.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class XRMenuProxyButton : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Types
    // -------------------------------------------------------------------------

    public enum ProxyButtonType
    {
        /// <summary>Calls <see cref="EndGameController.OnRestartClicked"/> — same roles.</summary>
        Restart,

        /// <summary>Calls <see cref="EndGameController.OnRestartSwitchedClicked"/> — swap Hammer ↔ Mole roles.</summary>
        RestartSwitched,

        /// <summary>Calls <see cref="EndGameController.OnExitClicked"/> — return both players to the lobby.</summary>
        Exit,
    }

    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("Button Identity")]
    [Tooltip("Which end-game action this proxy represents.\n" +
             "Set automatically by XRMenuProxySpawner.")]
    public ProxyButtonType buttonType;

    [Header("Action Target")]
    [Tooltip("The EndGameController to call when this proxy is selected.\n" +
             "Populated automatically by XRMenuProxySpawner when spawned at runtime.")]
    public EndGameController controller;

    [Header("Optional Event")]
    [Tooltip("Fired in addition to the EndGameController call.\n" +
             "Useful for feedback: sounds, haptics, visual effects, etc.")]
    public UnityEvent onActivated;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private XRSimpleInteractable _interactable;
    private bool _subscribed;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();

        if (_interactable == null)
            Debug.LogWarning($"[XRMenuProxyButton] No XRSimpleInteractable found on '{name}'. " +
                             "Interaction will not work.");
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // -------------------------------------------------------------------------
    //  Public API — called by XRMenuProxySpawner after component setup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Updates the interactable reference and re-subscribes.
    /// Called by <see cref="XRMenuProxySpawner"/> to pass the resolved
    /// <see cref="XRSimpleInteractable"/> after all components are set up.
    /// </summary>
    public void Init(XRSimpleInteractable interactable)
    {
        // Remove existing subscription before swapping reference.
        Unsubscribe();

        _interactable = interactable;

        // Re-subscribe if this component is currently active and enabled.
        if (isActiveAndEnabled)
            Subscribe();
    }

    // -------------------------------------------------------------------------
    //  XRI callback
    // -------------------------------------------------------------------------

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Debug.Log($"[XRMenuProxyButton] '{name}' selected — type: {buttonType}");

        // Fire optional Unity event first (audio cues, haptics, etc.).
        onActivated?.Invoke();

        if (controller == null)
        {
            Debug.LogWarning($"[XRMenuProxyButton] '{name}': EndGameController is not assigned. " +
                             "No game method will be called.");
            return;
        }

        switch (buttonType)
        {
            case ProxyButtonType.Restart:
                // Restart with the same Hammer / Mole assignment.
                controller.OnRestartClicked();
                break;

            case ProxyButtonType.RestartSwitched:
                // Restart with roles swapped (Hammer ↔ Mole).
                controller.OnRestartSwitchedClicked();
                break;

            case ProxyButtonType.Exit:
                // Return both players to the lobby / entry scene.
                controller.OnExitClicked();
                break;

            default:
                Debug.LogWarning($"[XRMenuProxyButton] '{name}': Unhandled ProxyButtonType '{buttonType}'.");
                break;
        }
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    private void Subscribe()
    {
        if (_subscribed || _interactable == null) return;
        _interactable.selectEntered.AddListener(OnSelectEntered);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _interactable == null) return;
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
        _subscribed = false;
    }
}
