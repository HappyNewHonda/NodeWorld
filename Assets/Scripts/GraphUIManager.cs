
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Linq;
using Define;

public class GraphUIManager : MonoBehaviour
{
	public static GraphUIManager Instance;

	[Header("Scene refs")]
	public Canvas canvas;
	public RectTransform graphRoot;
	public RectTransform nodeLayer;      // Nodeをぶら下げる親
	public RectTransform edgesLayer;     // Edgeをぶら下げる親
	public GraphicRaycaster raycaster;   // UIヒットテスト用
	public RequestListManager requestListManager;

	[Header("Prefabs")]
	public NodeView nodePrefab;          // 下記 NodeView
	public EdgeView edgePrefab;          // 下記 EdgeView

	// Dirty管理
	readonly HashSet<NodeView> dirtyNodes = new();
	readonly HashSet<EdgeView> dirtyEdges = new();

	// ドラッグ中の仮エッジ
	EdgeView previewEdge;
	public bool IsEdgeDragging => previewEdge != null;
	PortView dragSourcePort;
	PortView hoverTargetPort; // プレビュー中に「今」マウスの下にあるPort

	// プレビュー中にハイライトしている全ポートを覚えておく
	readonly List<PortView> highlightedPorts = new();

	EdgeView hoveredEdge;   // 現在ホバー中（自前判定）

	Camera CanvasCam => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

	/// <summary>
	/// 起動時の処理
	/// </summary>
	GraphUIManager()
	{
		Instance = this;
	}

	/// <summary>
	/// 更新（エッジの右クリック検出）
	/// </summary>
	void Update()
	{
		// エッジの右クリック検出（Clickイベント：押して離した時）
		if (Input.GetMouseButtonUp(1) && hoveredEdge != null)
		{
			// マウスボタンを離した時にもまだホバー中ならメニュー表示
			hoveredEdge.OnRightClick(Input.mousePosition);
		}
	}


	/// <summary>
	/// 更新
	/// </summary>
	void LateUpdate()
	{
		if (dirtyEdges.Count > 0)
		{
			// まとめて更新（このフレームの最後）
			foreach (var e in dirtyEdges)
			{
				e.UpdateFromPorts();
			}

			// フラグをクリア
			dirtyEdges.Clear();
		}
		dirtyNodes.Clear();

		UpdateEdgeHoverUnderMouse();

		// Edgeドラッグ中は常に全NodeのHoverを強制OFF
		if (IsEdgeDragging)
		{
			ForceAllNodeHoversOff();
		}
	}

	void UpdateEdgeHoverUnderMouse()
	{
		// ドラッグ/プレビュー/矩形選択中はホバー演出を抑制
		if (SelectionController.Instance.IsGroupDragging ||
			SelectionController.Instance.IsMarqueeActive)
		{
			ClearEdgeHover();
			return;
		}

		if (IsEdgeDragging)
		{
			ClearEdgeHover();
			return;
		}
		if (edgesLayer == null)
		{
			ClearEdgeHover();
			return;
		}

		Vector2 mouse = Input.mousePosition;
		EdgeView hitEdge = null;

		// edgesLayer配下のEdgeViewを総当たり
		foreach (Transform t in edgesLayer)
		{
			var e = t.GetComponent<EdgeView>();
			if (e == null || e.uiLine == null) continue;

			if (e.HitTestScreen(mouse, CanvasCam))
			{
				hitEdge = e;
				break;
			}
		}

		// nodeLayer配下のEdgeViewも探す（ポートホバー時に移動したエッジ）
		if (hitEdge == null)
		{
			foreach (Transform t in nodeLayer)
			{
				var e = t.GetComponent<EdgeView>();
				if (e == null || e.uiLine == null) continue;

				if (e.HitTestScreen(mouse, CanvasCam))
				{
					hitEdge = e;
					break;
				}
			}
		}

		if (hitEdge != hoveredEdge)
		{
			// 以前のホバーを解除
			if (hoveredEdge != null)
			{
				hoveredEdge.OnPointerExit();
			}
			hoveredEdge = hitEdge;

			// 新しいホバーを適用
			if (hoveredEdge != null)
			{
				hoveredEdge.OnPointerEnter();
			}
		}
	}

