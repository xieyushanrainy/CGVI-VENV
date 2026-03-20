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
    public static float GameDuration = 10f;
    public static string GetWinner()
    {
        if (HammerScore > MoleScore) return "Hammer";
        if (MoleScore   > HammerScore) return "Mole";
        return "Draw";
    }
}
