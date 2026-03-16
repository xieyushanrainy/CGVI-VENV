/// <summary>
/// Lightweight static carrier for data that must survive a scene load.
/// Write before calling SceneManager.LoadScene; read in the new scene's Start/Awake.
/// </summary>
public static class GameData
{
    public static RoleManager.Role LocalRole = RoleManager.Role.Mole;// { get; set; }
    public static int moleScore = 0;
    public static int hammerScore = 0;
    public static string GetWinner()
    {
        if (HammerScore > MoleScore) return "Hammer";
        if (MoleScore   > HammerScore) return "Mole";
        return "Draw";
    }
}
