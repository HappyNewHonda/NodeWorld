
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;



public class ZoomPanController : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
	[Header("Targets")]
	public RectTransform graphRoot;   // ズーム対象（NodesLayerとEdgesLayerの親）
	public RectTransform viewport;

	// 表示領域（ScrollRectのViewportに相当）

	[Header("ズーム")]
	public float minScale = 0.5f;
	public float maxScale = 2.5f;
	public float zoomStep = 0.1f;

	[Header("画面端の余白")]
	public float padding = 0f;        // 端に余白を取りたい場合（px）

	[Header("慣性移動")]
	public bool inertiaEnabled = true;
	public float decelerationRate = 0.03f;   // Exponential: 1秒あたりの倍率（ScrollRect風）
	public float deceleration = 2000f;        // Linear: px/s^2
	public float maxSpeed = 4000f;            // 速度上限（px/s）
	public float stopSpeed = 5f;              // 停止閾値（px/s）
	public float velocityBlend = 0.5f;        // ドラッグ速度の平滑化（0..1）

	// 直近の移動判定に使う設定
	public float recentWindow = 0.12f;         // 例: 120ms以内に動いていれば慣性OK
	public float minVelocityEpsilon = 10f;     // 微小速度しきい値
	public float dragIdleDampRate = 0.05f;     // 例: 1秒で 5% に

	// ドラッグ移動用
	[Header("ドラッグ移動")]
	public float autoScrollPaddingPx = 48f;          // 端の反応幅（まずは 48〜64 が無難）
	public float autoScrollMaxSpeedPxPerSec = 800f;  // 最大スクロール速度（px/s）
	public AnimationCurve autoScrollCurve;           // 0..1 -> 0..1（未設定なら線形）

	// コントローラ
	SelectionController selection;
	GraphUIManager gui;

	// 直近移動時間
	float lastMoveTime;
	private bool isPanning = false;

	// ドラッグ状態
	Vector2 dragStartLocal;           // Viewportローカル座標のドラッグ開始点
	Vector2 contentStartPos;
	float lastDragTime;
	Vector2 lastDragLocal;

	// 慣性状態
	Vector2 velocity;                 // px/s（Viewportローカル座標系）
	bool coasting;

	Camera CanvasCam =>
		(GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay) ? null
		: GetComponentInParent<Canvas>().worldCamera;


	// 開始時の処理
	private void Start()
	{
		selection = SelectionController.Instance;
		gui = GraphUIManager.Instance;
	}

	// 更新
	void Update()
	{
		// 先に LMB オートスクロールを処理
		AutoScrollWhileLeftMouseDown();

		if (!coasting) return;

		// コンテンツがViewportより小さいなら中央固定・停止
		if (!HasRoomToPan())
		{
			graphRoot.anchoredPosition = Vector2.zero;
			coasting = false;
			velocity = Vector2.zero;
			return;
		}

		// 位置更新
		var pos = graphRoot.anchoredPosition + velocity * Time.unscaledDeltaTime;

		// 一旦位置を適用してからクランプ → 外向き成分をゼロに
		graphRoot.anchoredPosition = pos;
		var preClamp = graphRoot.anchoredPosition;
		var blocked = ClampToViewportWithBlocking(out bool blockX, out bool blockY);
		var postClamp = graphRoot.anchoredPosition;

		// 外に押し出していた軸の速度をゼロへ
		if (blockX)
		{
			// どちら側に当たったかは pos→postClamp の差で判定不要。外向きならとにかく0に。
			velocity.x = 0f;
		}
		if (blockY)
		{
			velocity.y = 0f;
		}

		// 減速
		// ScrollRect風：1秒で decelerationRate 倍。Δtで rate^Δt。
		float factor = Mathf.Pow(decelerationRate, Time.unscaledDeltaTime);
		velocity *= factor;

		// 停止判定
		if (velocity.magnitude < stopSpeed)
		{
			coasting = false;
			velocity = Vector2.zero;
		}
	}

	// 自動スクロール
	void AutoScrollWhileLeftMouseDown()
	{
		if (!Input.GetMouseButton(0)) return; // 左押下中のみ

		// 発火ガード：ノードドラッグ中 or エッジ接続ドラッグ中のときだけ
		bool draggingNodes = selection != null && selection.IsGroupDragging;
		bool draggingEdge = gui != null && gui.IsEdgeDragging;
		bool selectingMarquee = selection != null && selection.IsMarqueeActive;

		if (!(draggingNodes || draggingEdge || selectingMarquee)) return;

		// ビューポート/カメラが無い場合は中止
		if (viewport == null) return;

		// マウス位置をビューポートローカルへ
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
				viewport, Input.mousePosition, CanvasCam, out var local))
			return;

		var rect = viewport.rect; // pivot=0.5 を想定（前提通り）
		float hx = rect.width * 0.5f;
		float hy = rect.height * 0.5f;
		float pad = Mathf.Max(0.1f, autoScrollPaddingPx);

		// 端への寄り具合（-1..1）：右/上で正、左/下で負
		float fx = 0f, fy = 0f;
		if (local.x > hx - pad) fx = (local.x - (hx - pad)) / pad;
		else if (local.x < -hx + pad) fx = -((-hx + pad - local.x) / pad);

		if (local.y > hy - pad) fy = (local.y - (hy - pad)) / pad;
		else if (local.y < -hy + pad) fy = -((-hy + pad - local.y) / pad);

		fx = Mathf.Clamp(fx, -1f, 1f);
		fy = Mathf.Clamp(fy, -1f, 1f);
		if (Mathf.Approximately(fx, 0f) && Mathf.Approximately(fy, 0f)) return;

		// カーブ適用（未割当なら線形）
		float ax = autoScrollCurve != null ? autoScrollCurve.Evaluate(Mathf.Abs(fx)) : Mathf.Abs(fx);
		float ay = autoScrollCurve != null ? autoScrollCurve.Evaluate(Mathf.Abs(fy)) : Mathf.Abs(fy);

		// 速度（内容を動かす向き：右端=内容を左へ→負、左端=内容を右へ→正）
		float vmax = Mathf.Max(0f, autoScrollMaxSpeedPxPerSec);
		float vx = -Mathf.Sign(fx) * ax * vmax;
		float vy = -Mathf.Sign(fy) * ay * vmax;
		Vector2 delta = new Vector2(vx, vy) * Time.unscaledDeltaTime;

		// パン（Clamp あり・惰性無効化）
		PanBy(delta, clamp: true, cancelInertia: true);

		// ノード群がドラッグ中なら追従
		if (draggingNodes)
			selection?.NudgeGroupDragUnderMouse();

		// エッジ接続ドラッグ中ならプレビュー更新
		if (draggingEdge)
			gui.UpdateEdgeDrag(Input.mousePosition);

		// マルキー中なら矩形を再描画（マウスが静止でも伸縮を継続）
		if (selectingMarquee)
			selection?.NudgeMarqueeUnderMouseForPan();

	}

	public void OnScroll(PointerEventData eventData)
	{
		// 1) 親(Viewport)基準でマウス座標（ローカル）を取得
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			viewport, eventData.position, CanvasCam, out var centerLocal); // OverlayならCanvasCam=null 

		// 2) スケール変更
		var s = graphRoot.localScale.x;
		var delta = Mathf.Sign(eventData.scrollDelta.y) * zoomStep;
		var newScale = Mathf.Clamp(s + delta, minScale, maxScale);
		var scaleRatio = newScale / s;

		// Viewport中心固定の補正
		var oldPos = graphRoot.anchoredPosition;
		var offset = oldPos - centerLocal;
		var newPos = centerLocal + offset * scaleRatio;

		graphRoot.localScale = new Vector3(newScale, newScale, 1);
		graphRoot.anchoredPosition = newPos;

		// 4) ビューポート境界内へ即クランプ
		ClampToViewport();

		// ズームで慣性を止める
		coasting = false;
		velocity = Vector2.zero;
	}

