using UniRx;
using UnityEngine;

public class ZoneData
{
    public ZoneType Type;
    public ReactiveProperty<int> WorkerCount = new ReactiveProperty<int>(0);
    public ReactiveProperty<float> TaskProgress = new ReactiveProperty<float>(0f);
    public ReactiveProperty<float> WorkSpeed = new ReactiveProperty<float>(0f);
    public ReactiveProperty<string> StatusText = new ReactiveProperty<string>("0%");

    public ZonePhase Phase = ZonePhase.Idle;
    public CustomerData DeliveryCustomer;

    public bool HasSharedItem;
    public FoodStage SharedItemStage = FoodStage.None;
    public FoodVisual SharedFoodVisual = FoodVisual.None;
    public string SharedItemId = "";            // 当前携带/加工物品的 identifier
    public string CurrentRecipeId;
    public Vector2 SharedItemPosition;
    public Vector2 SharedMoveTarget;
    public float WorkRotation;

    public float BaseDuration;
    public bool HasActiveStep;
    public FoodStage StepInput = FoodStage.None;
    public FoodStage StepOutput = FoodStage.None;
    public string StepOutputId = "";            // 当前步骤产出的 identifier
    public int SoloWorkerCount;
    public bool SpawnInputInZone;
    public bool ConsumeWorkerAsInput;
    public FoodVisual StepInputVisual = FoodVisual.None;
    public FoodVisual StepOutputVisual = FoodVisual.None;

    // 多原料取料状态：需要依次收集全部 FetchInputs 后才能开始加工。
    public System.Collections.Generic.List<StepInput> StepInputs = new System.Collections.Generic.List<StepInput>();
    public int FetchInputIndex;
    public System.Collections.Generic.List<CollectedInput> CollectedInputs = new System.Collections.Generic.List<CollectedInput>();

    public int CurrentOrderId;
    public int ActiveQueueIndex = -1;
    public FoodWorkStep ActiveWorkStep;
    public bool IsUnlocked = true;
    public System.Collections.Generic.List<ZoneQueueItem> TaskQueue = new System.Collections.Generic.List<ZoneQueueItem>();
    public System.Collections.Generic.List<ZoneOutputItem> OutputItems = new System.Collections.Generic.List<ZoneOutputItem>();
}
