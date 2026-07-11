using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniRx;

public class CustomerData
{
    public int Id;
    public string Name;
    public string CustomerTypeId;
    public string Effect;
    public int EffectValue;
    public float EffectTimer;
    public bool IsEffectActive;
    public bool IsBoss;
    public ReactiveProperty<float> EffectProgress = new ReactiveProperty<float>(0f);
    public int SpawnSlotIndex = -1;
    public int RequiredSatiety = 1;
    public int ReceivedSatiety;
    // 已放到取餐位、正在等手取走的份额，用于避免同一顾客被重复投喂。
    public int PendingSatiety;
    public bool YummyMinionSatietyGranted;
    public float MaxPatience = 100f;
    public ReactiveProperty<float> Patience = new ReactiveProperty<float>(100f);
    public bool IsServed;
    public bool IsAwaitingEntrance;
    public bool IsExiting;
    public List<CustomerOrderItem> OrderedDishes = new List<CustomerOrderItem>();

    public bool IsInSilhouettePerformance => IsAwaitingEntrance || IsExiting;

    public string OrderLabel => BuildOrderLabel(false);
    public bool IsFullySatiated => ReceivedSatiety >= RequiredSatiety;

    public string BuildOrderLabel(bool useRichText)
    {
        if (OrderedDishes.Count == 0)
            return useRichText
                ? $"<b>{Name}</b>\nFull {ReceivedSatiety}/{RequiredSatiety}"
                : $"{Name}\nFull {ReceivedSatiety}/{RequiredSatiety}";

        var sb = new StringBuilder();
        if (useRichText)
            sb.Append("<b>").Append(Name).Append("</b>\n");
        else
            sb.Append(Name).Append('\n');

        for (int i = 0; i < OrderedDishes.Count; i++)
        {
            if (i > 0)
                sb.Append(useRichText ? "  " : ", ");

            var dish = OrderedDishes[i];
            string displayName = string.IsNullOrEmpty(dish.DisplayName) ? dish.RecipeId : dish.DisplayName;
            if (useRichText)
            {
                string color = dish.IsDelivered ? "#44DD66" : "#FF4444";
                sb.Append("<color=").Append(color).Append(">").Append(displayName).Append("</color>");
            }
            else
            {
                sb.Append(displayName);
            }
        }

        return sb.ToString();
    }
}
