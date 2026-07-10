using UnityEngine;

[DisallowMultipleComponent]
public class TutorialGameobject : MonoBehaviour
{
    [SerializeField] string identifier;

    Canvas cachedCanvas;
    TutorialManager manager;

    public string Identifier => identifier;

    public Canvas Canvas
    {
        get
        {
            EnsureCanvas();
            return cachedCanvas;
        }
    }

    void Awake()
    {
        EnsureCanvas();
    }

    void Start()
    {
        manager = FindObjectOfType<TutorialManager>();
        manager?.RegisterTutorialGameobject(this);
    }

    void OnDestroy()
    {
        if (manager != null)
            manager.UnregisterTutorialGameobject(this);
    }

    void OnValidate()
    {
        EnsureCanvas();
    }

    void EnsureCanvas()
    {
        if (cachedCanvas == null)
            cachedCanvas = GetComponent<Canvas>();
        if (cachedCanvas == null)
        {
            cachedCanvas = gameObject.AddComponent<Canvas>();
            cachedCanvas.overrideSorting = false;
            cachedCanvas.sortingLayerName = "tutorial";
            cachedCanvas.sortingOrder = 100;
        }
    }
}