	void ClearEdgeHover()
	{
		if (hoveredEdge != null)
		{
			hoveredEdge.OnPointerExit();
		}
		hoveredEdge = null;
	}

	// ノード削除（エッジも連鎖削除）
	public void RemoveNode(NodeView node)
	{
		if (node == null) return;

		// 入力側エッジの破棄
		foreach (var port in node.inputPorts)
		{
			port.RemoveEdgeAll();
		}

		// 出力側エッジの破棄
		foreach (var port in node.outputPorts)
		{
			port.RemoveEdgeAll();
		}

		// 最後にノード本体を破棄
		Destroy(node.gameObject);

	}

	// 動いたノードと、紐づいているエッジにDirtyフラグを立てる
	public void MarkNodeDirty(NodeView node)
	{
		if (dirtyNodes.Contains(node)) return;

		// このノードに紐づくエッジを収集
		foreach (var port in node.inputPorts)
		{
			foreach (var e in port.edges)
			{
				if (dirtyEdges.Contains(e) == false)
				{
					dirtyEdges.Add(e);
				}
			}
		}
		foreach (var port in node.outputPorts)
		{
			foreach (var e in port.edges)
			{
				if (dirtyEdges.Contains(e) == false)
				{
					dirtyEdges.Add(e);
				}
			}
		}
		dirtyNodes.Add(node);
	}

	// エッジ削除
	public void RemoveEdge(EdgeView edge)
	{

		if (edge == null) return;

		// エッジ上の全トークンを破棄（重要：ポート参照前に実行）
		var tokens = edge.GetComponentsInChildren<ResourceTokenView>();
		foreach (var token in tokens)
		{
			if (token != null && token.gameObject != null)
			{
				Destroy(token.gameObject);
			}
		}

		// どちらか生きている側から RemoveEdge を呼べば十分（内部で Unbind & Destroy 済）
		if (edge.fromPort != null)
		{
			edge.fromPort.RemoveEdge(edge);
		}
		else if (edge.toPort != null)
		{
			edge.toPort.RemoveEdge(edge);
		}
		else
		{
			// 両ポートが既に切れている（もしくは未バインド）の場合のみ保険で Destroy
			if (edge.gameObject != null) Destroy(edge.gameObject);
		}
	}

	// ポート色取得
	public Color GetPortColor(PortView port)
	{
		if (port == null) return Color.gray;

		var resourceId = (int)port.resourceType;
		return MasterData.Instance.ResourceDatas.SelectId[resourceId].Color;
	}

	public void BeginEdgeDrag(PortView fromPort)
	{
		dragSourcePort = fromPort;
		previewEdge = Instantiate(edgePrefab, nodeLayer);
		previewEdge.Initialize(this, isPreview: true);
		previewEdge.SetColor(GetPortColor(dragSourcePort), GetPortColor(dragSourcePort));

		EnterPortHighlightMode(dragSourcePort);

		// ドラッグ開始時にNodeのホバーをOFF
		ForceAllNodeHoversOff();
	}

	// Nodeのホバー状態をオフにする
	void ForceAllNodeHoversOff()
	{
		if (nodeLayer == null) return;
		foreach (Transform t in nodeLayer)
		{
			var n = t.GetComponent<NodeView>();
			if (n != null) n.ForceHoverOff();
		}
	}

	// ドラッグ中：ポインタ位置までプレビュー線を更新
	public void UpdateEdgeDrag(Vector2 screenPos)
	{
		if (previewEdge == null) return;

		// マウス下ポートを検出してハイライトを更新
		var targetPort = HitPortAt(screenPos);
		UpdatePortHoverHighlight(targetPort);

		// 始点: 出力ポート、終点: マウス位置（ローカル化）
		var worldStart = dragSourcePort.PortWorldCenter();
		var worldEnd = ScreenToWorldOn(edgesLayer, screenPos);
		previewEdge.RebuildCurve(worldStart + Vector3.right, worldEnd);
	}

