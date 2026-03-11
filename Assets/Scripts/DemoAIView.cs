using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// デモ用AIキャラクター表示パネル。
/// ShowAI で生成されるプレハブにアタッチする。
/// 画像の切り替え、テキストの表示/非表示、パネル全体の表示/非表示を提供する。
/// </summary>
public class DemoAIView : MonoBehaviour
{
    [Header("AI Image")]
    [SerializeField] private Image aiImage;

    [Header("Text Bubble")]
    [SerializeField] private GameObject textBubble;
    [SerializeField] private TextMeshProUGUI aiText;

    [Header("Sprite")]
    [Tooltip("AI 画像Sprite")]
    [SerializeField] private Sprite[] sprites;

    // 現在のスプライト名をキャッシュ（同じ画像の再ロードを防ぐ）
    private string currentSpriteName;

    // ===== 画像 =====

    /// <summary>
    /// AI の表情画像をスプライト名で切り替える。
    /// Resources から読み込み、見つからなければ代替パスも試行する。
    /// </summary>
    public void SetImage(string spriteName)
    {
        if (aiImage == null)
        {
            Debug.LogWarning("[DemoAIView] aiImage is not assigned.");
            return;
        }

        if (string.IsNullOrEmpty(spriteName)) return;
        if (spriteName == currentSpriteName) return;

        foreach(var sprite in sprites)
        {
            if (sprite != null && sprite.name == spriteName)
            {
                aiImage.sprite = sprite;
                currentSpriteName = spriteName;
                return;
            }
		}
		Debug.LogWarning($"[DemoAIView] Sprite not found: '{spriteName}'");
	}

    /// <summary>
    /// AI の表情画像を Sprite 直接指定で切り替える。
    /// </summary>
    public void SetImage(Sprite sprite)
    {
        if (aiImage == null) return;
        if (sprite == null) return;
        aiImage.sprite = sprite;
        currentSpriteName = null;
    }

    /// <summary>
    /// AI 画像の表示/非表示を切り替える。
    /// </summary>
    public void SetImageVisible(bool visible)
    {
        if (aiImage != null)
        {
            aiImage.gameObject.SetActive(visible);
        }
    }

    // ===== テキスト =====

    /// <summary>
    /// テキストを設定し、テキストバブルを表示する。
    /// </summary>
    public void SetText(string text)
    {
        if (aiText != null)
        {
            aiText.text = text ?? "";
        }
        SetTextVisible(!string.IsNullOrEmpty(text));
    }

    /// <summary>
    /// 現在のテキストを取得する。
    /// </summary>
    public string GetText()
    {
        return aiText != null ? aiText.text : "";
    }

    /// <summary>
    /// テキストバブルの表示/非表示を切り替える。
    /// </summary>
    public void SetTextVisible(bool visible)
    {
        if (textBubble != null)
        {
            textBubble.SetActive(visible);
        }
        else if (aiText != null)
        {
            aiText.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// テキストバブルが表示中か。
    /// </summary>
    public bool IsTextVisible()
    {
        if (textBubble != null) return textBubble.activeSelf;
        if (aiText != null) return aiText.gameObject.activeSelf;
        return false;
    }

    // ===== パネル全体 =====

    /// <summary>
    /// AIパネル全体の表示/非表示を切り替える。
    /// </summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>
    /// AIパネルが表示中か。
    /// </summary>
    public bool IsVisible()
    {
        return gameObject.activeSelf;
    }

    // ===== コンポーネント参照の外部アクセス =====

    /// <summary>Image コンポーネントを返す（DemoPlayer 既存処理との互換用）</summary>
    public Image GetAIImage() => aiImage;

    /// <summary>TextMeshProUGUI コンポーネントを返す（DemoPlayer 既存処理との互換用）</summary>
    public TextMeshProUGUI GetAIText() => aiText;
}
