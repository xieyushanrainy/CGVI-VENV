using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class canvasControl : MonoBehaviour
{
    private ScoreManager scoreManager;
    private TutorialReadyManager tutorialReadyManager;
    public GameObject molePlayUI;
    public GameObject hammerPlayUI;
    public GameObject endGameUI;
    [SerializeField] TextMeshProUGUI winner;
    [SerializeField] GameObject hammerMesh;

    // -------------------------------------------------------------------------
    //  B Button – Show / Dismiss End-Game Panel at any time
    // -------------------------------------------------------------------------

    [Header("B Button – End-Game Panel Toggle")]
    [Tooltip("Assign the right-hand 'B' / Secondary Button InputActionReference here.\n" +
             "In the XRI Default Input Actions asset this is usually called\n" +
             "'XRI RightHand Interaction/Secondary Button'.")]
    [SerializeField] private InputActionReference bButtonAction;

    [Tooltip("These UI elements are hidden while the end-game panel is showing.\n" +
             "When the player dismisses the panel mid-game they are restored\n" +
             "to their previous active state automatically.\n\n" +
             "Typical entries: score displays, timer, role indicators, etc.")]
    [SerializeField] private List<GameObject> itemsToHideOnPauseMenu = new();

    // Saved active-states for the configurable list so we can restore them.
    private bool[] _savedItemStates;

    // True only while the panel was opened manually via B (not via game-over).
    // Allows B to dismiss it; cleared when a real game-over fires.
    private bool _manuallyOpened;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        endGameUI.SetActive(false);
        molePlayUI.SetActive(GameData.LocalRole == RoleManager.Role.Mole);
        hammerPlayUI.SetActive(GameData.LocalRole != RoleManager.Role.Mole);

        scoreManager = FindObjectOfType<ScoreManager>();
        if (scoreManager != null)
            scoreManager.OnGameOver += HandleGameOver;

        tutorialReadyManager = FindObjectOfType<TutorialReadyManager>();
        if (tutorialReadyManager != null)
            tutorialReadyManager.OnBothReady += StartGame;
    }

    private void OnEnable()
    {
        if (bButtonAction != null)
        {
            bButtonAction.action.Enable();
            bButtonAction.action.performed += OnBButtonPressed;
        }
    }

    private void OnDisable()
    {
        if (bButtonAction != null)
            bButtonAction.action.performed -= OnBButtonPressed;
    }

    private void OnDestroy()
    {
        if (scoreManager != null)
            scoreManager.OnGameOver -= HandleGameOver;

        if (tutorialReadyManager != null)
            tutorialReadyManager.OnBothReady -= StartGame;
    }

    // -------------------------------------------------------------------------
    //  B Button handler
    // -------------------------------------------------------------------------

    private void OnBButtonPressed(InputAction.CallbackContext ctx)
    {
        // If the panel is showing as a permanent game-over screen, ignore B.
        if (endGameUI.activeSelf && !_manuallyOpened) return;

        if (endGameUI.activeSelf && _manuallyOpened)
            HideEndGamePanel();
        else
            ShowEndGamePanel();
    }

    // -------------------------------------------------------------------------
    //  Public API – call ShowEndGamePanel() from other scripts if needed
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shows the end-game panel immediately (same panel as the game-over screen).
    /// Saves and hides every item in <c>itemsToHideOnPauseMenu</c> so the
    /// panel can be dismissed mid-game with B and the HUD is restored.
    /// Called automatically when the player presses B.
    /// </summary>
    public void ShowEndGamePanel()
    {
        if (endGameUI.activeSelf) return;

        // Save and hide every configurable HUD item.
        if (itemsToHideOnPauseMenu != null && itemsToHideOnPauseMenu.Count > 0)
        {
            _savedItemStates = new bool[itemsToHideOnPauseMenu.Count];
            for (int i = 0; i < itemsToHideOnPauseMenu.Count; i++)
            {
                if (itemsToHideOnPauseMenu[i] != null)
                {
                    _savedItemStates[i] = itemsToHideOnPauseMenu[i].activeSelf;
                    itemsToHideOnPauseMenu[i].SetActive(false);
                }
            }
        }

        endGameUI.SetActive(true);
        _manuallyOpened = true;
    }

    /// <summary>
    /// Dismisses the end-game panel and restores every item that was hidden
    /// by <see cref="ShowEndGamePanel"/>.
    /// Called automatically when the player presses B while the panel is open.
    /// Has no effect if the panel was opened by a real game-over.
    /// </summary>
    public void HideEndGamePanel()
    {
        if (!_manuallyOpened) return;

        endGameUI.SetActive(false);
        _manuallyOpened = false;

        // Restore the HUD items to their saved states.
        if (itemsToHideOnPauseMenu != null && _savedItemStates != null)
        {
            for (int i = 0; i < itemsToHideOnPauseMenu.Count; i++)
            {
                if (itemsToHideOnPauseMenu[i] != null && i < _savedItemStates.Length)
                    itemsToHideOnPauseMenu[i].SetActive(_savedItemStates[i]);
            }
        }
    }

    // -------------------------------------------------------------------------
    //  Internal callbacks
    // -------------------------------------------------------------------------

    private void HandleGameOver(ScoreUpdateMessage final)
    {
        End();
    }

    public void moleClicked() { }
    public void hammerClicked() { }

    public void StartGame()
    {
        // Play UIs are already visible; no layout change needed when game starts.
        if (scoreManager != null) scoreManager.BeginGame();
        FindObjectOfType<Timer>().startGame();
    }

    public void End()
    {
        // Guard: if End() is somehow called twice (e.g. from a stale timer path)
        // and the panel is already locked as a permanent end screen, skip.
        if (endGameUI.activeSelf && !_manuallyOpened) return;

        // If the player had manually opened the panel via B, restore any items
        // that were hidden so they are visible during the end-game screen.
        if (_manuallyOpened && itemsToHideOnPauseMenu != null && _savedItemStates != null)
        {
            for (int i = 0; i < itemsToHideOnPauseMenu.Count; i++)
            {
                if (itemsToHideOnPauseMenu[i] != null && i < _savedItemStates.Length)
                    itemsToHideOnPauseMenu[i].SetActive(_savedItemStates[i]);
            }
        }

        // Panel may be open via manual B-press: clear the manual flag so B can
        // no longer dismiss it, then update the winner text and finalise state.
        _manuallyOpened = false;

        string text = scoreManager != null ? scoreManager.GetWinner() : "Draw";
        winner.text = text == "Draw" ? text : $"The winner is {text}!";

        molePlayUI.SetActive(false);
        hammerPlayUI.SetActive(false);
        endGameUI.SetActive(true);

        if (hammerMesh != null && GameData.LocalRole == RoleManager.Role.Hammer)
            hammerMesh.SetActive(false);

        FindObjectOfType<FireworkSpawner>().PlayMultipleFireworksWithDelay();
    }
}
