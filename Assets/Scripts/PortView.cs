using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Coffee.UIEffects;
using Define;
using System.Linq;
using JetBrains.Annotations;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Android.Gradle.Manifest;

public class PortView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
	public bool isInput;
	public int resourceType { get; private set; } = ResourceId.全て;
	private int quantity;
	public int Quantity
	{
		get
		{
			return quantity;
		}
	}
	// ポートごとの上限・必要数・生産数
	private int maxStock = 1;
	public int MaxStock
	{
		get
		{
			return maxStock;
		}
	}
	private int requiredAmount = 1;  // 入力ポート用：必要素材数
	public int RequiredAmount
	{
		get
		{
			return requiredAmount;
		}
	}
	private int produceAmount = 1;   // 出力ポート用：生産数
	public int ProduceAmount
	{
		get
		{
			return produceAmount;
		}
	}

	[Header("Resource Name")]
	public TextMeshProUGUI resourceNameText;

	[Header("Icon Settings")]
	public Sprite[] resourceIcons;

	public List<EdgeView> edges = new();

	[Header("UIEffect (Hover)")]
	public UIEffectTweener hoverTweener;

	RectTransform rt;
	Image img;
	bool? isResourceBuffer = null;
	public bool IsResourceBuffer { get { return isResourceBuffer.Value; } }

	public enum HighlightState { Normal, Emphasize, Disabled }
	HighlightState currentHighlight = HighlightState.Normal;
	Color baseColor;

	private PortView sharedPort;
	public bool IsShared { get { return sharedPort != null; } }

	public int SharedQuantity { get { return IsShared ? sharedPort.Quantity : quantity; } }

	void Awake()
	{
		rt = GetComponent<RectTransform>();
		img = GetComponent<Image>();
	}

	void Start()
	{
		hoverTweener?.PlayReverse();
	}

	public NodeView GetParentNode()
	{
		return GetComponentInParent<NodeView>();
	}

	/// <summary>
	/// ポートの初期化（上限値・必要数・生産数を設定）
	/// </summary>
	public void Initialize(int maxStock, int requiredOrProduceAmount)
	{
		this.maxStock = Mathf.Max(0, maxStock);
		if (isInput)
		{
			this.requiredAmount = Mathf.Max(0, requiredOrProduceAmount);
		}
		else
		{
			this.produceAmount = Mathf.Max(0, requiredOrProduceAmount);
		}
		img = GetComponent<Image>();
		UpdateResourceNameText();
	}

	public void SetType(int type, bool useChangeChecke = false, bool isCreate = false)
	{
		if(useChangeChecke && resourceType == type)
		{
			return;
		}
		var beforeResourceType = resourceType;
		resourceType = type;

		if (isResourceBuffer == null)
		{
			isResourceBuffer = resourceType == ResourceId.全て;
		}
		// リソースバッファの設定
		else if (isResourceBuffer.Value && resourceType != beforeResourceType && isCreate == false)
		{
			var node = GetParentNode();
			if (isInput)
			{
				// 素材から全てに代わるときは、全てのポートから入力がないか確認して変更
				if (resourceType == ResourceId.全て)
				{
					foreach (var port in node.inputPorts)
					{
						// まだつながっているエッジがあればresourceTypeを戻して終了
						if(port.edges.Count > 0)
						{
							resourceType = beforeResourceType;
							return;
						}
					}
				}

				// インプットの入力を変更
				foreach (var port in node.inputPorts)
				{
					port.SetType(type, true);
				}
				// アウトプットをすべ手変更してつながっているエッジを削除
				foreach (var port in node.outputPorts)
				{
					port.SetType(type, true);
					port.RemoveEdgeAll();
				}
			}
		}

		// 色設設定
		baseColor = GetColorFromMasterData(resourceType);

		// 画像設定
		img.color = baseColor;
		img.raycastTarget = resourceType != ResourceId.全て || isInput;
		SetIconByResourceType(resourceType);

		// 値と名称クリア
		quantity = 0;
		UpdateResourceNameText();
	}

	public void SetQuantity(int value)
	{
		// シェアしているポートがあればそちらに増加値を追加して、自分を０にする。
		if (sharedPort)
		{
			sharedPort.SetQuantity(sharedPort.quantity + value);
			value = 0;
		}

		// 
		int oldQuantity = quantity;
		quantity = value;
		UpdateResourceNameText();

		// 資金ポートで、エッジが接続されていない場合は即座に回収
		if (resourceType == ResourceId.資金 && !isInput && edges.Count == 0)
		{
			int addedAmount = quantity - oldQuantity;
			if (addedAmount > 0)
			{
				CollectMoney(addedAmount);
			}
		}
	}

	/// <summary>
	/// リソースバッファのRequiredAmount/ProduceAmountを再計算
	/// </summary>
	public void RecalculateResourceBufferValues()
	{
		if (IsResourceBuffer == false) return;

		var node = GetParentNode();
		if (node == null) return;

		if (isInput)
		{
			// インプット: RequiredAmount = 全アウトプットのProduceAmountの合計
			int totalProduceAmount = 0;
			foreach (var outputPort in node.outputPorts)
			{
				totalProduceAmount += outputPort.ProduceAmount;
			}
			Initialize(totalProduceAmount, totalProduceAmount);

			// 接続元のリソースバッファにも伝播
			PropagateToSourceResourceBuffers();
		}
		else
		{
			// アウトプット: ProduceAmount = 接続先のRequiredAmountに変更
			int targetRequired = 0;
			foreach (var edge in edges)
			{
				if (edge?.toPort == null) continue;
				targetRequired += edge.toPort.RequiredAmount;
			}
			Initialize(targetRequired, targetRequired);
			UpdateResourceNameText();

			// 同じノードのインプットも再計算
			foreach (var inputPort in node.inputPorts)
			{
				inputPort.RecalculateResourceBufferValues();
			}
		}
		UpdateResourceNameText();
	}

	/// <summary>
	/// 接続元のリソースバッファに変更を伝播
	/// </summary>
	private void PropagateToSourceResourceBuffers()
	{
		foreach (var edge in edges)
		{
			if (edge?.fromPort == null) continue;

			var sourceNode = edge.fromPort.GetParentNode();
			if (sourceNode == null) continue;

			// 接続元がリソースバッファなら再計算を伝播
			if (edge.fromPort.IsResourceBuffer)
			{
				edge.fromPort.RecalculateResourceBufferValues();
			}
		}
	}

	public void SetSharedPort(PortView port)
	{
		sharedPort = port;
	}

	private Color GetColorFromMasterData(int type)
	{
		var resourceId = (int)type;
		return MasterData.Instance.ResourceDatas.SelectId[resourceId].Color;
	}

	private string GetNameFromMasterData(int type)
	{
		var resourceId = (int)type;
		return MasterData.Instance.ResourceDatas.SelectId[resourceId].DisplayName;
	}

	public void UpdateResourceNameText()
	{
		string name = GetNameFromMasterData(resourceType);

		if (resourceType == ResourceId.全て)
		{
			resourceNameText.text = name;
		}
		else if (sharedPort)
		{
			resourceNameText.text = name + " - <color=grey><i>shared</i></color>";
		}
		else if (isInput)
		{
			// 入力ポート: [ストック数/必要数]
			resourceNameText.text = $"{name} [{quantity}/{requiredAmount}]";
		}
		else
		{
			// 出力ポート: [ストック数/上限] ストック満タンなら赤表示
			bool isStockFull = quantity >= maxStock;
			if (isStockFull)
			{
				resourceNameText.text = $"{name} [<color=red>{quantity}</color>/{produceAmount}]";
			}
			else
			{
				resourceNameText.text = $"{name} [{quantity}/{produceAmount}]";
			}
		}
	}

	/// <summary>
	/// ストックが上限に達しているか
	/// </summary>
	public bool IsStockFull()
	{
		return quantity >= maxStock;
	}

	private void SetIconByResourceType(int type)
	{
		img.sprite = resourceIcons[type];

		// HoverエフェクトのImageにも同じSpriteを設定
		if (hoverTweener != null)
		{
			var hoverImage = hoverTweener.GetComponent<Image>();
			if (hoverImage != null)
			{
				hoverImage.sprite = img.sprite;
			}
		}
	}

	public void SetHighlightState(HighlightState state)
	{
		if (currentHighlight == state) return;
		currentHighlight = state;

		switch (state)
		{
			case HighlightState.Normal:
				img.color = baseColor;
				hoverTweener?.PlayReverse();
				transform.parent.GetComponent<Image>().enabled = true;
				break;

			case HighlightState.Emphasize:
				hoverTweener?.PlayForward();
				break;

			case HighlightState.Disabled:
				Color gray = Color.Lerp(baseColor, Color.black, 0.5f);
				hoverTweener?.PlayReverse();
				img.color = gray;
				transform.parent.GetComponent<Image>().enabled = false;
				break;
		}
	}

	public void BindEdge(EdgeView edge)
	{
		if (edges.Contains(edge) == false)
		{
			edges.Add(edge);
			edge.fromPort.RecalculateResourceBufferValues();
			edge.toPort.RecalculateResourceBufferValues();
		}
	}

	public void RemoveEdge(EdgeView edge)
	{
		if (edges.Contains(edge))
		{
			var fromPort = edge.fromPort;
			var toPort = edge.toPort;

			edge.UnbindPorts();
			fromPort.RecalculateResourceBufferValues();
			toPort.RecalculateResourceBufferValues();

			edges.Remove(edge);

			if (edge != null && edge.gameObject != null)
			{
				Destroy(edge.gameObject);
			}
		}
	}

	public void RemoveEdgeAll()
	{
		var _edges = edges.ToArray();
		foreach (var edge in _edges)
		{
			var fromPort = edge.fromPort;
			var toPort = edge.toPort;

			edge.UnbindPorts();
			fromPort.RecalculateResourceBufferValues();
			toPort.RecalculateResourceBufferValues();

			if (edge != null && edge.gameObject != null)
			{
				Destroy(edge.gameObject);
			}
		}
		edges.Clear();
	}

	public Vector3 PortWorldCenter()
	{
		return rt.TransformPoint(rt.rect.center);
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (GraphUIManager.Instance.IsEdgeDragging)
			return;

		hoverTweener?.PlayForward();

		// このポートから出ているエッジをノードより手前に表示
		BringEdgesToFront();
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (GraphUIManager.Instance.IsEdgeDragging)
			return;

		hoverTweener?.PlayReverse();

		// エッジを元の表示順に戻す
		ResetEdgesOrder();
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		TabController.Instance.Hide();

		if (eventData.button == PointerEventData.InputButton.Left)
		{
			// このポートに接続されているエッジを全て選択状態に
			if (edges != null && edges.Count > 0)
			{
				if (SelectionController.Instance != null)
				{
					bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

					if (isCtrlPressed)
					{
						// Ctrl押下時：トグル選択（全て選択済みなら解除、そうでなければ追加）
						bool allSelected = true;
						foreach (var edge in edges)
						{
							if (edge != null && !SelectionController.Instance.IsSelected(edge))
							{
								allSelected = false;
								break;
							}
						}

						if (allSelected)
						{
							// 全て選択済み → 全て解除
							foreach (var edge in edges)
							{
								if (edge != null)
								{
									SelectionController.Instance.Remove(edge);
								}
							}
						}
						else
						{
							// 一部または全て未選択 → 未選択のエッジを追加選択
							foreach (var edge in edges)
							{
								if (edge != null && !SelectionController.Instance.IsSelected(edge))
								{
									SelectionController.Instance.Add(edge);
								}
							}
						}
					}
					else
					{
						// Ctrl非押下時：既存の選択をクリアして、このポートのエッジのみを選択
						SelectionController.Instance.ClearAllSelection();

						foreach (var edge in edges)
						{
							if (edge != null)
							{
								SelectionController.Instance.Add(edge);
							}
						}
					}
				}
			}

			eventData.Use();
		}
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		GraphUIManager.Instance.BeginEdgeDrag(this);

		// エッジを元の表示順に戻す
		ResetEdgesOrder();
	}

	public void OnDrag(PointerEventData eventData)
	{
		GraphUIManager.Instance.UpdateEdgeDrag(eventData.position);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		GraphUIManager.Instance.EndEdgeDrag(eventData.position);
	}

	/// <summary>
	/// このポートに接続されたエッジを最前面に移動
	/// </summary>
	private void BringEdgesToFront()
	{
		if (edges == null || edges.Count == 0) return;
		if (GraphUIManager.Instance == null) return;

		foreach (var edge in edges)
		{
			if (edge == null) continue;

			// 選択されているかチェック
			bool isSelected = SelectionController.Instance.IsSelected(edge);

			// 選択されていないエッジのみホバー演出を解除
			if (!isSelected)
			{
				edge.ForceHoverOff();
			}

			// 選択されていないエッジのみ選択演出を解除
			if (!isSelected && edge.selectTweener != null)
			{
				edge.selectTweener.PlayReverse();
			}

			// エッジをnodeLayerに一時移動（ノードより手前に表示）
			edge.transform.SetParent(GraphUIManager.Instance.nodeLayer, worldPositionStays: true);
			edge.transform.SetAsLastSibling(); // nodeLayer内で最前面
		}
	}

	/// <summary>
	/// エッジの表示順を元に戻す（先頭に移動）
	/// </summary>
	private void ResetEdgesOrder()
	{
		if (edges == null || edges.Count == 0) return;
		if (GraphUIManager.Instance == null) return;

		foreach (var edge in edges)
		{
			if (edge == null) continue;

			// 選択されているかチェック
			bool isSelected = SelectionController.Instance.IsSelected(edge);

			// 選択されていないエッジのみホバー演出を解除
			if (!isSelected)
			{
				edge.ForceHoverOff();
			}

			// 選択されていないエッジのみ選択演出を解除
			if (!isSelected && edge.selectTweener != null)
			{
				edge.selectTweener.PlayReverse();
			}

			// エッジをedgesLayerに戻す（ノードより背面に表示）
			edge.transform.SetParent(GraphUIManager.Instance.edgesLayer, worldPositionStays: true);
		}
	}

	/// <summary>
	/// 資金を回収してUserDataに追加
	/// </summary>
	private void CollectMoney(int amount)
	{
		UserData.Instance.AddMoney(amount);
		Debug.Log($"[PortView] Collected ${amount} from port (Total: ${UserData.Instance.Money})");

		// 回収した分だけポートから減らす
		quantity -= amount;
		UpdateResourceNameText();
	}
}
