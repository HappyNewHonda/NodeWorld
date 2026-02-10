using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 中ボタン（ホイールボタン）を押している間だけ Raycast を通す透明イメージ。
/// これで中ドラッグのイベントが必ずこのオブジェクトに届くようになる。
/// </summary>
[RequireComponent(typeof(Image))]
public class PanCatcherImage : Image, ICanvasRaycastFilter, IPointerDownHandler
{
	// 中ボタンを押している間のみ、Raycastを「通す（= true を返す）」
	public override bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
	{
		// マウスが無い環境は要件外なら false 固定でもOK。必要なら判定を拡張。
		return Input.GetMouseButton(2); // 2 = Middle
	}

	// クリックで選択
	public void OnPointerDown(PointerEventData eventData)
	{
		// タブパネルを隠す
		TabController.Instance.Hide();
	}

}
