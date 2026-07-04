using UnityEngine.SceneManagement;

public static class SceneFlowService
{
    public const string MainGameScene = "MainGame";
    public const string UpgradeScene = "UpgradeScene";

    public static bool IsMainGameScene()
    {
        return SceneManager.GetActiveScene().name == MainGameScene;
    }

    public static bool IsUpgradeScene()
    {
        return SceneManager.GetActiveScene().name == UpgradeScene;
    }

    public static void LoadMainGame()
    {
        SceneManager.LoadScene(MainGameScene);
    }

    public static void LoadUpgradeScene()
    {
        SceneManager.LoadScene(UpgradeScene);
    }
}
