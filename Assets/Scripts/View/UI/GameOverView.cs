using UniRx;
using UnityEngine;

public class GameOverView : MonoBehaviour
{
    bool goldSettled;

    public void Setup(GameModel model, GameBootstrap bootstrap, CompositeDisposable disposables)
    {
        model.State
            .Subscribe(state =>
            {
                if (state != GameState.GameOver)
                    return;

                string summary = SettleGold(model);
                bootstrap.EnterUpgradeMode(summary);
            })
            .AddTo(disposables);
    }

    string SettleGold(GameModel model)
    {
        if (goldSettled)
            return string.Empty;

        goldSettled = true;
        int runGold = model.Gold.Value;
        var meta = MetaSaveService.Load();
        meta.MetaGold += runGold;
        MetaSaveService.Save(meta);
        return $"This run: +{runGold} Gold\nTotal: {meta.MetaGold} Gold";
    }
}
