using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// デモ用メッセージ表示パネル。
/// ShowMessage で生成されるプレハブ、またはフォールバック生成されたオブジェクトにアタッチする。
/// テキストの変更と表示/非表示を提供する。
/// </summary>
public class DemoMessageView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Image backgroundImage;

    [Header("Animation (Optional)")]
    [SerializeField] private CanvasGroup canvasGroup;

    // ===== テキスト =====

    /// <summary>
    /// メッセージテキストを設定する。
    /// </summary>
    public void SetText(string text)
    {
        if (messageText != null)
        {
            messageText.text = text ?? "";
        }
    }

    /// <summary>
    /// 現在のメッセージテキストを取得する。
    /// </summary>
    public string GetText()
    {
        return messageText != null ? messageText.text : "";
    }

    // ===== 表示制御 =====

    /// <summary>
    /// メッセージパネル全体の表示/非表示を切り替える。
    /// </summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>
    /// メッセージパネルが表示中か。
    /// </summary>
    public bool IsVisible()
    {
        return gameObject.activeSelf;
    }

    /// <summary>
    /// CanvasGroup がアタッチされている場合、アルファで表示/非表示を制御する。
    /// フェードアニメーション等に利用可能。
    /// </summary>
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.interactable = alpha > 0.01f;
            canvasGroup.blocksRaycasts = alpha > 0.01f;
        }
    }

    /// <summary>
    /// 背景色を変更する。
    /// </summary>
    public void SetBackgroundColor(Color color)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = color;
        }
    }

    // ===== コンポーネント参照の外部アクセス =====

    /// <summary>TextMeshProUGUI を返す（DemoPlayer 既存処理との互換用）</summary>
    public TextMeshProUGUI GetMessageText() => messageText;
}
