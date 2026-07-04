using UnityEngine.SceneManagement;

public static class SceneFlowService
{
    public const string MainGameScene = "MainGame";

    public static bool IsMainGameScene()
    {
        return SceneManager.GetActiveScene().name == MainGameScene;
    }

    public static void LoadMainGame()
    {
        SceneManager.LoadScene(MainGameScene);
    }
}
