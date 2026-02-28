#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drop this on any GameObject in the lobby scene.
/// Press [~] to show/hide the overlay during Play Mode.
/// Simulates: Join Room → Opponent Joins → Opponent Ready.
/// </summary>
public class RoleManagerDebugUI : MonoBehaviour
{
    [Tooltip("Key to toggle the debug panel.")]
    public Key toggleKey = Key.Backquote;   // ~ key

    private RoleManager _rm;
    private bool _visible = true;

    private void Start()
    {
        _rm = FindFirstObjectByType<RoleManager>();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
            _visible = !_visible;
    }

    private void OnGUI()
    {
        if (!_visible || _rm == null) return;

        // Semi-transparent background
        GUI.Box(new Rect(8, 8, 260, 200), "");
        GUILayout.BeginArea(new Rect(16, 16, 244, 188));

        GUILayout.Label("<b>[DEBUG] RoleManager</b>", RichStyle());
        GUILayout.Label($"Role: {_rm.LocalRole}  |  Ready: {_rm.IsLocalReady}  |  Opp: {_rm.IsOpponentReady}");

        GUILayout.Space(4);

        if (GUILayout.Button("1. Simulate Join Room"))
            _rm.Debug_SimulateJoinRoom();

        if (GUILayout.Button("2. Simulate Opponent Joined (you=Hammer)"))
            _rm.Debug_SimulateOpponentJoined();

        if (GUILayout.Button("3. Toggle Opponent Ready"))
            _rm.Debug_ToggleOpponentReady();

        if (GUILayout.Button("4. Toggle Local Ready"))
            _rm.Debug_ToggleLocalReady();

        GUILayout.EndArea();
    }

    private static GUIStyle RichStyle()
    {
        var s = new GUIStyle(GUI.skin.label);
        s.richText = true;
        return s;
    }
}
#endif