	// ドロップ：入力ポートに落ちたか判定して確定
	public void EndEdgeDrag(Vector2 screenPos)
	{
		if (previewEdge == null) return;

		var targetPort = HitPortAt(screenPos);

		if (targetPort != null && CanConnect(dragSourcePort, targetPort))
		{
			// --- 方向を Output -> Input に正規化 ---
			PortView outPort, inPort;
			if (!dragSourcePort.isInput && targetPort.isInput)
			{
				// 元々の既存パス（Output→Input）
				outPort = dragSourcePort;
				inPort = targetPort;
			}
			else if (dragSourcePort.isInput && !targetPort.isInput)
			{
				// 逆向きで開始された場合（Input→Output）、向きを入れ替えて確定
				outPort = targetPort;
				inPort = dragSourcePort;
			}
			else
			{
				// ここに来るのは isInput==isInput のケースなので保険で弾く
				Destroy(previewEdge.gameObject);
				previewEdge = null;
				dragSourcePort = null;
				return;
			}

			// --- Inputのエッジを記憶 ---
			var edges = inPort.edges.ToArray();

			// --- 新しいエッジを確定生成（常に Output→Input で Bind） ---
			var edge = Instantiate(edgePrefab, edgesLayer);
			edge.Initialize(this, isPreview: false);
			edge.BindPorts(outPort, inPort);

			// --- Input は常に1本：既存エッジを全削除 ---
			foreach(var e in edges)
			{
				inPort.RemoveEdge(e);
			}
		}

		CleanupPreview();
		ExitPortHighlightMode();
	}

	// ポートのハイライト
	void EnterPortHighlightMode(PortView source)
	{
		highlightedPorts.Clear();
		var ports = nodeLayer.GetComponentsInChildren<PortView>(includeInactive: false);

		foreach (var p in ports)
		{
			if (p == null) continue;

			if (p == source)
			{
				// ドラッグ元は常に強調
				p.SetHighlightState(PortView.HighlightState.Emphasize);
			}
			else
			{
				// ベースライン：接続可→Normal、接続不可→Disabled（ノイズ低減）
				bool ok = CanConnect(source, p);
				p.SetHighlightState(ok ? PortView.HighlightState.Normal
									   : PortView.HighlightState.Disabled);
			}
			highlightedPorts.Add(p);
		}

		// 直近のホバーターゲットはリセット
		hoverTargetPort = null;
	}

	// ポートのハイライト状態の解除
	void ExitPortHighlightMode()
	{
		foreach (var p in highlightedPorts)
		{
			if (p != null) p.SetHighlightState(PortView.HighlightState.Normal);
		}
		highlightedPorts.Clear();
		hoverTargetPort = null; // 念のため
	}

	// プレビュー中の「今のターゲット」ハイライト更新
	void UpdatePortHoverHighlight(PortView target)
	{
		if (target == hoverTargetPort) return; // 変化なし

		// 直前ターゲットをベースラインへ戻す
		if (hoverTargetPort != null && hoverTargetPort != dragSourcePort)
		{
			bool okPrev = CanConnect(dragSourcePort, hoverTargetPort);
			hoverTargetPort.SetHighlightState(
				okPrev ? PortView.HighlightState.Normal : PortView.HighlightState.Disabled
			);
		}

		hoverTargetPort = target;

		// 新ターゲットが接続可能なら強調する
		if (hoverTargetPort != null && hoverTargetPort != dragSourcePort)
		{
			if (CanConnect(dragSourcePort, hoverTargetPort))
				hoverTargetPort.SetHighlightState(PortView.HighlightState.Emphasize);
			else
				hoverTargetPort.SetHighlightState(PortView.HighlightState.Disabled);
		}
	}

	// プレビュー状態の解除
	void CleanupPreview()
	{
		if (previewEdge != null && previewEdge.gameObject != null)
			Destroy(previewEdge.gameObject);
		previewEdge = null;
		dragSourcePort = null;
	}

