/// <summary>
/// Lightweight static carrier for data that must survive a scene load.
/// Write before calling SceneManager.LoadScene; read in the new scene's Start/Awake.
/// </summary>
public static class GameData
{
    public static RoleManager.Role LocalRole { get; set; }
    // public static Raiser.MoleMovementMode moleMovementMode { get; set; }
    public static int MoleScore = 0;
    public static int HammerScore = 0;

    /// <summary>Total round duration in seconds. Change this to adjust game length.</summary>
    public static float GameDuration = 60f;

    /// <summary>
    /// The AvatarManager's original prefab, saved by RolePanelController before
    /// it is nulled on game-scene entry. Restored by EndGameController on Exit
    /// so lobby avatars reappear when the player returns to the entry scene.
    /// </summary>
    public static UnityEngine.GameObject LobbyAvatarPrefab;

    public static string GetWinner()
    {
        if (HammerScore > MoleScore) return "Hammer";
        if (MoleScore   > HammerScore) return "Mole";
        return "Draw";
    }
}
