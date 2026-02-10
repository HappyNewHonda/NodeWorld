// Assets/Scripts/Effects/EffectBadgeView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 単一行テキストの簡易バッジ
/// </summary>
public class EffectBadgeView : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI label;
	[SerializeField] private TextMeshProUGUI timeLabel;

	/// <summary>テキストと色を設定</summary>
	public void Setup(string text, Color? color = null, float seconds = -1)
    {
        if (label != null) label.text = text ?? "";
        if (background != null && color.HasValue) background.color = color.Value;
		SetTimeSeconds(seconds);
	}

	/// <summary>残り時間（秒）を mm:ss / h:mm:ss で表示。null or <=0 or 無期限は非表示</summary>
	public void SetTimeSeconds(float seconds)
	{
		if (timeLabel == null) return;
		if (seconds < 0f)
		{
			if (timeLabel.gameObject.activeSelf) timeLabel.gameObject.SetActive(false);
			return;
		}
		timeLabel.gameObject.SetActive(true);
		timeLabel.text = FormatTime(seconds);
	}

	static string FormatTime(float sec)
	{
		if (sec < 0f) return "";
		int total = Mathf.CeilToInt(sec);
		int h = total / 3600;
		int m = (total % 3600) / 60;
		int s = total % 60;
		if (h > 0) return $"{h}:{m:00}:{s:00}";
		return $"{m:00}:{s:00}";
	}

}