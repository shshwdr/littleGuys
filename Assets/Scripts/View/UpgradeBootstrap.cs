using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

public class UpgradeBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (!SceneFlowService.IsUpgradeScene())
            return;

        if (FindObjectOfType<UpgradeBootstrap>() != null)
            return;

        var go = new GameObject("UpgradeBootstrap");
        go.AddComponent<UpgradeBootstrap>();
    }

    readonly CompositeDisposable disposables = new CompositeDisposable();
    bool built;

    void Awake()
    {
        MainThreadDispatcher.Initialize();
        EnsureEventSystem();
        SetupCamera();
    }

    void Start()
    {
        if (built)
            return;

        built = true;
        BuildUpgradeScene();
    }

    void OnDestroy()
    {
        disposables.Dispose();
    }

    void BuildUpgradeScene()
    {
        var metaSave = MetaSaveService.Load();

        var uiGo = new GameObject("UpgradeSceneView");
        uiGo.transform.SetParent(transform, false);
        uiGo.AddComponent<UpgradeSceneView>().Setup(metaSave, disposables);
    }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            return;
        }

        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
    }
}
