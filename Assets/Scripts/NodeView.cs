using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using TMPro;
using Coffee.UIEffects;
using Define;
using Effects;

public class NodeView : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
{
	private static GraphUIManager manager;
	private static RectTransform dragPlane;
	private static Canvas canvas;
	private static SelectionController selection;

	public TextMeshProUGUI titleText;
	public Image background;

	[Header("Dynamic Ports")]
	[SerializeField] private GameObject inputPortPrefab;
	[SerializeField] private GameObject outputPortPrefab;
	[SerializeField] private EffectBadgeView inputBadgePrefab;
	[SerializeField] private EffectBadgeView outputBadgePrefab;
	[SerializeField] private EffectBadgeView textBadgePrefab;

	[SerializeField] private RectTransform inputPortContainer;
	[SerializeField] private RectTransform outputPortContainer;
	[SerializeField] private RectTransform badgeContainer;
	[SerializeField] private NodeEffectController nodeEffectController;

	public List<PortView> inputPorts = new List<PortView>();
	public List<PortView> outputPorts = new List<PortView>();
	private readonly Dictionary<int, EffectBadgeView> typeBadges = new();

	[Header("UIEffect (Hover)")]
	public UIEffectTweener hoverTweener;

	[Header("UIEffect (Select)")]
	public UIEffectTweener selectTweener;

	[Header("Resource Transfer")]
	public float tokenTransferDuration = 1f;

	[Header("Production")]
	public Slider productionGauge;
	public float productionProgress { get; private set; } = 0f;
	public float productionTime { get; private set; } = 1f;
	bool isProducing = false;

	[Header("ResourceBufferStepper")]
	public Stepper InputStepper;
	public Stepper OutputStepper;

	public RectTransform rt { get; private set; }

	// ノードのメタデータ（複製用）
	public int nodeId { get; private set; }
	public int nodeLevel { get; private set; }

	private bool dragging;
	private bool nodeHoverOn;
	private int pendingHeightAdjustCounter;