	// UI上のポートをRaycastで検出（GraphicRaycaster）
	PortView HitPortAt(Vector2 screenPos)
	{
		var ev = new PointerEventData(EventSystem.current) { position = screenPos };
		var results = new List<RaycastResult>();
		raycaster.Raycast(ev, results);
		return results.Select(r => r.gameObject.GetComponent<PortView>())
					  .FirstOrDefault(p => p != null);
	}

	// Screen → World（RectTransform平面上のワールド座標に変換）
	public Vector3 ScreenToWorldOn(RectTransform rect, Vector2 screen)
	{
		// rect（=EdgesLayer 等）の平面上に投影したワールド座標を得る
		RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, screen, CanvasCam, out var world);
		return world;
	}

	// World → RectTransform local
	public Vector2 WorldToLocalOn(RectTransform rect, Vector3 world)
	{
		var screen = RectTransformUtility.WorldToScreenPoint(CanvasCam, world);
		RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, CanvasCam, out var local);
		return local;
	}

	// 接続出来るかどうか
	public System.Func<PortView, PortView, bool> CanConnect = (from, to) =>
	{
		// デバッグ表示
		Debug.Log(from.isInput + " : " + from.resourceType + " : " + from.GetParentNode() + " :: " + to.isInput + " : " + to.resourceType + " : " + to.GetParentNode());

		// 出力→入力のみ。資源タイプ一致（または入力がNone）を許容
		if (from.isInput == to.isInput) return false;
		if (from.GetParentNode() == to.GetParentNode()) return false;
		if (from.isInput == true && from.resourceType == ResourceId.全て) return true;
		if (from.isInput == false && from.resourceType == ResourceId.全て) return false;
		if (from.isInput == false && to.IsResourceBuffer) return true;
		return from.resourceType == to.resourceType;
	};

	/// <summary>
	/// データからノードを生成（可変数ポート）
	/// </summary>
	public NodeView CreateNodeFromData(int nodeId, int level, Vector2 position)
	{
		// ノード上限チェック
		if (NodeLimitController.Instance != null && !NodeLimitController.Instance.CanAddNode())
		{
			Debug.LogWarning($"[CreateNodeFromData] Node limit reached ({NodeLimitController.MAX_NODE_COUNT}). Cannot create more nodes.");
			return null;
		}

		// NodeDataを取得
		var nodeData = MasterData.Instance.NodeDatas.SelectId[nodeId];

		// NodeIoDataを取得
		var ioDataArray = MasterData.Instance.NodeIoDatas.SelectId[nodeId];

		// 指定レベルのIoDataを探す
		var ioData = ioDataArray.FirstOrDefault(io => io.Level == level);
		if (ioData == null)
		{
			Debug.LogError($"NodeIoData not found for ID: {nodeId}, Level: {level}");
			return null;
		}

		// ioDataの中身を表示
		Debug.Log($"Creating Node ID: {nodeId}, Level: {level}, Inputs: {string.Join(",", ioData.InputResourceTypes ?? new int[0])}, Outputs: {string.Join(",", ioData.OutputResourceTypes ?? new int[0])}");

		// ノードを生成
		var nodeObj = Instantiate(nodePrefab, nodeLayer);
		var nodeView = nodeObj.GetComponent<NodeView>();
		var rt = nodeView.GetComponent<RectTransform>();
		rt.anchoredPosition = position;

		// データからセットアップ
		nodeView.Setup(
			nodeData.DisplayName,
			this,
			ioData.InputResourceTypes ?? new int[0],
			ioData.OutputResourceTypes ?? new int[0],
			ioData.InputValues,
			ioData.OutputValues,
			ioData.OutputSec / 1000f,
			nodeId,
			level
		);

		return nodeView;
	}

	/// <summary>
	/// ノードを複製します（CreateNodeFromData経由で正しく生成）
	/// </summary>
	/// <param name="sourceNode">複製元ノード</param>
	/// <param name="offset">複製先のオフセット</param>
	/// <returns>生成されたノード、または複製不可の場合null</returns>
	public NodeView DuplicateNode(NodeView sourceNode, Vector2 offset)
	{
		if (sourceNode == null)
		{
			Debug.LogError("[DuplicateNode] sourceNode is null");
			return null;
		}

		// nodeIdが不明な場合は複製不可
		if (sourceNode.nodeId < 0)
		{
			Debug.LogWarning($"[DuplicateNode] Cannot duplicate node '{sourceNode.titleText.text}': nodeId is invalid");
			return null;
		}

		// NodeDataを取得して解放されているかチェック
		var data = MasterData.Instance.NodeDatas.SelectId[sourceNode.nodeId];
		if (data.UnlockChapter * 100 + data.UnlockSection <= UserData.Instance.CurrentChapter * 100 + UserData.Instance.CurrentSection)
		{
			// TODO: 将来的にはここでコスト支払い処理を実装
		}
		else
		{
			Debug.LogWarning($"[DuplicateNode] Cannot duplicate node '{sourceNode.titleText.text}': NodeCostData not found (ID: {sourceNode.nodeId})");
			return null;
		}

		// 複製元の位置 + オフセット
		var newPosition = sourceNode.rt.anchoredPosition + offset;

		// CreateNodeFromDataで正しく生成
		return CreateNodeFromData(sourceNode.nodeId, sourceNode.nodeLevel, newPosition);
	}

	/// <summary>
	/// ノードをコストチェックしてから生成します（Ctrl+V用）
	/// </summary>
	public NodeView CreateNodeFromDataWithCostCheck(int nodeId, int level, Vector2 position)
	{
		// ノード上限チェック
		if (NodeLimitController.Instance != null && !NodeLimitController.Instance.CanAddNode())
		{
			Debug.LogWarning($"[CreateNodeFromDataWithCostCheck] Node limit reached ({NodeLimitController.MAX_NODE_COUNT}).");
			return null;
		}

		// NodeDataを取得して解放されているかチェック
		var data = MasterData.Instance.NodeDatas.SelectId[nodeId];
		if (data.UnlockChapter * 100 + data.UnlockSection <= UserData.Instance.CurrentChapter * 100 + UserData.Instance.CurrentSection)
		{
			var costData = MasterData.Instance.NodeCostDatas.SelectId[nodeId][0];
			if (UserData.Instance.Money >= costData.Cost)
			{
				// お金を消費
				UserData.Instance.SpendMoney(costData.Cost);

				// CreateNodeFromDataで生成
				return CreateNodeFromData(nodeId, level, position);
			}
			Debug.LogWarning($"[CreateNodeFromDataWithCostCheck] Cannot create node: Money short");
		}
		else
		{
			Debug.LogWarning($"[CreateNodeFromDataWithCostCheck] Cannot create node: NodeCostData not found (ID: {nodeId})");
		}

		return null;
	}

	/// <summary>
	///  指定のノードIDからNodeViewを取得
	/// </summary>
	/// <param name="nodeId"></param>
	/// <returns></returns>
	public List<NodeView> GetNodesById(int nodeId)
	{
		var nodes = new List<NodeView>();
		foreach (Transform t in nodeLayer)
		{
			var node = t.GetComponent<NodeView>();
			if (node != null && node.nodeId == nodeId)
			{
				nodes.Add(node);
			}
		}
		return nodes;
	}

	/// <summary>
	/// 現在のグラフをセーブデータに変換
	/// </summary>
	public GraphSaveData SaveGraph()
	{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
		EffectLogger.Log(EffectLogger.Category.SaveLoad, "SaveGraph begin");
#endif
		// ユーザーデータを保存
		var saveData = new GraphSaveData();
		saveData.userData = UserData.Instance.GetSaveData();

		var nodeList = new List<NodeView>();
		var nodeToIndex = new Dictionary<NodeView, int>();

		// 移動中トークンの量を集計（各Inputポートごと）
		var tokensInTransit = CalculateTokensInTransit();

		foreach (Transform t in nodeLayer)
		{
			var node = t.GetComponent<NodeView>();
			if (node == null || node.nodeId < 0) continue;

			nodeToIndex[node] = nodeList.Count;
			nodeList.Add(node);

			var savedNode = new SavedNode
			{
				nodeId = node.nodeId,
				nodeLevel = node.nodeLevel,
				posX = node.rt.anchoredPosition.x,
				posY = node.rt.anchoredPosition.y,
				productionProgress = node.productionProgress,
				isProducing = false
			};

			// 投資額を保存
			if (node.investmentController != null)
			{
				savedNode.investmentAmount = node.investmentController.InvestmentAmount;
			}

			// inputの資源を保存
			savedNode.inputQuantities = new int[node.inputPorts.Count];
			for (int i = 0; i < node.inputPorts.Count; i++)
			{
				var port = node.inputPorts[i];
				int baseQuantity = port.Quantity;

				// このポート宛の移動中トークンが消えないようにInputに加算
				if (tokensInTransit.TryGetValue(port, out int transitAmount))
				{
					baseQuantity += transitAmount;
				}

				savedNode.inputQuantities[i] = baseQuantity;
			}
			// outputの資源を保存
			savedNode.outputQuantities = new int[node.outputPorts.Count];
			for (int i = 0; i < node.outputPorts.Count; i++)
			{
				savedNode.outputQuantities[i] = node.outputPorts[i].Quantity;
			}

			// ノードのローカル効果（合算スナップショット）
			var nec = node.GetComponent<NodeEffectController>();
			if (nec != null)
			{
				var effectIds = nec.GetLocalEffectIdsForSave().ToArray();
				savedNode.activeEffects = effectIds;

				// 状態を持つエフェクトの現在値も保存
				var states = nec.GetStatefulEffectStatesForSave()?.ToArray();
				if (states != null && states.Length > 0)
				{
					savedNode.effectStates = states;
				}
			}

			saveData.nodes.Add(savedNode);
		}

		var processedEdges = new HashSet<EdgeView>();
		foreach (var node in nodeList)
		{
			foreach (var outPort in node.outputPorts)
			{
				foreach (var edge in outPort.edges)
				{
					if (edge == null || processedEdges.Contains(edge)) continue;
					processedEdges.Add(edge);

					if (edge.fromPort == null || edge.toPort == null) continue;
					var fromNode = edge.fromPort?.GetComponentInParent<NodeView>();
					var toNode = edge.toPort?.GetComponentInParent<NodeView>();

					if (fromNode == null || toNode == null) continue;
					if (!nodeToIndex.ContainsKey(fromNode) || !nodeToIndex.ContainsKey(toNode)) continue;

					int fromPortIndex = fromNode.outputPorts.IndexOf(edge.fromPort);
					int toPortIndex = toNode.inputPorts.IndexOf(edge.toPort);

					if (fromPortIndex < 0 || toPortIndex < 0) continue;

					saveData.edges.Add(new SavedEdge
					{
						fromNodeIndex = nodeToIndex[fromNode],
						fromPortIndex = fromPortIndex,
						toNodeIndex = nodeToIndex[toNode],
						toPortIndex = toPortIndex
					});
				}
			}
		}

		// グローバル効果
		var globals = Effects.GlobalEffectController.Instance
						.EnumerateAllGlobalEffectsRaw()
						.Select(d => d.Id)
						.ToList();
		saveData.globalEffects = globals;
		
		var globalState = Effects.GlobalEffectController.Instance.GetGlobalStatesForSave()
			.Select(t => new SavedGlobalEffectState { typeId = t.typeId, durationLeftSec = t.sec })
			.ToList();
		saveData.globalEffectStates = globalState;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
		EffectLogger.Log(EffectLogger.Category.SaveLoad,
		$"SaveGraph nodes={saveData.nodes.Count} edges={saveData.edges.Count} " +
		$"globalEffects={(saveData.globalEffects == null ? 0 : saveData.globalEffects.Count)} " +
		$"globalEffectStates={(saveData.globalEffectStates == null ? 0 : saveData.globalEffectStates.Count)}");
#endif
		return saveData;
	}

	/// <summary>
	/// セーブデータからグラフを復元
	/// </summary>
	public void LoadGraph(GraphSaveData saveData)
	{
		if (saveData == null)
		{
			Debug.LogError("[LoadGraph] saveData is null");
			return;
		}

		ClearGraph();

		// ユーザーデータを復元
		UserData.Instance.LoadFromSaveData(saveData.userData);

		// ここでRequestItemViewを作り直す必要がある。今はプレハブが残っており、それが持っているRequestItemViewが更新されていない。

		var loadedNodes = new List<NodeView>();

		foreach (var savedNode in saveData.nodes)
		{
			var position = new Vector2(savedNode.posX, savedNode.posY);
			var node = CreateNodeFromData(savedNode.nodeId, savedNode.nodeLevel, position);

			if (node != null)
			{
				// 投資額を復元
				if (savedNode.investmentAmount > 0)
				{
					if (node.investmentController != null)
					{
						node.investmentController.SetInvestmentAmount(savedNode.investmentAmount);
					}
				}

				// input/outputの資源量を復元
				for (int i = 0; i < savedNode.inputQuantities.Length && i < node.inputPorts.Count; i++)
				{
					node.inputPorts[i].SetQuantity(savedNode.inputQuantities[i]);
				}
				for (int i = 0; i < savedNode.outputQuantities.Length && i < node.outputPorts.Count; i++)
				{
					node.outputPorts[i].SetQuantity(savedNode.outputQuantities[i]);
				}

				// ノードのローカル効果（1.1～）。nullセーフ（1.0互換）
				if (savedNode.activeEffects != null && savedNode.activeEffects.Length > 0)
				{
					var nec = node.GetComponent<NodeEffectController>();
					if (nec != null)
					{
						var effectDatas = savedNode.activeEffects
							.Where(id => MasterData.Instance.EffectDatas.SelectId.ContainsKey(id))
							.Select(id => MasterData.Instance.EffectDatas.SelectId[id]);
						nec.SetNodeEffects(effectDatas);

						// もし状態があれば、EffectData適用後に上書き
						if (savedNode.effectStates != null && savedNode.effectStates.Length > 0)
						{
							nec.ApplyStatefulEffectStates(savedNode.effectStates);
						}
					}
				}

				loadedNodes.Add(node);
			}
		}

		foreach (var savedEdge in saveData.edges)
		{
			if (savedEdge.fromNodeIndex < 0 || savedEdge.fromNodeIndex >= loadedNodes.Count) continue;
			if (savedEdge.toNodeIndex < 0 || savedEdge.toNodeIndex >= loadedNodes.Count) continue;

			var fromNode = loadedNodes[savedEdge.fromNodeIndex];
			var toNode = loadedNodes[savedEdge.toNodeIndex];

			if (savedEdge.fromPortIndex < 0 || savedEdge.fromPortIndex >= fromNode.outputPorts.Count) continue;
			if (savedEdge.toPortIndex < 0 || savedEdge.toPortIndex >= toNode.inputPorts.Count) continue;

			var outPort = fromNode.outputPorts[savedEdge.fromPortIndex];
			var inPort = toNode.inputPorts[savedEdge.toPortIndex];

			if (!CanConnect(outPort, inPort)) continue;

			inPort.RemoveEdgeAll();

			// エッジを作成（Initializeで自動的に非選択状態になる）
			var edge = Instantiate(edgePrefab, edgesLayer);
			edge.Initialize(this, isPreview: false);
			edge.BindPorts(outPort, inPort);
		}

		// グローバル効果
		if (saveData.globalEffects != null && saveData.globalEffects.Count > 0
			&& Effects.GlobalEffectController.Instance != null)
		{
			var globals = saveData.globalEffects
							.Where(id => MasterData.Instance.EffectDatas.SelectId.ContainsKey(id))
							.Select(id => MasterData.Instance.EffectDatas.SelectId[id]);
			Effects.GlobalEffectController.Instance.SetGlobalEffects(globals);
			Effects.GlobalEffectController.Instance.ApplyGlobalEffectStates(saveData.globalEffectStates);
		}

		// 依頼UIを現在の章・節に合わせて再構築
		if (requestListManager != null)
		{
			requestListManager.DisplayRequests(
				UserData.Instance.CurrentChapter,
				UserData.Instance.CurrentSection);
		}
		Debug.Log($"[LoadGraph] Loaded {loadedNodes.Count} nodes, {saveData.edges.Count} edges");
	}

	/// <summary>
	/// グラフ内の全ノードとエッジを削除
	/// </summary>
	public void ClearGraph()
	{
		if (edgesLayer != null)
		{
			var edgeList = new List<EdgeView>();
			foreach (Transform t in edgesLayer)
			{
				var edge = t.GetComponent<EdgeView>();
				if (edge != null) edgeList.Add(edge);
			}

			foreach (var edge in edgeList)
			{
				RemoveEdge(edge);
			}
		}

		if (nodeLayer != null)
		{
			var nodeList = new List<NodeView>();
			foreach (Transform t in nodeLayer)
			{
				var node = t.GetComponent<NodeView>();
				if (node != null) nodeList.Add(node);
			}

			foreach (var node in nodeList)
			{
				Destroy(node.gameObject);
			}
		}

		dirtyNodes.Clear();
		dirtyEdges.Clear();
		Debug.Log("[ClearGraph] Graph cleared");
	}

	/// <summary>
	/// 移動中の全トークンを出力ポートに戻す（セーブ前に呼ぶ）
	/// </summary>
	private Dictionary<PortView, int> CalculateTokensInTransit()
	{
		var tokensInTransit = new Dictionary<PortView, int>();
		if (edgesLayer == null) return tokensInTransit;

		int totalInTransit = 0;

		// 全EdgeViewを走査して、移動中トークンを集計（破棄はしない）
		foreach (Transform t in edgesLayer)
		{
			var edge = t.GetComponent<EdgeView>();
			if (edge == null) continue;

			// EdgeViewからResourceTokenViewを検索
			var tokens = edge.GetComponentsInChildren<ResourceTokenView>();
			foreach (var token in tokens)
			{
				if (token == null || token.sourceEdge == null) continue;

				// 目的地のInputポートを取得（Unity nullチェック + 破棄済みチェック）
				var destinationPort = token.sourceEdge.toPort;
				if (destinationPort == null) continue; // Unity の null チェック（破棄済みも含む）

				// さらに安全のため、GameObjectの有効性もチェック
				if (destinationPort.gameObject == null) continue;

				if (!tokensInTransit.ContainsKey(destinationPort))
				{
					tokensInTransit[destinationPort] = 0;
				}
				tokensInTransit[destinationPort] += token.amount;
				totalInTransit += token.amount;
			}
		}

		if (totalInTransit > 0)
		{
			Debug.Log($"[CalculateTokensInTransit] Found {totalInTransit} resources in transit (not destroyed, only counted)");
		}

		return tokensInTransit;
	}

	/// <summary>
	/// JSONファイルにセーブ
	/// </summary>
	public void SaveToFile(string filePath)
	{
		var saveData = SaveGraph();
		string json = JsonUtility.ToJson(saveData, prettyPrint: true);
		System.IO.File.WriteAllText(filePath, json);
		Debug.Log($"[SaveToFile] Saved to {filePath}");
	}

	/// <summary>
	/// JSONファイルからロード
	/// </summary>
	public void LoadFromFile(string filePath)
	{
		if (!System.IO.File.Exists(filePath))
		{
			Debug.LogError($"[LoadFromFile] File not found: {filePath}");
			return;
		}

		string json = System.IO.File.ReadAllText(filePath);
		var saveData = JsonUtility.FromJson<GraphSaveData>(json);
		LoadGraph(saveData);
		Debug.Log($"[LoadFromFile] Loaded from {filePath}");
	}

	/// <summary>
	/// セーブボタンから呼ぶ
	/// </summary>
	public void OnClickSaveButton()
	{
		string filePath = Application.persistentDataPath + "/graph_save.json";
		SaveToFile(filePath);
		Debug.Log($"[OnClickSaveButton] Game saved to: {filePath}");
	}

	/// <summary>
	/// ロードボタンから呼ぶ
	/// </summary>
	public void OnClickLoadButton()
	{
		string filePath = Application.persistentDataPath + "/graph_save.json";
		LoadFromFile(filePath);
		Debug.Log($"[OnClickLoadButton] Game loaded from: {filePath}");
	}

}
