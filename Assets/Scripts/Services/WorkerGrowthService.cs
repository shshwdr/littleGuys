public class WorkerGrowthService
{
    readonly GameModel model;

    public WorkerGrowthService(GameModel model)
    {
        this.model = model;
    }

    public void Tick(float dt)
    {
        if (model.State.Value != GameState.Playing)
            return;

        foreach (var worker in model.Workers)
        {
            if (!worker.IsSmall && worker.RemainingGrowTime <= 0f)
                continue;

            worker.RemainingGrowTime -= dt;
            if (worker.RemainingGrowTime > 0f)
                continue;

            worker.RemainingGrowTime = 0f;
            worker.IsSmall = false;
            model.NotifyWorkerAssignmentChanged();
        }
    }
}