	Camera CanvasCam =>
		(canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

	// 生産完了通知（追加出力/除去をNodeEffectController側で処理）
	public event System.Action<NodeView> ProductionCompleted;

	public void Setup(string title, GraphUIManager m, int[] inputTypes, int[] outputTypes, int[] inputValues, int[] outputValues, float productionTimeSec, int id, int level)
	{
		titleText.text = title;
		rt = GetComponent<RectTransform>();

		if (manager == null) manager = m;
		if (canvas == null) canvas = manager.canvas;
		if (selection == null) selection = SelectionController.Instance;
		if (dragPlane == null) dragPlane = selection.dragPlane;

		nodeId = id;
		nodeLevel = level;

		if (nodeId == NodeId.リソースバッファ)
		{
			SetResourceBufferButton();
		}

		// 入力ポートを生成
		ClearPorts(inputPorts);
		if (inputTypes != null)
		{
			for (int i = 0; i < inputTypes.Length; i++)
			{
				int requiredAmount = (inputValues != null && i < inputValues.Length) ? inputValues[i] : 1;
				int maxStock = requiredAmount; // 入力ポートの上限は必要数と同じ
				var port = CreatePort(true, inputTypes[i], 0, inputPortContainer, maxStock, requiredAmount);
				if (port != null) inputPorts.Add(port);
			}
		}

		// 出力ポートを生成
		ClearPorts(outputPorts);
		if (outputTypes != null)
		{
			for (int i = 0; i < outputTypes.Length; i++)
			{
				int produceAmount = (outputValues != null && i < outputValues.Length) ? outputValues[i] : 1;
				int maxStock = produceAmount * 10; // 出力ポートの上限は生産量の10倍
				var port = CreatePort(false, outputTypes[i], 0, outputPortContainer, maxStock, produceAmount);
				if (port != null) outputPorts.Add(port);
			}
		}

		resetLayout();
		SetProductionTime(productionTimeSec);

		hoverTweener?.PlayReverse();
		selectTweener?.PlayReverse();

		// 効果適用（ベースは今の値をキャプチャ）
		nodeEffectController.Setp();
		nodeEffectController.ApplyAll();
	}

	private void SetResourceBufferButton()
	{
		productionGauge.gameObject.SetActive(false);
		InputStepper.transform.parent.gameObject.SetActive(true);
		InputStepper.onValueChanged.AddListener((v) =>
		{
			if (v > inputPorts.Count)
			{
				// 新規入力ポート追加
				int requiredAmount = inputPorts.Count > 0 ? inputPorts[0].ProduceAmount : 1;
				int maxStock = inputPorts.Count > 0 ? inputPorts[0].MaxStock : 10;
				var port = CreatePort(true, ResourceId.全て, 0, inputPortContainer, maxStock, requiredAmount);
				port.SetSharedPort(inputPorts[0]);
				port.SetType(inputPorts[0].resourceType, true, true);
				port.RecalculateResourceBufferValues();
				inputPorts.Add(port);
			}
			else if (v < inputPorts.Count)
			{
				PortView deletePort = null;
				foreach (var port in inputPorts)
				{
					if (port.edges.Count == 0)
					{
						deletePort = port;
					}
				}
				if (deletePort == null)
				{
					deletePort = inputPorts.Last();
				}

				deletePort.RemoveEdgeAll();
				inputPorts.Remove(deletePort);
				deletePort.transform.parent.gameObject.SetActive(false);
				Destroy(deletePort.transform.parent.gameObject);
			}

			nodeEffectController?.ResetBaselines();
			nodeEffectController?.ApplyAll();

			GraphUIManager.Instance.MarkNodeDirty(this);
			resetLayout();
		});

		OutputStepper.onValueChanged.AddListener((v) =>
		{
			if (v > outputPorts.Count)
			{
				// 新規出力ポート追加
				int produceAmount = outputPorts.Count > 0 ? outputPorts[0].ProduceAmount : 1;
				int maxStock = outputPorts.Count > 0 ? outputPorts[0].MaxStock : 10;
				var port = CreatePort(false, ResourceId.全て, 0, outputPortContainer, maxStock, produceAmount);
				port.SetType(outputPorts[0].resourceType, true, true);
				port.RecalculateResourceBufferValues();
				outputPorts.Add(port);

			}
			else if (v < outputPorts.Count)
			{
				PortView deletePort = null;
				foreach (var port in outputPorts)
				{
					if (port.edges.Count == 0)
					{
						deletePort = port;
					}
				}
				if (deletePort == null)
				{
					deletePort = outputPorts.Last();
				}

				deletePort.RemoveEdgeAll();
				outputPorts.Remove(deletePort);
				deletePort.transform.parent.gameObject.SetActive(false);
				Destroy(deletePort.transform.parent.gameObject);
			}

			GraphUIManager.Instance.MarkNodeDirty(this);
			resetLayout();
		});
	}

	private void resetLayout()
	{
		pendingHeightAdjustCounter = 2;
	}

	void Update()
	{
		if (isProducing)
		{
			UpdateProduction();
		}
		else
		{
			TryStartProduction();
		}
		TryTransferResources();
	}

	void LateUpdate()
	{
		if (pendingHeightAdjustCounter > 0)
		{
			Canvas.ForceUpdateCanvases();

			// container の PreferredHeight を取得してノード高さへ反映
			var portRoot = (RectTransform)inputPortContainer.parent;
			LayoutRebuilder.ForceRebuildLayoutImmediate(portRoot);

			float h = LayoutUtility.GetPreferredHeight(portRoot);
			var self = (RectTransform)transform;
			self.sizeDelta = new Vector2(self.sizeDelta.x, 40f + h);

			pendingHeightAdjustCounter--;
		}
	}

	private void TryTransferResources()
	{
		foreach (var outputPort in outputPorts)
		{
			if (outputPort.Quantity <= 0) continue;

			foreach (var edge in outputPort.edges)
			{
				if (edge == null) continue;
				if (edge.toPort == null) continue;
				if (edge.isTransferring) continue;

				var targetNode = edge.toPort.GetParentNode();
				if (targetNode == null) continue;

				int requiredAmount = GetRequiredAmount(edge.toPort, targetNode);
				if (requiredAmount <= 0) continue;

				int transferAmount = Mathf.Min(outputPort.Quantity, requiredAmount);
				if (transferAmount <= 0) continue;

				outputPort.SetQuantity(outputPort.Quantity - transferAmount);
				edge.SpawnToken(tokenTransferDuration, transferAmount);
				break;
			}
		}
	}

	private int GetRequiredAmount(PortView inputPort, NodeView targetNode)
	{
		// ポートから直接必要数を取得
		int requiredAmount = inputPort.RequiredAmount;
		int currentQuantity = inputPort.SharedQuantity;
		int deficit = requiredAmount - currentQuantity;

		return deficit > 0 ? deficit : 0;
	}

	private void TryStartProduction()
	{
		if (CanProduce())
		{
			ConsumeInputs();
			StartProduction();
		}
	}

	private bool CanProduce()
	{
		if (inputPorts.Count == 0) return true;

		// ポートから直接必要数を取得
		foreach (var port in inputPorts)
		{
			if (port.IsShared) continue;
			if (port.Quantity < port.RequiredAmount)
			{
				return false;
			}
		}
		return true;
	}

	private void ConsumeInputs()
	{
		// ポートから直接必要数を取得して消費
		foreach (var port in inputPorts)
		{
			if (port.IsShared) continue;
			port.SetQuantity(port.Quantity - port.RequiredAmount);
		}
	}

	private void StartProduction()
	{
		if (productionTime > 0)
		{
			isProducing = true;
		}
		else
		{
			ProduceOutputs();
		}
		productionProgress = 0f;
		UpdateGauge();
	}

	private void UpdateProduction()
	{
		// 出力ストックが上限に達しているかチェック
		if (IsOutputStockFull())
		{
			// ストック満タン時はゲージを99%で停止
			productionProgress = Mathf.Min(productionProgress, productionTime * 0.99f);
			UpdateGauge();
			return;
		}

		productionProgress += Time.deltaTime;
		UpdateGauge();

		if (productionProgress >= productionTime)
		{
			ProduceOutputs();
			isProducing = false;
			productionProgress = 0f;
			UpdateGauge();
		}
	}

	private bool IsOutputStockFull()
	{
		// ポートから直接上限を取得
		foreach (var port in outputPorts)
		{
			if (port.IsStockFull() == false)
			{
				return false;
			}
		}
		return true;
	}

	private void ProduceOutputs()
	{
		// ポートから直接生産数と上限を取得
		foreach (var port in outputPorts)
		{
			int produceAmount = port.ProduceAmount;
			int newQuantity = Mathf.Min(port.Quantity + produceAmount, port.MaxStock);
			port.SetQuantity(newQuantity);

			// 素材の排出を記録（統計更新）— ノード種類別も記録
			if (produceAmount > 0 && !port.IsResourceBuffer)
			{
				UserData.Instance.RecordResourceOutput((int)port.resourceType, produceAmount, nodeId);
			}
		}

		// 生産完了イベント発火
		nodeEffectController.OnProductionCompleted();
		ProductionCompleted?.Invoke(this);
	}

	public int GetProduceAmount(int resourceId)
	{
		// ポートから直接生産数を取得
		foreach (var port in outputPorts)
		{
			if (port.IsResourceBuffer)
			{
				Debug.Log("リソースバッファの時はまだ未実装");
				continue;
			}
			if (port.resourceType == resourceId)
			{
				return port.ProduceAmount;
			}
		}
		return 0;
	}

	private void UpdateGauge()
	{
		float fillRatio = productionTime > 0 ? productionProgress / productionTime : 0f;
		productionGauge.value = fillRatio;
	}

	public void SetProductionTime(float time)
	{
		productionTime = Mathf.Max(0.1f, time);
	}

	/// <summary>
	/// ポートを生成（上限値と必要数/生産数を設定）
	/// </summary>
	private PortView CreatePort(bool isInput, int type, int quantity, RectTransform container, int maxStock, int requiredOrProduceAmount)
	{
		var obj = Instantiate(isInput ? inputPortPrefab : outputPortPrefab, container);
		var port = obj.GetComponentInChildren<PortView>();
		port.isInput = isInput;
		port.SetType(type);
		port.SetQuantity(quantity);
		port.Initialize(maxStock, requiredOrProduceAmount); // 上限値と必要数/生産数を初期化

		return port;
	}

	private void ClearPorts(List<PortView> ports)
	{
		foreach (var port in ports)
		{
			if (port != null && port.gameObject != null)
			{
				Destroy(port.transform.parent.gameObject);
			}
		}
		ports.Clear();
	}

	public void SetSelected(bool on)
	{
		if (on)
		{
			selectTweener?.PlayForward();
		}
		else
		{
			selectTweener?.PlayReverse();
		}
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		UpdateNodeHoverByPointer(eventData.position);
	}

	public void OnPointerMove(PointerEventData eventData)
	{
		UpdateNodeHoverByPointer(eventData.position);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		SetNodeHover(false);
	}

	void UpdateNodeHoverByPointer(Vector2 screenPos)
	{
		if (SelectionController.Instance.IsMarqueeActive)
		{
			SetNodeHover(false);
			return;
		}

		if (dragging)
		{
			SetNodeHover(false);
			return;
		}

		if (GraphUIManager.Instance.IsEdgeDragging)
		{
			SetNodeHover(false);
			return;
		}

		if (IsPointerOverOwnPort(screenPos))
		{
			SetNodeHover(false);
			return;
		}

		SetNodeHover(true);
	}

	void SetNodeHover(bool on)
	{
		if (nodeHoverOn == on) return;
		nodeHoverOn = on;
		if (on) hoverTweener?.PlayForward();
		else hoverTweener?.PlayReverse();
	}

	bool IsPointerOverOwnPort(Vector2 screenPos)
	{
		var mgr = manager;
		if (mgr == null || mgr.raycaster == null) return false;

		var ev = new PointerEventData(EventSystem.current) { position = screenPos };
		var results = new List<RaycastResult>();
		mgr.raycaster.Raycast(ev, results);

		foreach (var r in results)
		{
			if (!r.gameObject) continue;
			var pv = r.gameObject.GetComponentInParent<PortView>();
			if (pv == null) continue;
			var owner = pv.GetComponentInParent<NodeView>();
			if (owner == this) return true;
		}
		return false;
	}

	public void ForceHoverOff()
	{
		nodeHoverOn = false;
		hoverTweener?.PlayReverse();
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		TabController.Instance.Hide();

		if (eventData.button != PointerEventData.InputButton.Left)
		{
			return;
		}

		if (SelectionController.IsCtrlOrCmdPressed())
		{
			selection.ToggleSelect(this);
		}
		else if (selection.IsSelected(this) == false)
		{
			selection.SelectOnly(this);
		}
	}

	/// <summary>
	/// クリックイベント（右クリックメニュー用）
	/// </summary>
	public void OnPointerClick(PointerEventData eventData)
	{
		// 右クリック：コンテキストメニュー表示
		if (eventData.button == PointerEventData.InputButton.Right)
		{
			ShowContextMenu(eventData.position);
		}
	}

	/// <summary>
	/// 右クリックメニューを表示
	/// </summary>
	private void ShowContextMenu(Vector2 screenPosition)
	{
		// このノードが選択されていない場合、このノードのみを選択
		if (selection != null && !selection.IsSelected(this))
		{
			selection.SelectOnly(this);
		}

		// 購入可能かどうかをチェック（NodeCostDataが存在するか）
		bool canPurchase = false;
		var data = MasterData.Instance.NodeDatas.SelectId[nodeId];
		if (data.UnlockChapter * 100 + data.UnlockSection <= UserData.Instance.CurrentChapter * 100 + UserData.Instance.CurrentSection)
		{
			// TODO: 将来的にはここでコストが足りているかのチェックを実装
			canPurchase = true;
		}

		var items = new List<ContextMenuItem>
		{
			new ContextMenuItem("コピー (Ctrl+C)", () => selection?.CopySelectionToClipboard(), enabled: canPurchase),
			new ContextMenuItem("複製 (Ctrl+D)", () => selection?.DuplicateSelectionOnce(), enabled: canPurchase),
			ContextMenuItem.Separator(),
			new ContextMenuItem("削除 (Delete)", () => selection?.DeleteSelection(), enabled: canPurchase)
		};

		ContextMenuController.Instance.ShowMenu(screenPosition, items);
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		// 左クリック以外は無視
		if (eventData.button != PointerEventData.InputButton.Left)
		{
			return;
		}

		if (selection.IsSelected(this) == false)
		{
			OnPointerDown(eventData);
		}
		dragging = true;

		RectTransformUtility.ScreenPointToLocalPointInRectangle(dragPlane, eventData.position, CanvasCam, out var localPoint);
		selection.BeginGroupDrag(localPoint);
	}

	public void OnDrag(PointerEventData eventData)
	{
		// 左クリック以外は無視
		if (eventData.button != PointerEventData.InputButton.Left)
		{
			return;
		}
		if (!dragging)
		{
			return;
		}

		RectTransformUtility.ScreenPointToLocalPointInRectangle(dragPlane, eventData.position, CanvasCam, out var localPoint);
		selection.DragGroupTo(localPoint);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		// 左クリック以外は無視
		if (eventData.button != PointerEventData.InputButton.Left)
		{
			return;
		}
		dragging = false;
		selection.EndGroupDrag();
	}

	private Color ResColorForBucket(EffectTypeBucket b)
	{
		if (b.affectsAllResources || b.targetResources.Count == 0) return new Color(0.3f, 0.3f, 0.3f, 1f);
		int first = b.targetResources.First();
		return MasterData.Instance.ResourceDatas.SelectId[first].Color;
	}

	public void UpsertBadgeForType(EffectTypeBucket bucket)
	{
		int typeId = bucket.typeId;
		var kind = (EffectLogicalKind)typeId;
		EffectBadgeView view = null;

		if (typeBadges.TryGetValue(typeId, out var old) && old != null)
		{
			view = old;
		}
		switch (kind)
		{
			case EffectLogicalKind.Node_InputCostChange_Percent:
				if (view == null)
				{
					view = Instantiate(textBadgePrefab, badgeContainer);
				}
				view.Setup($"入力コスト {(MasterData.Instance.EffectTypeDatas.SelectTypeid[typeId].Evaluation * bucket.valueSum >= 0 ? "+" : "")}{MasterData.Instance.EffectTypeDatas.SelectTypeid[typeId].Evaluation * bucket.valueSum}％",seconds: bucket.durationSecSum);
				break;
			case EffectLogicalKind.Node_OutputValueChange_Percent:
				if (view == null)
				{
					view = Instantiate(textBadgePrefab, badgeContainer);
				}
				view.Setup($"出力 {(MasterData.Instance.EffectTypeDatas.SelectTypeid[typeId].Evaluation * bucket.valueSum >= 0 ? "+" : "")}{MasterData.Instance.EffectTypeDatas.SelectTypeid[typeId].Evaluation * bucket.valueSum}％", seconds: bucket.durationSecSum);
				break;
			case EffectLogicalKind.Node_AddInputResource:
				if (view == null)
				{
					view = Instantiate(inputBadgePrefab, inputPortContainer);

					var port = view.GetComponentInChildren<PortView>();
					port.isInput = true;
					port.SetType(bucket.targetResources.First());
					port.SetQuantity(0);
					port.Initialize(bucket.valueSum, bucket.valueSum);
					inputPorts.Add(port);
				}
				else
				{
					var port = view.GetComponentInChildren<PortView>();
					port.Initialize(bucket.valueSum, bucket.valueSum);
				}
				view.Setup($"追加入力 +{bucket.valueSum}", ResColorForBucket(bucket), seconds: bucket.durationSecSum);
				break;
			case EffectLogicalKind.Node_AddOutputResource:
				if (view == null)
				{
					view = Instantiate(outputBadgePrefab, outputPortContainer);

					var port = view.GetComponentInChildren<PortView>();
					port.isInput = false;
					port.SetType(bucket.targetResources.First());
					port.SetQuantity(0);
					port.Initialize(bucket.valueSum * 10, bucket.valueSum);
					outputPorts.Add(port);
				}
				else
				{
					var port = view.GetComponentInChildren<PortView>();
					port.Initialize(bucket.valueSum, bucket.valueSum);
				}
				view.Setup($"追加出力 +{bucket.valueSum}", ResColorForBucket(bucket), seconds: bucket.durationSecSum);
				break;
			case EffectLogicalKind.Node_RemoveByOutputCount:
				if (view == null)
				{
					view = Instantiate(textBadgePrefab, badgeContainer);
				}
				view.Setup($"あと {bucket.valueSum} 回の出力で除去", seconds: bucket.durationSecSum);
				break;
			default:
				if (view == null)
				{
					view = Instantiate(textBadgePrefab, badgeContainer);
				}
				view.Setup(bucket.displayName ?? kind.ToString(),seconds: bucket.durationSecSum);
				break;
		}
		if (view != null) typeBadges[typeId] = view;
	}

	public void UpdateRemoveByOutputCountBadge(int remaining)
	{
		int typeId = (int)EffectLogicalKind.Node_RemoveByOutputCount;
		EffectBadgeView view = null;

		// 既存バッジがあれば使いまわす
		if (typeBadges.TryGetValue(typeId, out var old) && old != null)
		{
			view = old;
		}
		else
		{
			view = Instantiate(textBadgePrefab, badgeContainer);
		}

		view.Setup($"あと {Mathf.Max(0, remaining)} 回の出力で除去");
		typeBadges[typeId] = view;
	}

	/// <summary>特定タイプのバッジに残り時間を反映（非表示条件なら timeLabel を隠す）</summary>
	public void UpdateBadgeTime(int typeId, float seconds)
	{
		if (typeBadges.TryGetValue(typeId, out var view) && view != null)
		{
			view.SetTimeSeconds(seconds);
		}
	}


	public void ClearAllBadges()
	{
		foreach (var kv in typeBadges)
		{
			if (kv.Value != null)
			{
				var port = kv.Value.gameObject.GetComponentInChildren<PortView>();
				if (port)
				{
					port.RemoveEdgeAll();
					if (inputPorts.Contains(port))
					{
						inputPorts.Remove(port);
					}
					if (outputPorts.Contains(port))
					{
						outputPorts.Remove(port);
					}
					port.transform.parent.gameObject.SetActive(false);
				}
				Destroy(kv.Value.gameObject);
			}
		}
		typeBadges.Clear();
		resetLayout();
	}

	// EffectTypeBucket の並びをそのまま UI に反映（全差し替え）
	public void SetBadges(IEnumerable<Effects.EffectTypeBucket> buckets)
	{
		ClearAllBadges();
		if (buckets == null) return;
		foreach (var b in buckets)
		{
			if (b == null) continue;
			UpsertBadgeForType(b);
		}
	}

	public void RemoveBadgeForType(int typeId)
	{
		if (typeBadges.TryGetValue(typeId, out var old) && old != null)
		{
			var port = old.gameObject.GetComponentInChildren<PortView>();
			if(port)
			{
				port.RemoveEdgeAll();
				if (inputPorts.Contains(port))
				{
					inputPorts.Remove(port);
				}
				if (outputPorts.Contains(port))
				{
					outputPorts.Remove(port);
				}
				port.transform.parent.gameObject.SetActive(false);
			}
			Destroy(old.gameObject);
		}
		typeBadges.Remove(typeId);
		resetLayout();
	}
}
