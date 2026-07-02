using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CustomerView : MonoBehaviour
{
    CustomerData customer;
    Image patienceFill;
    TMP_Text orderText;

    public void Setup(CustomerData data, Vector2 position)
    {
        customer = data;
        transform.position = new Vector3(position.x, position.y, 0f);

        ColorSpriteFactory.CreateSquare("Body", transform, new Color(0.9f, 0.4f, 0.6f), new Vector2(0.8f, 0.8f));

        var canvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, 0.8f, 0f), new Vector2(220f, 120f));
        orderText = WorldUiFactory.CreateText(canvas.transform, "Order", customer.OrderName, new Vector2(0f, 30f), 28f, TextAlignmentOptions.Center);
        patienceFill = WorldUiFactory.CreateFillBar(canvas.transform, "Patience", new Vector2(0f, -10f), new Vector2(180f, 20f), new Color(0.2f, 0.8f, 0.3f));
        WorldUiFactory.CreateText(canvas.transform, "PatienceLabel", "Patience", new Vector2(0f, 15f), 18f, TextAlignmentOptions.Center);
    }

    public void Bind(CompositeDisposable disposables)
    {
        customer.Patience
            .Subscribe(p => patienceFill.fillAmount = Mathf.Clamp01(p / customer.MaxPatience))
            .AddTo(disposables);

        orderText.text = customer.OrderName;
    }
}