public void OnPointerDown(PointerEventData eventData)
	{
		// タップした瞬間に慣性を停止
		coasting = false;
		velocity = Vector2.zero;
	}

// NodeまたはEdgeの上でクリックされたかを判定



	public void OnPointerUp(PointerEventData eventData)
	{
		// ドラッグが中ボタンだった場合のみ終了処理
		if (isPanning && eventData.button == PointerEventData.InputButton.Middle)
		{
			isPanning = false;
		}
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		// 中ボタン以外は無視
		if (eventData.button != PointerEventData.InputButton.Middle)
		{
			isPanning = false;
			return;
		}

		RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, CanvasCam, out dragStartLocal);

		isPanning = true;
		contentStartPos = graphRoot.anchoredPosition;
		lastDragLocal = dragStartLocal;
		lastDragTime = Time.unscaledTime;
		lastMoveTime = lastDragTime;
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (isPanning == false)
		{
			return;
		}

		RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, CanvasCam, out var curLocal);

		var deltaLocal = curLocal - dragStartLocal;
		graphRoot.anchoredPosition = contentStartPos + deltaLocal;

		// クランプ
		ClampToViewport();

		// 速度計算
		float now = Time.unscaledTime;
		float dt = Mathf.Max(1e-6f, now - lastDragTime);
		var frameDelta = curLocal - lastDragLocal;

		// 実質的に動いたときだけ速度を更新し、「最後に動いた時刻」を更新
		if (frameDelta.sqrMagnitude > 0.01f)      // ほぼ停止のフレームは無視
		{
			var instVel = frameDelta / dt;        // px/s
			instVel = Vector2.ClampMagnitude(instVel, maxSpeed);

			// 平滑化
			velocity = Vector2.Lerp(velocity, instVel, Mathf.Clamp01(velocityBlend));

			lastMoveTime = now;
		}
		else
		{
			// 指を止めているあいだ、ドラッグ中も速度を減衰させる
			float damp = Mathf.Pow(dragIdleDampRate, dt); // 例: 1秒で 5% へ
			velocity *= damp;
			if (velocity.magnitude < minVelocityEpsilon) velocity = Vector2.zero;
		}

		lastDragLocal = curLocal;
		lastDragTime = now;
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (isPanning == false)
		{
			return;
		}

		// 直近 recentWindow 秒以内に移動が無ければ、慣性開始しない
		bool movedRecently = (Time.unscaledTime - lastMoveTime) <= recentWindow;

		// 微小速度なら0扱い
		if (velocity.magnitude < Mathf.Max(stopSpeed, minVelocityEpsilon))
			velocity = Vector2.zero;

		if (!inertiaEnabled || !movedRecently || velocity == Vector2.zero)
		{
			coasting = false;
			velocity = Vector2.zero;
			return;
		}

		coasting = true;
		isPanning = false;
	}

	// クランプ（Pivot=0.5/Anchor=center前提）
	void ClampToViewport()
	{
		var pos = graphRoot.anchoredPosition;
		var (minX, maxX, minY, maxY, smallX, smallY) = ClampRanges();
		if (smallX) pos.x = 0f; else pos.x = Mathf.Clamp(pos.x, minX, maxX);
		if (smallY) pos.y = 0f; else pos.y = Mathf.Clamp(pos.y, minY, maxY);
		graphRoot.anchoredPosition = pos;
	}

	// クランプ＋「どの軸で衝突が起きたか」を返す
	bool ClampToViewportWithBlocking(out bool blockedX, out bool blockedY)
	{
		blockedX = blockedY = false;

		var before = graphRoot.anchoredPosition;
		var pos = before;

		var (minX, maxX, minY, maxY, smallX, smallY) = ClampRanges();

		if (smallX) { pos.x = 0f; }
		else
		{
			float clampedX = Mathf.Clamp(pos.x, minX, maxX);
			blockedX = !Mathf.Approximately(clampedX, pos.x);
			pos.x = clampedX;
		}

		if (smallY) { pos.y = 0f; }
		else
		{
			float clampedY = Mathf.Clamp(pos.y, minY, maxY);
			blockedY = !Mathf.Approximately(clampedY, pos.y);
			pos.y = clampedY;
		}

		graphRoot.anchoredPosition = pos;
		return blockedX || blockedY;
	}

	// 現在のサイズからクランプ範囲を直接算出
	(float minX, float maxX, float minY, float maxY, bool smallX, bool smallY) ClampRanges()
	{
		Vector2 vp = viewport.rect.size;
		Vector2 ct = graphRoot.rect.size * graphRoot.localScale.x;

		bool smallX = ct.x <= vp.x;
		bool smallY = ct.y <= vp.y;

		float rangeX = (ct.x - vp.x) * 0.5f;
		float rangeY = (ct.y - vp.y) * 0.5f;

		float minX = -rangeX + padding;
		float maxX = rangeX - padding;
		float minY = -rangeY + padding;
		float maxY = rangeY - padding;

		return (minX, maxX, minY, maxY, smallX, smallY);
	}

	bool HasRoomToPan()
	{
		Vector2 vp = viewport.rect.size;
		Vector2 ct = graphRoot.rect.size * graphRoot.localScale.x;
		return ct.x > vp.x || ct.y > vp.y;
	}


	public void PanBy(Vector2 delta, bool clamp = true, bool cancelInertia = true)
	{
		if (delta == Vector2.zero) return;
		graphRoot.anchoredPosition += delta;
		if (clamp) ClampToViewport();        // 既存のクランプを利用
		if (cancelInertia)
		{
			coasting = false;
			velocity = Vector2.zero;
		}
	}

	// 外部からクランプしたい場合用（既存の ClampToViewport を呼ぶ薄いラッパー）
	public void ClampNow()
	{
		ClampToViewport();
	}


#if UNITY_EDITOR
	void OnDrawGizmosSelected()
	{
		var rt = graphRoot;
		// nodeLayer のワールド四隅
		Vector3[] corners = new Vector3[4];
		rt.GetWorldCorners(corners);

		// 外枠：シアン
		Gizmos.color = Color.cyan;
		for (int i = 0; i < 4; i++)
		{
			var a = corners[i];
			var b = corners[(i + 1) % 4];
			Gizmos.DrawLine(a, b);
		}

		// 原点位置（親ローカル基準の pivot 点）もわかるように
		Gizmos.color = Color.yellow;
		Gizmos.DrawSphere(rt.TransformPoint(rt.rect.center), 6f);
	}
#endif
}

