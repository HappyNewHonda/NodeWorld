using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Define;

public class SelectionController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler, IPointerClickHandler
{
	public static SelectionController Instance;
	public GraphUIManager manager; // 割り当て必須
	public RectTransform dragPlane; // GraphRoot（グループドラッグの座標系）
	public Canvas canvas; // キー入力やフォーカス判定に使用
	public UIGraphGrid grid;

	[Header("Marquee (矩形選択)")]
	public RectTransform viewport; // Viewport（ローカル座標取得に使用）
	public GameObject marqueePrefab; // 選択範囲プレハブ
	public float clickThreshold = 5f; // クリック/ドラッグの閾値（px）

	[Header("コピ & ペースト")]
	public float pasteResetMoveThresholdPx = 2f; // 微小な揺れを無視するしきい値

	// 複数選択
	readonly HashSet<NodeView> marqueeHoverNodes = new();
	readonly HashSet<EdgeView> marqueeHoverEdges = new();
	readonly HashSet<NodeView> selectionNodes = new();
	readonly HashSet<EdgeView> selectionEdges = new();

	// グループドラッグ用（ノードごとのオフセット）
	readonly Dictionary<NodeView, Vector2> dragOffsets = new();
	bool groupDragging = false;
	public bool IsGroupDragging => groupDragging;

	// 矩形選択用
	bool marqueeActive = false;
	public bool IsMarqueeActive => marqueeActive;
	Vector2 pressScreen; // PointerDown時のスクリーン座標
	Vector2 pressLocalGraph; // PointerDown時のGraphRootローカル座標
	Image marquee; // 実体
	HashSet<NodeView> selectionAtPress = new(); // ドラッグ開始時の選択スナップショット

	// コピペ用 （既存のクリップボード構造は維持）
	class CopiedNodeInfo
	{
		public int nodeId;
		public int nodeLevel;
		public Vector2 relativePosition; // 選択中心からの相対位置
	}
	class CopiedEdgeInfo
	{
		public int fromNodeIndex; // clipboard内のインデックス
		public int fromPortIndex;
		public int toNodeIndex;   // clipboard内のインデックス
		public int toPortIndex;
	}
	List<CopiedNodeInfo> clipboard = new List<CopiedNodeInfo>();
	List<CopiedEdgeInfo> clipboardEdges = new List<CopiedEdgeInfo>();
	int pasteCount = 0; // 連続ペーストで段々ずらす用
	private bool waitMouseMoveToResetPaste = false;
	private Vector3 mousePosAtLastPaste;

