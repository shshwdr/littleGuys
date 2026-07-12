using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TutorialManager : MonoBehaviour
{
    const string TutorialSortingLayer = "tutorial";
    const int TutorialSortingOrder = 10000;

    [SerializeField] bool enableTutorial = true;
    [SerializeField] GameObject tutorialView;
    [SerializeField] TMP_Text tutorialText;
    [SerializeField] GameObject disableAllButton;

    readonly Dictionary<string, Canvas> canvasByIdentifier = new Dictionary<string, Canvas>();
    readonly List<Canvas> activeHighlightCanvases = new List<Canvas>();
    readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    readonly List<Selectable> disabledSelectables = new List<Selectable>();
    readonly HashSet<string> finishedGroups = new HashSet<string>();

    Coroutine runningRoutine;
    Canvas allowedClickCanvas;
    bool waitingForClick;
    bool requireHighlightTargetClick;
    bool disableAllButtonsActive;
    bool waitingForLineInput;
    bool inTimePassGap;
    float resumeTimeScale = 1f;
    string currentTutorialId;

    public bool IsTutorialCompleted => MetaSaveService.Load().TutorialCompleted;

    public void TryShowTutorial(string identifier)
    {
        if (!enableTutorial)
        {
            Debug.LogWarning($"Tutorial skipped '{identifier}': enableTutorial is false.");
            HideTutorialView();
            return;
        }

        if (IsTutorialCompleted)
        {
            Debug.LogWarning($"Tutorial skipped '{identifier}': TutorialCompleted is true in save. Press S to reset meta save.");
            HideTutorialView();
            return;
        }

        // 每次检查前从存档同步，避免 GameBootstrap.Awake 早于本组件 Awake 时读到空列表。
        ReloadFinishedGroups();
        CSVLoader.Init();
        if (IsTutorialGroupFinished(identifier))
        {
            Debug.Log($"Tutorial skipped '{identifier}': group already finished.");
            HideTutorialView();
            return;
        }

        ShowTutorial(identifier);
    }

    public void ShowTutorial(string identifier)
    {
        if (!enableTutorial || IsTutorialCompleted)
        {
            HideTutorialView();
            return;
        }

        ReloadFinishedGroups();
        CSVLoader.Init();

        if (IsTutorialGroupFinished(identifier))
        {
            HideTutorialView();
            return;
        }

        var rows = CSVLoader.GetTutorialRows(identifier);
        if (rows.Count == 0)
        {
            Debug.LogWarning($"Tutorial '{identifier}' has 0 rows in tutorial.csv.");
            HideTutorialView();
            return;
        }

        Debug.Log($"Tutorial show '{identifier}' ({rows.Count} rows).");

        EnsureAllTutorialTargetsRegistered();

        if (runningRoutine != null)
            StopCoroutine(runningRoutine);

        CleanupCurrentLineState(restoreTime: false, removeBlocker: false);
        resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        currentTutorialId = identifier;
        runningRoutine = StartCoroutine(PlayTutorial(identifier));
    }

    bool IsTutorialGroupFinished(string identifier)
    {
        var rows = CSVLoader.GetTutorialRows(identifier);
        for (int i = 0; i < rows.Count; i++)
        {
            string group = rows[i].group;
            if (string.IsNullOrEmpty(group))
                continue;

            if (finishedGroups.Contains(group))
                return true;
        }

        return false;
    }

    void MarkGroupFinished(string group)
    {
        if (string.IsNullOrEmpty(group))
            return;

        ReloadFinishedGroups();
        if (!finishedGroups.Add(group))
            return;

        var meta = MetaSaveService.Load();
        var list = new List<string>(finishedGroups);
        meta.FinishedTutorialGroups = list.ToArray();
        MetaSaveService.Save(meta);
        Debug.Log($"Tutorial finishGroup recorded: '{group}'. Finished=[{string.Join(",", finishedGroups)}]");
    }

    void ReloadFinishedGroups()
    {
        finishedGroups.Clear();
        var meta = MetaSaveService.Load();
        if (meta.FinishedTutorialGroups == null)
            return;

        foreach (var group in meta.FinishedTutorialGroups)
        {
            if (!string.IsNullOrEmpty(group))
                finishedGroups.Add(group);
        }
    }

    void Update()
    {
        if (!waitingForClick || !Input.GetMouseButtonDown(0))
            return;

        if (!TryRaycastUi(out var results))
        {
            waitingForClick = false;
            return;
        }

        if (requireHighlightTargetClick)
        {
            if (!TryGetTopHitUnderClickTarget(results, out var hitObject))
                return;

            PropagatePointerClick(hitObject);
            CaptureResumeTimeScaleFromCurrent();
            waitingForClick = false;
            return;
        }

        PropagateTopRaycastClick(results);
        CaptureResumeTimeScaleFromCurrent();
        waitingForClick = false;
    }

    void CaptureResumeTimeScaleFromCurrent()
    {
        if (Time.timeScale > 0f)
            resumeTimeScale = Time.timeScale;
    }

    void LateUpdate()
    {
        if (!disableAllButtonsActive)
            return;

        RefreshInputBlock(allowedClickCanvas);
    }

    IEnumerator PlayTutorial(string identifier)
    {
        var rows = CSVLoader.GetTutorialRows(identifier);
        if (rows.Count == 0)
        {
            FinishTutorial();
            runningRoutine = null;
            yield break;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            BeginLine(row);

            waitingForClick = true;
            waitingForLineInput = true;
            yield return new WaitUntil(() => !waitingForClick);
            yield return null;

            waitingForLineInput = false;
            EndLine();
            ExecuteLogic(row.logicAfter, true);
            if (!string.IsNullOrEmpty(row.finishGroup))
                MarkGroupFinished(row.finishGroup);

            if (row.isEnd != 0)
            {
                if (currentTutorialId == "upgradeView")
                    MarkTutorialCompleted();
                break;
            }

            Time.timeScale = resumeTimeScale;

            if (row.timePass > 0f)
            {
                inTimePassGap = true;
                if (disableAllButtonsActive)
                    ShowBlockerOverlay();
                HideTutorialView();
                yield return new WaitForSeconds(row.timePass);
                inTimePassGap = false;
            }
        }

        FinishTutorial();
        runningRoutine = null;
    }

    void BeginLine(TutorialInfo row)
    {
        Time.timeScale = 0f;
        ShowTutorialView();

        if (tutorialText != null)
            tutorialText.text = row.text ?? string.Empty;

        ExecuteLogic(row.logic, true);

        EnsureAllTutorialTargetsRegistered();

        Canvas clickCanvas = ResolveCanvas(row.click);
        if (!string.IsNullOrEmpty(row.click) && clickCanvas == null)
            Debug.LogWarning($"Tutorial click target '{row.click}' was not found/registered.");

        Canvas higherSortCanvas = ResolveCanvas(row.higherSort);
        SetAllowedClickCanvas(clickCanvas);

        if (clickCanvas != null)
            requireHighlightTargetClick = true;
        else
            requireHighlightTargetClick = false;

        if (higherSortCanvas != null && higherSortCanvas != clickCanvas)
            EnableCanvasHighlight(higherSortCanvas);

        if (disableAllButtonsActive)
            ShowBlockerOverlay();

        RefreshInputBlock(allowedClickCanvas);
    }

    void EndLine()
    {
        ClearAllowedClickCanvas();
        EndHigherSortHighlights();
        requireHighlightTargetClick = false;

        if (disableAllButtonsActive)
            RefreshInputBlock(null);
    }

    void SetAllowedClickCanvas(Canvas clickCanvas)
    {
        if (allowedClickCanvas == clickCanvas)
            return;

        if (allowedClickCanvas != null)
        {
            allowedClickCanvas.overrideSorting = false;
            activeHighlightCanvases.Remove(allowedClickCanvas);
        }

        allowedClickCanvas = clickCanvas;

        if (allowedClickCanvas != null)
            EnableCanvasHighlight(allowedClickCanvas);
    }

    void EndHigherSortHighlights()
    {
        for (int i = activeHighlightCanvases.Count - 1; i >= 0; i--)
        {
            var canvas = activeHighlightCanvases[i];
            if (canvas == null || canvas == allowedClickCanvas)
                continue;

            canvas.overrideSorting = false;
            activeHighlightCanvases.RemoveAt(i);
        }
    }

    void ClearAllowedClickCanvas()
    {
        SetAllowedClickCanvas(null);
    }

    void FinishTutorial()
    {
        ClearAllowedClickCanvas();
        CleanupCurrentLineState(restoreTime: true, removeBlocker: false);
        HideTutorialView();
        currentTutorialId = null;

        if (disableAllButtonsActive)
        {
            ShowBlockerOverlay();
            RefreshInputBlock(null);
        }
    }

    void MarkTutorialCompleted()
    {
        var meta = MetaSaveService.Load();
        if (meta.TutorialCompleted)
            return;

        meta.TutorialCompleted = true;
        MetaSaveService.Save(meta);
        enableTutorial = false;
    }

    void CleanupCurrentLineState(bool restoreTime, bool removeBlocker)
    {
        waitingForClick = false;
        waitingForLineInput = false;
        inTimePassGap = false;

        if (removeBlocker)
            ExecuteLogic("removeDisableAllButtons", true);
        else
            EndLine();

        if (restoreTime)
            Time.timeScale = resumeTimeScale;
    }

    void ExecuteLogic(string logic, bool apply)
    {
        if (!apply || string.IsNullOrEmpty(logic))
            return;

        if (logic == "addDisableAllButtons")
        {
            disableAllButtonsActive = true;
            ShowBlockerOverlay();
            return;
        }

        if (logic == "removeDisableAllButtons")
        {
            disableAllButtonsActive = false;
            ReleaseGameplayInput();
        }
    }

    void ReleaseGameplayInput()
    {
        ClearAllowedClickCanvas();
        ClearInputBlock();
        HideBlockerOverlay();
    }

    void ShowBlockerOverlay()
    {
        if (!disableAllButtonsActive || disableAllButton == null)
            return;

        disableAllButton.SetActive(true);
        EnsureBlockerOnTop();
    }

    void HideBlockerOverlay()
    {
        if (disableAllButton != null)
            disableAllButton.SetActive(false);
    }

    void RefreshInputBlock(Canvas allowedClickCanvas)
    {
        ClearInputBlock();
        if (!disableAllButtonsActive)
            return;

        var selectables = FindObjectsOfType<Selectable>(true);
        foreach (var selectable in selectables)
        {
            if (selectable == null || !selectable.interactable)
                continue;

            if (IsBlockerHit(selectable.transform))
                continue;

            if (allowedClickCanvas != null && IsUnderCanvas(selectable.transform, allowedClickCanvas))
                continue;

            selectable.interactable = false;
            disabledSelectables.Add(selectable);
        }
    }

    void ClearInputBlock()
    {
        for (int i = disabledSelectables.Count - 1; i >= 0; i--)
        {
            var selectable = disabledSelectables[i];
            if (selectable != null)
                selectable.interactable = true;
        }

        disabledSelectables.Clear();
    }

    void EnsureBlockerOnTop()
    {
        if (disableAllButton == null)
            return;

        var canvas = disableAllButton.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (disableAllButton.TryGetComponent<Image>(out var image))
            image.raycastTarget = true;
    }

    void EnableCanvasHighlight(Canvas canvas)
    {
        if (canvas == null || activeHighlightCanvases.Contains(canvas))
            return;

        ApplyTutorialOverrideSorting(canvas);
        activeHighlightCanvases.Add(canvas);
    }

    static void ApplyTutorialOverrideSorting(Canvas canvas)
    {
        if (canvas == null)
            return;

        canvas.overrideSorting = true;
        canvas.sortingLayerName = TutorialSortingLayer;
        canvas.sortingOrder = TutorialSortingOrder;
    }

    public void RegisterTutorialGameobject(TutorialGameobject target)
    {
        if (target == null || string.IsNullOrEmpty(target.Identifier))
            return;

        var canvas = target.Canvas;
        if (canvas == null)
            return;

        canvasByIdentifier[target.Identifier] = canvas;
    }

    public void UnregisterTutorialGameobject(TutorialGameobject target)
    {
        if (target == null || string.IsNullOrEmpty(target.Identifier))
            return;

        if (canvasByIdentifier.TryGetValue(target.Identifier, out var canvas)
            && canvas == target.Canvas)
            canvasByIdentifier.Remove(target.Identifier);
    }

    void EnsureAllTutorialTargetsRegistered()
    {
        var targets = FindObjectsOfType<TutorialGameobject>(true);
        for (int i = 0; i < targets.Length; i++)
            RegisterTutorialGameobject(targets[i]);
    }

    Canvas ResolveCanvas(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return null;

        canvasByIdentifier.TryGetValue(identifier, out var canvas);
        return canvas;
    }

    bool TryGetTopHitUnderClickTarget(List<RaycastResult> results, out GameObject hitObject)
    {
        hitObject = null;
        if (allowedClickCanvas == null || results == null || results.Count == 0)
            return false;

        for (int i = 0; i < results.Count; i++)
        {
            var hit = results[i].gameObject;
            if (hit == null)
                continue;

            if (IsUnderCanvas(hit.transform, allowedClickCanvas))
            {
                hitObject = hit;
                return true;
            }
        }

        return false;
    }

    void PropagateTopRaycastClick(List<RaycastResult> results)
    {
        if (results == null || results.Count == 0)
            return;

        for (int i = 0; i < results.Count; i++)
        {
            var hit = results[i].gameObject;
            if (hit == null || IsBlockerHit(hit.transform))
                continue;

            if (ShouldManuallyPropagateClick(results, hit))
                PropagatePointerClick(hit);
            return;
        }
    }

    bool ShouldManuallyPropagateClick(List<RaycastResult> results, GameObject targetHit)
    {
        if (targetHit == null || results == null || results.Count == 0)
            return false;

        var topHit = results[0].gameObject;
        if (topHit == null || IsBlockerHit(topHit.transform))
            return true;

        return !IsSameClickableTarget(topHit, targetHit);
    }

    static bool IsSameClickableTarget(GameObject a, GameObject b)
    {
        if (a == null || b == null)
            return false;

        var buttonA = a.GetComponentInParent<Button>();
        var buttonB = b.GetComponentInParent<Button>();
        if (buttonA != null && buttonB != null)
            return buttonA == buttonB;

        var selectableA = a.GetComponentInParent<Selectable>();
        var selectableB = b.GetComponentInParent<Selectable>();
        if (selectableA != null && selectableB != null)
            return selectableA == selectableB;

        return a == b;
    }

    bool TryRaycastUi(out List<RaycastResult> results)
    {
        results = raycastResults;
        results.Clear();

        if (EventSystem.current == null)
            return false;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition,
            button = PointerEventData.InputButton.Left
        };
        EventSystem.current.RaycastAll(pointerData, results);
        return true;
    }

    void PropagatePointerClick(GameObject hitObject)
    {
        if (hitObject == null || EventSystem.current == null)
            return;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition,
            button = PointerEventData.InputButton.Left
        };

        var button = hitObject.GetComponentInParent<Button>();
        if (button != null && button.interactable && button.gameObject.activeInHierarchy)
        {
            ExecuteEvents.Execute(button.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
            return;
        }

        var selectable = hitObject.GetComponentInParent<Selectable>();
        if (selectable != null && selectable.interactable && selectable.gameObject.activeInHierarchy)
            ExecuteEvents.Execute(selectable.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
    }

    static bool IsUnderCanvas(Transform transform, Canvas canvas)
    {
        if (transform == null || canvas == null)
            return false;

        return transform == canvas.transform || transform.IsChildOf(canvas.transform);
    }

    bool IsBlockerHit(Transform transform)
    {
        return disableAllButton != null
            && disableAllButton.activeInHierarchy
            && (transform == disableAllButton.transform || transform.IsChildOf(disableAllButton.transform));
    }

    void ShowTutorialView()
    {
        if (tutorialView != null)
            tutorialView.SetActive(true);
    }

    void HideTutorialView()
    {
        if (tutorialView != null)
            tutorialView.SetActive(false);
    }

    void OnDisable()
    {
        if (runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
            runningRoutine = null;
        }

        CleanupCurrentLineState(restoreTime: true, removeBlocker: true);
    }
}
