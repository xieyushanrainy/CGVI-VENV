/// <summary>
/// Lightweight static carrier for data that must survive a scene load.
/// Write before calling SceneManager.LoadScene; read in the new scene's Start/Awake.
/// </summary>
public static class GameData
{
    public static RoleManager.Role LocalRole { get; set; }
}