	Camera Cam => (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

	// --- Blueprint types (新規追加) -------------------------------------------
	private struct NodeBlueprint
	{
		public int nodeId;
		public int nodeLevel;
		public Vector2 relativePosition;
	}
	private struct EdgeBlueprint
	{
		public int fromNodeIndex;
		public int fromPortIndex;
		public int toNodeIndex;
		public int toPortIndex;
	}
	private struct NodeSetBlueprint
	{
		public List<NodeBlueprint> nodes;
		public List<EdgeBlueprint> edges;
	}

	// ---- 公開API --------------------------------------------------------------
	public bool IsSelected(NodeView n) => selectionNodes.Contains(n);
	public void SelectOnly(NodeView n)
	{
		ClearAllSelection();
		Add(n);
	}
	public void ToggleSelect(NodeView n)
	{
		if (selectionNodes.Contains(n)) Remove(n);
		else Add(n);
	}
	public void Add(NodeView n)
	{
		if (selectionNodes.Add(n))
		{
			n.SetSelected(true);
			BringToFront(n);
		}
	}
	public void Remove(NodeView n)
	{
		if (selectionNodes.Remove(n)) n.SetSelected(false);
	}
	public void ClearSelectionNodes()
	{
		foreach (var n in selectionNodes) n.SetSelected(false);
		selectionNodes.Clear();
	}
	public void BringToFront(NodeView n)
	{
		if (n == null) return;
		var parent = n.rt != null ? (RectTransform)n.rt.parent : null;
		if (parent != null) n.transform.SetAsLastSibling();
	}

	public bool IsSelected(EdgeView e) => selectionEdges.Contains(e);
	public void SelectOnly(EdgeView n)
	{
		ClearAllSelection();
		Add(n);
	}
	public void ToggleSelect(EdgeView e)
	{
		if (selectionEdges.Contains(e)) Remove(e);
		else Add(e);
	}
	public void Add(EdgeView e)
	{
		if (selectionEdges.Add(e))
		{
			e.SetSelected(true);
			BringToFront(e);
		}
	}
	public void Remove(EdgeView e)
	{
		if (selectionEdges.Remove(e)) e.SetSelected(false);
	}
	public void ClearSelectionEdges()
	{
		foreach (var e in selectionEdges) e.SetSelected(false);
		selectionEdges.Clear();
	}
	public void BringToFront(EdgeView e)
	{
		if (e == null) return;
		var parent = e.transform.parent as RectTransform;
		if (parent != null) e.transform.SetAsLastSibling();
	}
	public void ClearAllSelection()
	{
		ClearSelectionNodes(); // ノード解除
		ClearSelectionEdges(); // エッジ解除
	}

	/// <summary>起動時の処理</summary>
	SelectionController()
	{
		Instance = this;
	}

	/// <summary>更新処理</summary>
	void Update()
	{
		// テキスト入力中は削除を無視
		if (EventSystem.current != null)
		{
			var go = EventSystem.current.currentSelectedGameObject;
			if (go && (go.GetComponent<InputField>() != null
					   || go.GetComponent<TMPro.TMP_InputField>() != null))
			{
				return;
			}
		}

		// マウス移動で pasteCount リセット
		if (waitMouseMoveToResetPaste)
		{
			var cur = Input.mousePosition;
			if ((cur - mousePosAtLastPaste).sqrMagnitude > pasteResetMoveThresholdPx * pasteResetMoveThresholdPx)
			{
				pasteCount = 0;
				waitMouseMoveToResetPaste = false;
			}
		}

		if (IsCtrlOrCmdPressed())
		{
			if (Input.GetKeyDown(KeyCode.C))
			{
				CopySelectionToClipboard();
			}
			else if (Input.GetKeyDown(KeyCode.V))
			{
				PasteFromClipboard(selectNew: true);
			}
			else if (Input.GetKeyDown(KeyCode.D))
			{
				DuplicateSelectionOnce();
			}
		}

		if (Input.GetKeyDown(KeyCode.Delete)
			|| Input.GetKeyDown(KeyCode.Backspace))
		{
			DeleteSelection();
		}
	}

	// 削除処理
	public void DeleteSelection()
	{
		// 連鎖削除：エッジ単体
		var edges = new List<EdgeView>(selectionEdges);
		foreach (var e in edges)
		{
			manager.RemoveEdge(e);
		}
		selectionEdges.Clear();

		// 削除可能なノードを
		var creatableNodeIds = new List<int>();
		foreach (var data in MasterData.Instance.NodeDatas.data)
		{
			if (data.UnlockChapter * 100 + data.UnlockSection <= UserData.Instance.CurrentChapter * 100 + UserData.Instance.CurrentSection)
			{
				creatableNodeIds.Add(data.Id);
			}
		}

		// 連鎖削除：ノードと接続エッジ
		var list = new List<NodeView>(selectionNodes);
		foreach (var n in list)
		{
			if (creatableNodeIds.Contains(n.nodeId))
			{
				manager.RemoveNode(n);
				selectionNodes.Remove(n);
			}
			else
			{
				Debug.LogWarning($"[Delete] Skipping undeletable node '{n.titleText.text}' (nodeId: {n.nodeId})");
			}
		}
	}

	public void OnInitializePotentialDrag(PointerEventData eventData)
	{
		// 何もしない
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		// 左ボタンのみ扱う
		if (eventData.button != PointerEventData.InputButton.Left)
			return;

		pressScreen = eventData.position;

		// 空白ヒットかどうかを判定（NodeView / PortView / EdgeView 上なら矩形選択しない）
		bool hitOnNode = HitNodeOrPortOrEdge(eventData.position);

		// 空白でクリックされた場合、タブパネルを隠す
		if (!hitOnNode)
		{
			TabController.Instance.Hide();
		}

		// 空白であれば矩形選択モードへ
		if (!hitOnNode)
		{
			marqueeActive = true;
			selectionAtPress = new HashSet<NodeView>(selectionNodes);

			// 始点：GraphRoot（dragPlane）ローカルで保持
			RectTransformUtility.ScreenPointToLocalPointInRectangle(dragPlane, eventData.position, Cam, out pressLocalGraph);

			// 親は dragPlane（=GraphRoot）に統一
			if (marquee == null)
			{
				var obj = Instantiate(marqueePrefab, dragPlane);
				marquee = obj.GetComponent<Image>();
			}
			else if (marquee.rectTransform.parent != dragPlane)
			{
				marquee.rectTransform.SetParent(dragPlane, worldPositionStays: false);
			}

			marquee.enabled = true;
			SetMarqueeRectGraph(pressLocalGraph, pressLocalGraph);

			// 可視セットをクリア
			marqueeHoverNodes.Clear();
			marqueeHoverEdges.Clear();
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (!marqueeActive) return;

		RectTransformUtility.ScreenPointToLocalPointInRectangle(dragPlane, eventData.position, Cam, out var curLocalGraph);
		SetMarqueeRectGraph(pressLocalGraph, curLocalGraph);

		var min = Vector2.Min(pressLocalGraph, curLocalGraph);
		var max = Vector2.Max(pressLocalGraph, curLocalGraph);
		Rect rectGraph = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

		UpdateMarqueeHoverVisuals(rectGraph, pressScreen, eventData.position);
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		// クリック判定（小移動 & 空白 & 修飾なし）で選択解除
		if (!marqueeActive)
		{
			var dist = (eventData.position - pressScreen).magnitude;
			bool clicked = dist < clickThreshold;
			if (clicked && !HitNodeOrPortOrEdge(eventData.position) && !IsCtrlOrCmdPressed() &&
				!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
			{
				ClearAllSelection(); // 空白クリックで解除
			}
			return;
		}

		// 終点を GraphRoot(=dragPlane) ローカルで取得
		RectTransformUtility.ScreenPointToLocalPointInRectangle(dragPlane, eventData.position, Cam, out var curLocalGraph);

		var min = Vector2.Min(pressLocalGraph, curLocalGraph);
		var max = Vector2.Max(pressLocalGraph, curLocalGraph);
		Rect rectGraph = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

		var hitNodes = GatherNodesInRect(rectGraph);
		var hitEdges = GatherEdgesInRect(pressScreen, eventData.position, rectGraph);

		bool ctrlCmd = IsCtrlOrCmdPressed();
		bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

		if (ctrlCmd)
		{
			foreach (var n in hitNodes) ToggleSelect(n);
			foreach (var e in hitEdges) ToggleSelect(e);
		}
		else if (shift)
		{
			foreach (var n in hitNodes) Add(n);
			foreach (var e in hitEdges) Add(e);
		}
		else
		{
			ClearAllSelection();
			foreach (var n in hitNodes) Add(n);
			foreach (var e in hitEdges) Add(e);
		}

		if (marquee != null) marquee.enabled = false;
		marqueeActive = false;
		selectionAtPress?.Clear();
		ClearMarqueeHoverVisuals();
	}

	/// <summary>
	/// クリックイベント（空白右クリックメニュー用）
	/// </summary>
	public void OnPointerClick(PointerEventData eventData)
	{
		// 右クリック：購入メニュー表示
		if (eventData.button == PointerEventData.InputButton.Right)
		{
			// ノード、ポート、エッジ以外（空白）でのみメニュー表示
			if (!HitNodeOrPortOrEdge(eventData.position))
			{
				ShowPurchaseMenu(eventData.position);
			}
		}
	}

	/// <summary>購入可能なノードのメニューを表示</summary>
	private void ShowPurchaseMenu(Vector2 screenPosition)
	{
		var items = new List<ContextMenuItem>();
		bool canAddNode = NodeLimitController.Instance == null || NodeLimitController.Instance.CanAddNode();
		int remaining = NodeLimitController.Instance != null ? NodeLimitController.Instance.RemainingSlots() : 99;

		// 購入可能なノード（NodeCostDataが存在するもの）を収集
		var purchasableNodes = new List<(int nodeId, string displayName, int cost)>();
		foreach (var data in MasterData.Instance.NodeDatas.data)
		{
			if (data.UnlockChapter * 100 + data.UnlockSection <= UserData.Instance.CurrentChapter * 100 + UserData.Instance.CurrentSection)
			{
				int nodeId = data.Id;
				var costDataArray = MasterData.Instance.NodeCostDatas.SelectId[nodeId];
				var costData = costDataArray[0];

				purchasableNodes.Add((nodeId, data.DisplayName, costData.Cost));
			}
		}

		foreach (var nodeInfo in purchasableNodes)
		{
			string label = $"{nodeInfo.displayName} ($: {nodeInfo.cost})";
			int capturedNodeId = nodeInfo.nodeId;
			bool enabled = canAddNode && UserData.Instance.Money >= nodeInfo.cost;

			items.Add(new ContextMenuItem(label, () =>
			{
				RectTransformUtility.ScreenPointToLocalPointInRectangle(
					manager.nodeLayer, screenPosition, Cam, out var localPos);
				var snappedPos = SnapToGrid(localPos);
				var newNode = manager.CreateNodeFromDataWithCostCheck(capturedNodeId, 1, snappedPos);
				if (newNode != null)
				{
					SelectOnly(newNode);
				}
			}, enabled: enabled));
		}

		items.Add(ContextMenuItem.Separator());
		items.Add(new ContextMenuItem($"残り配置可能: {remaining}/{NodeLimitController.MAX_NODE_COUNT}", null, enabled: false));
		items.Add(new ContextMenuItem("ペースト (Ctrl+V)", () => PasteFromClipboard(), enabled: clipboard.Count > 0 && canAddNode));

		if (items.Count == 0)
		{
			Debug.LogWarning("[購入メニュー] 購入可能なノードがありません");
			return;
		}
		ContextMenuController.Instance.ShowMenu(screenPosition, items);
	}

	/// <summary>
	/// クリップボード内のノードをペーストする際に必要な合計コストを計算する
	/// </summary>
	public int CalculatePasteCost()
	{
		int totalCost = 0;
		foreach (var node in clipboard)
		{
			// ノードIDからコストデータを取得
			var costDataArray = MasterData.Instance.NodeCostDatas.SelectId[node.nodeId];
			if (costDataArray != null && costDataArray.Length > 0)
			{
				totalCost += costDataArray[0].Cost;
			}
		}
		return totalCost;
	}

	// 補助：Raycastでノード/ポート/エッジかどうか判定
	bool HitNodeOrPortOrEdge(Vector2 screenPos)
	{
		var ev = new PointerEventData(EventSystem.current) { position = screenPos };
		var results = new List<RaycastResult>();
		manager.raycaster.Raycast(ev, results);
		foreach (var r in results)
		{
			if (!r.gameObject) continue;
			if (r.gameObject.GetComponentInParent<NodeView>() != null) return true;
			if (r.gameObject.GetComponentInParent<PortView>() != null) return true;
			if (r.gameObject.GetComponentInParent<EdgeView>() != null) return true;
		}
		return false;
	}

	void SetMarqueeRectGraph(Vector2 aLocal, Vector2 bLocal)
	{
		if (marquee == null) return;
		var rt = marquee.rectTransform;
		var parent = (RectTransform)rt.parent;

		rt.anchorMin = rt.anchorMax = parent.pivot;
		rt.pivot = parent.pivot;

		var center = (aLocal + bLocal) * 0.5f;
		var size = new Vector2(Mathf.Abs(bLocal.x - aLocal.x), Mathf.Abs(bLocal.y - aLocal.y));
		rt.anchoredPosition = center;
		rt.sizeDelta = size;
	}

	// 補助：ノード矩形との重なり判定
	List<NodeView> GatherNodesInRect(Rect rectGraph)
	{
		var list = new List<NodeView>();
		foreach (Transform t in manager.nodeLayer)
		{
			var n = t.GetComponent<NodeView>();
			if (n == null) continue;

			var nrt = n.rt != null ? n.rt : n.GetComponent<RectTransform>();
			var nodeRect = RectTransformToRectIn(dragPlane, nrt);
			if (RectOverlaps(rectGraph, nodeRect))
				list.Add(n);
		}
		return list;
	}

	// NodeのRectTransformを dragPlane(=GraphRoot) ローカルのRectに変換
	Rect RectTransformToRectIn(RectTransform targetSpace, RectTransform node)
	{
		Vector3[] corners = new Vector3[4];
		node.GetWorldCorners(corners);

		Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
		Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
		for (int i = 0; i < 4; i++)
		{
			var screen = RectTransformUtility.WorldToScreenPoint(Cam, corners[i]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(targetSpace, screen, Cam, out var local);
			min = Vector2.Min(min, local);
			max = Vector2.Max(max, local);
		}
		return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
	}

	// AABB重なり
	bool RectOverlaps(Rect a, Rect b)
	{
		return a.xMin <= b.xMax && a.xMax >= b.xMin &&
			   a.yMin <= b.yMax && a.yMax >= b.yMin;
	}

	List<EdgeView> GatherEdgesInRect(Vector2 startPos, Vector2 endPos, Rect rectGraph)
	{
		var list = new List<EdgeView>();
		if (manager == null || manager.edgesLayer == null) return list;

		foreach (Transform t in manager.edgesLayer)
		{
			var e = t.GetComponent<EdgeView>();
			if (e == null || e.uiLine == null) continue;

			var pts = e.uiLine.Points;
			if (pts == null || pts.Length < 2) continue;

			if (e.HitTestScreen(startPos, Cam))
			{
				list.Add(e);
			}
			else if (e.HitTestScreen(endPos, Cam))
			{
				list.Add(e);
			}
			else if (EdgeIntersectsRectGraph(e, rectGraph))
			{
				list.Add(e);
			}
		}
		return list;
	}

	// EdgeViewのポリラインが矩形と交差/内包するか（GraphRootローカルで判定）
	bool EdgeIntersectsRectGraph(EdgeView edge, Rect rectGraph)
	{
		var rtEdge = (RectTransform)edge.transform;
		var pts = edge.uiLine.Points;
		if (pts == null || pts.Length < 2) return false;

		Vector2 prev = default;
		bool hasPrev = false;
		for (int i = 0; i < pts.Length; i++)
		{
			Vector3 w = rtEdge.TransformPoint(pts[i]);
			Vector2 p = WorldToLocalOn(dragPlane, w);
			if (rectGraph.Contains(p)) return true;

			if (hasPrev)
			{
				if (SegmentIntersectsRect(prev, p, rectGraph))
					return true;
			}
			prev = p; hasPrev = true;
		}
		return false;
	}

	// ワールド→任意RectTransformローカル
	Vector2 WorldToLocalOn(RectTransform target, Vector3 world)
	{
		var cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
		var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
		RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screen, cam, out var local);
		return local;
	}

	// 線分×矩形 交差（端点内 or 辺と交差）
	bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect r)
	{
		if (r.Contains(a) || r.Contains(b)) return true;

		if ((a.x < r.xMin && b.x < r.xMin)
		 || (a.x > r.xMax && b.x > r.xMax)
		 || (a.y < r.yMin && b.y < r.yMin)
		 || (a.y > r.yMax && b.y > r.yMax))
			return false;

		var rBL = new Vector2(r.xMin, r.yMin);
		var rBR = new Vector2(r.xMax, r.yMin);
		var rTL = new Vector2(r.xMin, r.yMax);
		var rTR = new Vector2(r.xMax, r.yMax);

		return SegmentsIntersect(a, b, rBL, rBR)
			|| SegmentsIntersect(a, b, rBR, rTR)
			|| SegmentsIntersect(a, b, rTR, rTL)
			|| SegmentsIntersect(a, b, rTL, rBL);
	}

	// 2D線分交差
	bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
	{
		float o1 = Cross(q1 - p1, p2 - p1);
		float o2 = Cross(q2 - p1, p2 - p1);
		float o3 = Cross(p1 - q1, q2 - q1);
		float o4 = Cross(p2 - q1, q2 - q1);

		if ((o1 * o2) < 0f && (o3 * o4) < 0f) return true;

		if (Mathf.Approximately(o1, 0f) && OnSegment(p1, p2, q1)) return true;
		if (Mathf.Approximately(o2, 0f) && OnSegment(p1, p2, q2)) return true;
		if (Mathf.Approximately(o3, 0f) && OnSegment(q1, q2, p1)) return true;
		if (Mathf.Approximately(o4, 0f) && OnSegment(q1, q2, p2)) return true;
		return false;
	}
	float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
	bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
	{
		return Mathf.Min(a.x, b.x) - 1e-6f <= p.x && p.x <= Mathf.Max(a.x, b.x) + 1e-6f &&
			   Mathf.Min(a.y, b.y) - 1e-6f <= p.y && p.y <= Mathf.Max(a.y, b.y) + 1e-6f;
	}

	// 矩形内の見た目をHoverに変更する
	void UpdateMarqueeHoverVisuals(Rect rectGraph, Vector2 rectStartScreen, Vector2 rectEndScreen)
	{
		var hitNodes = GatherNodesInRect(rectGraph);
		var hitEdges = GatherEdgesInRect(rectStartScreen, rectEndScreen, rectGraph);

		foreach (var n in hitNodes)
		{
			if (marqueeHoverNodes.Add(n))
				n?.hoverTweener?.PlayForward();
		}
		var nodesToRemove = marqueeHoverNodes.Where(n => !hitNodes.Contains(n)).ToList();
		foreach (var n in nodesToRemove)
		{
			n?.hoverTweener?.PlayReverse();
			marqueeHoverNodes.Remove(n);
		}

		foreach (var e in hitEdges)
		{
			if (marqueeHoverEdges.Add(e))
				e?.OnPointerEnter();
		}
		var edgesToRemove = marqueeHoverEdges.Where(e => !hitEdges.Contains(e)).ToList();
		foreach (var e in edgesToRemove)
		{
			e?.OnPointerExit();
			marqueeHoverEdges.Remove(e);
		}
	}

	// 矩形内のHoverを取り消す
	void ClearMarqueeHoverVisuals()
	{
		foreach (var n in marqueeHoverNodes) n?.hoverTweener?.PlayReverse();
		foreach (var e in marqueeHoverEdges) e?.OnPointerExit();
		marqueeHoverNodes.Clear();
		marqueeHoverEdges.Clear();
	}

	// グループドラッグ（NodeViewから呼ぶ）
	public void BeginGroupDrag(Vector2 pointerLocal)
	{
		dragOffsets.Clear();
		foreach (var n in selectionNodes)
		{
			dragOffsets[n] = n.rt.anchoredPosition - pointerLocal;
		}
		groupDragging = true;
	}

	public void DragGroupTo(Vector2 pointerLocal)
	{
		if (!groupDragging) return;
		foreach (var kv in dragOffsets)
		{
			var n = kv.Key;
			var offset = kv.Value;

			var desired = SnapToGrid(pointerLocal + offset);
			desired = ClampNodeInsideGraph(n, desired);

			n.rt.anchoredPosition = desired;
			manager.MarkNodeDirty(n);
		}
	}

	// 画面パン中にグループドラッグ中のノードを追従させる
	public void NudgeGroupDragUnderMouse()
	{
		if (!groupDragging) return;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(dragPlane, Input.mousePosition, Cam, out var localPoint);
		DragGroupTo(localPoint);
	}

	// ZoomPanController からパン直後に呼ばれる：現在のマウス位置で矩形を再描画
	public void NudgeMarqueeUnderMouseForPan()
	{
		if (!marqueeActive) return;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(dragPlane, Input.mousePosition, Cam, out var curLocalGraph);
		SetMarqueeRectGraph(pressLocalGraph, curLocalGraph);
	}

	// ノードRectが nodeLayer の内側に収まるように位置をクランプ
	public Vector2 ClampNodeInsideGraph(NodeView n, Vector2 wanted)
	{
		var nodeRT = n.rt != null ? n.rt : n.GetComponent<RectTransform>();
		var parentRT = (manager != null && manager.nodeLayer != null)
			? manager.nodeLayer
			: (RectTransform)nodeRT.parent;

		Rect parentRect = parentRT.rect;
		Rect nodeRect = nodeRT.rect;

		float minX = parentRect.xMin + nodeRect.width * nodeRT.pivot.x;
		float maxX = parentRect.xMax - nodeRect.width * (1f - nodeRT.pivot.x);
		float minY = parentRect.yMin + nodeRect.height * nodeRT.pivot.y;
		float maxY = parentRect.yMax - nodeRect.height * (1f - nodeRT.pivot.y);

#if UNITY_EDITOR
		// Debug 用出力は必要に応じて
		// bool isNodeLayer = ReferenceEquals(parentRT, manager.nodeLayer);
		// Debug.Log($"[ClampCheck] rangeX=[{minX:F1},{maxX:F1}] rangeY=[{minY:F1},{maxY:F1}] wanted=({wanted.x:F1},{wanted.y:F1})");
#endif
		return new Vector2(
			Mathf.Clamp(wanted.x, minX, maxX),
			Mathf.Clamp(wanted.y, minY, maxY)
		);
	}

	public void EndGroupDrag()
	{
		groupDragging = false;
		dragOffsets.Clear();
	}

	Vector2 SnapToGrid(Vector2 localPos)
	{
		float spacing = Mathf.Max(1e-4f, 25);
		float snappedX = Mathf.Round((localPos.x) / spacing) * spacing;
		float snappedY = Mathf.Round((localPos.y) / spacing) * spacing;
		return new Vector2(snappedX, snappedY);
	}

	// Ctrl/Cmd 押下判定
	public static bool IsCtrlOrCmdPressed()
	{
		return Input.GetKey(KeyCode.LeftControl)
			|| Input.GetKey(KeyCode.RightControl)
			|| Input.GetKey(KeyCode.LeftCommand)
			|| Input.GetKey(KeyCode.RightCommand);
	}

	// コピー
	public void CopySelectionToClipboard()
	{
		clipboard.Clear();
		clipboardEdges.Clear();
		if (selectionNodes.Count == 0) return;

		// 選択中心
		Vector2 center = Vector2.zero;
		foreach (var n in selectionNodes)
		{
			var rt = n.rt != null ? n.rt : n.GetComponent<RectTransform>();
			center += rt.anchoredPosition;
		}
		center /= Mathf.Max(1, selectionNodes.Count);

		// ノード -> インデックス
		var nodeToIndex = new Dictionary<NodeView, int>();

		foreach (var n in selectionNodes)
		{
			if (n.nodeId < 0)
			{
				Debug.LogWarning($"[Copy] Skipping node '{n.titleText.text}': nodeId is invalid");
				continue;
			}
			var rt = n.rt != null ? n.rt : n.GetComponent<RectTransform>();
			nodeToIndex[n] = clipboard.Count;
			clipboard.Add(new CopiedNodeInfo
			{
				nodeId = n.nodeId,
				nodeLevel = n.nodeLevel,
				relativePosition = rt.anchoredPosition - center
			});
		}

		// 選択ノード間のエッジ
		var processedEdges = new HashSet<EdgeView>();
		foreach (var n in selectionNodes)
		{
			foreach (var outPort in n.outputPorts)
				foreach (var edge in outPort.edges)
				{
					if (edge == null || processedEdges.Contains(edge)) continue;
					processedEdges.Add(edge);

					var fromNode = edge.fromPort?.GetComponentInParent<NodeView>();
					var toNode = edge.toPort?.GetComponentInParent<NodeView>();
					if (fromNode != null && toNode != null &&
						nodeToIndex.ContainsKey(fromNode) && nodeToIndex.ContainsKey(toNode))
					{
						int fromPortIndex = fromNode.outputPorts.IndexOf(edge.fromPort);
						int toPortIndex = toNode.inputPorts.IndexOf(edge.toPort);
						if (fromPortIndex >= 0 && toPortIndex >= 0)
						{
							clipboardEdges.Add(new CopiedEdgeInfo
							{
								fromNodeIndex = nodeToIndex[fromNode],
								fromPortIndex = fromPortIndex,
								toNodeIndex = nodeToIndex[toNode],
								toPortIndex = toPortIndex
							});
						}
					}
				}
		}

		pasteCount = 0;
		Debug.Log($"[Copy] Copied {clipboard.Count} nodes and {clipboardEdges.Count} edges to clipboard");
	}

	// ===== ここから統合ロジック（新規） ========================================

	/// <summary>現在の選択から Blueprint を構築（順序は安定化）</summary>
	private NodeSetBlueprint BuildBlueprintFromSelection(out List<NodeView> orderedSelection, out Vector2 center)
	{
		var bp = new NodeSetBlueprint
		{
			nodes = new List<NodeBlueprint>(),
			edges = new List<EdgeBlueprint>()
		};

		// 安定順序：位置→nodeId でソート
		orderedSelection = selectionNodes
			.OrderBy(n => (n.rt != null ? n.rt : n.GetComponent<RectTransform>()).anchoredPosition.x)
			.ThenBy(n => (n.rt != null ? n.rt : n.GetComponent<RectTransform>()).anchoredPosition.y)
			.ThenBy(n => n.nodeId)
			.ToList();

		if (orderedSelection.Count == 0)
		{
			center = Vector2.zero;
			return bp;
		}

		// 重心
		center = Vector2.zero;
		foreach (var n in orderedSelection)
		{
			var rt = n.rt != null ? n.rt : n.GetComponent<RectTransform>();
			center += rt.anchoredPosition;
		}
		center /= orderedSelection.Count;

		// ノード -> インデックス
		var map = new Dictionary<NodeView, int>(orderedSelection.Count);
		for (int i = 0; i < orderedSelection.Count; i++)
		{
			var n = orderedSelection[i];
			if (n.nodeId < 0) continue;

			var rt = n.rt != null ? n.rt : n.GetComponent<RectTransform>();
			map[n] = bp.nodes.Count;
			bp.nodes.Add(new NodeBlueprint
			{
				nodeId = n.nodeId,
				nodeLevel = n.nodeLevel,
				relativePosition = rt.anchoredPosition - center
			});
		}

		// エッジ（選択ノード間のみ）
		var seen = new HashSet<EdgeView>();
		foreach (var n in orderedSelection)
		{
			foreach (var outPort in n.outputPorts)
				foreach (var e in outPort.edges)
				{
					if (e == null || seen.Contains(e)) continue;
					seen.Add(e);

					var fromNode = e.fromPort?.GetComponentInParent<NodeView>();
					var toNode = e.toPort?.GetComponentInParent<NodeView>();
					if (fromNode == null || toNode == null) continue;
					if (!map.ContainsKey(fromNode) || !map.ContainsKey(toNode)) continue;

					int fromPortIndex = fromNode.outputPorts.IndexOf(e.fromPort);
					int toPortIndex = toNode.inputPorts.IndexOf(e.toPort);
					if (fromPortIndex < 0 || toPortIndex < 0) continue;

					bp.edges.Add(new EdgeBlueprint
					{
						fromNodeIndex = map[fromNode],
						fromPortIndex = fromPortIndex,
						toNodeIndex = map[toNode],
						toPortIndex = toPortIndex
					});
				}
		}
		return bp;
	}

	/// <summary>
	/// Blueprint を基準点へ展開して生成・配線・選択まで一括実行。
	/// duplicateSourceOrdered を指定すると、その順序で DuplicateNode を用いて生成（固有状態保持）。
	/// </summary>
	private (List<NodeView> nodes, List<EdgeView> edges) InstantiateBlueprint(
		NodeSetBlueprint bp,
		Vector2 placeBase,          // 配置基準（nodeLayerローカル、relativePositionの原点）
		bool selectNew,
		bool offsetByGridDiag,
		int offsetMultiplier,
		List<NodeView> duplicateSourceOrdered = null // デュプリ時のみ指定
	)
	{
		var newNodes = new List<NodeView>(bp.nodes.Count);
		var newEdges = new List<EdgeView>(bp.edges.Count);

		var diag = new Vector2(grid.spacingHigh, -grid.spacingHigh);
		var offset = offsetByGridDiag ? diag * Mathf.Max(0, offsetMultiplier) : Vector2.zero;

		// --- ノード生成 ---
		for (int i = 0; i < bp.nodes.Count; i++)
		{
			var info = bp.nodes[i];
			var wanted = SnapToGrid(placeBase + offset + info.relativePosition);

			NodeView node = null;

			// デュプリケート：固有状態を保持するため DuplicateNode を使用
			if (duplicateSourceOrdered != null && i < duplicateSourceOrdered.Count && duplicateSourceOrdered[i] != null)
			{
				// Duplicate は位置もコピーされがちなので、オフセットなしで複製 → 目的位置へ再配置
				node = manager.DuplicateNode(duplicateSourceOrdered[i], Vector2.zero);
				if (node != null)
				{
					node.rt.anchoredPosition = wanted;
					node.rt.anchoredPosition = ClampNodeInsideGraph(node, node.rt.anchoredPosition);
				}
			}
			else
			{
				// ペースト：データから生成
				node = manager.CreateNodeFromDataWithCostCheck(info.nodeId, info.nodeLevel, wanted);
				if (node != null)
				{
					node.rt.anchoredPosition = ClampNodeInsideGraph(node, node.rt.anchoredPosition);
				}
			}

			newNodes.Add(node); // null でも添字保持
		}

		// --- エッジ復元 ---
		foreach (var eInfo in bp.edges)
		{
			if (eInfo.fromNodeIndex < 0 || eInfo.fromNodeIndex >= newNodes.Count) continue;
			if (eInfo.toNodeIndex < 0 || eInfo.toNodeIndex >= newNodes.Count) continue;

			var fromNode = newNodes[eInfo.fromNodeIndex];
			var toNode = newNodes[eInfo.toNodeIndex];
			if (fromNode == null || toNode == null) continue;

			if (eInfo.fromPortIndex < 0 || eInfo.fromPortIndex >= fromNode.outputPorts.Count) continue;
			if (eInfo.toPortIndex < 0 || eInfo.toPortIndex >= toNode.inputPorts.Count) continue;

			var outPort = fromNode.outputPorts[eInfo.fromPortIndex];
			var inPort = toNode.inputPorts[eInfo.toPortIndex];

			if (!manager.CanConnect(outPort, inPort)) continue;

			// 入力は1本制約：既存を外す
			if (inPort.edges != null && inPort.edges.Count > 0)
			{
				var exist = new List<EdgeView>(inPort.edges);
				foreach (var ex in exist)
					if (ex != null) inPort.RemoveEdge(ex);
			}

			var edge = Instantiate(manager.edgePrefab, manager.edgesLayer);
			edge.Initialize(manager, isPreview: false);
			edge.BindPorts(outPort, inPort);
			newEdges.Add(edge);
		}

		// --- 選択更新 ---
		if (selectNew)
		{
			ClearSelectionNodes();
			ClearSelectionEdges();
			foreach (var n in newNodes) if (n != null) Add(n);
			foreach (var e in newEdges) if (e != null) Add(e);
		}

		return (newNodes, newEdges);
	}

	// ===== ペースト & デュプリケート（統合呼び出し） ============================
	void PasteFromClipboard(bool selectNew = true)
	{
		if (clipboard.Count == 0) return;

		// クリップボード -> Blueprint へ変換
		var bp = new NodeSetBlueprint
		{
			nodes = clipboard.Select(c => new NodeBlueprint
			{
				nodeId = c.nodeId,
				nodeLevel = c.nodeLevel,
				relativePosition = c.relativePosition
			}).ToList(),
			edges = clipboardEdges.Select(e => new EdgeBlueprint
			{
				fromNodeIndex = e.fromNodeIndex,
				fromPortIndex = e.fromPortIndex,
				toNodeIndex = e.toNodeIndex,
				toPortIndex = e.toPortIndex
			}).ToList()
		};

		// 配置基準：マウス位置（nodeLayer ローカル）
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			manager.nodeLayer, Input.mousePosition, Cam, out var placeBase);

		// 連続ペーストのずらし
		pasteCount = Mathf.Max(0, pasteCount + 1);

		var result = InstantiateBlueprint(
			bp,
			placeBase,
			selectNew,
			offsetByGridDiag: true,
			offsetMultiplier: pasteCount,
			duplicateSourceOrdered: null // ペーストはデータ生成
		);

		mousePosAtLastPaste = Input.mousePosition;
		waitMouseMoveToResetPaste = true;

#if UNITY_EDITOR
		Debug.Log($"[Paste] Pasted {result.nodes.Count(n => n != null)} nodes and {result.edges.Count} edges");
#endif
	}

	public void DuplicateSelectionOnce()
	{
		if (selectionNodes.Count == 0) return;

		// 選択 -> Blueprint（安定順序で構築）
		var bp = BuildBlueprintFromSelection(out var orderedSelection, out var center);

		// 配置基準：選択の重心（元位置 + 対角 1 ステップ分だけ全体移動）
		var placeBase = center;

		var result = InstantiateBlueprint(
			bp,
			placeBase,
			selectNew: true,
			offsetByGridDiag: true,
			offsetMultiplier: 1,               // 1 ステップだけズラす
			duplicateSourceOrdered: orderedSelection // DuplicateNode で固有状態を保持
		);

#if UNITY_EDITOR
		Debug.Log($"[Duplicate] Duplicated {result.nodes.Count(n => n != null)} nodes and {result.edges.Count} edges");
#endif
	}
}