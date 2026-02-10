using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

/// <summary>
/// 右クリックメニューの管理クラス
/// </summary>
public class ContextMenuController : MonoBehaviour
{
	public static ContextMenuController Instance { get; private set; }

	[Header("Menu UI")]
	public Canvas canvas;
	public RectTransform menuContainer;
	public GameObject menuItemTextPrefab; // TextMeshProUGUI付きプレハブ

	[Header("Menu Style")]
	public Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
	public Color borderColor = new Color(0.4f, 0.4f, 0.4f, 1f);
	public float borderWidth = 1f;
	public Color itemNormalColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
	public Color itemHighlightColor = new Color(0.3f, 0.3f, 0.3f, 0.95f);
	public Color itemPressedColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
	public Color separatorColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
	public float menuWidth = 200f;
	public float separatorHeight = 2f;

	private GameObject currentMenu;
	private RectTransform currentMenuRect;

	ContextMenuController()
	{
		Instance = this;
	}

	void Update()
	{
		// メニュー表示中にEscapeキーで閉じる
		if (currentMenu != null && currentMenu.activeSelf)
		{
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				HideMenu();
				return;
			}

			// 左/中/右クリック：メニュー外をクリックした場合のみ閉じる
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
			{
				if (!IsPointerOverMenu(Input.mousePosition))
				{
					HideMenu();
				}
			}
		}
	}

	/// <summary>
	/// メニューを表示
	/// </summary>
	/// <summary>
	/// メニューを表示
	/// </summary>
	public void ShowMenu(Vector2 screenPosition, List<ContextMenuItem> items)
	{
		if (items == null || items.Count == 0) return;

		if (canvas == null)
		{
			Debug.LogError("[ContextMenu] Canvas is not assigned!");
			return;
		}

		if (menuContainer == null)
		{
			Debug.LogError("[ContextMenu] menuContainer is not assigned!");
			return;
		}

		HideMenu();

		// メニュー本体を動的生成
		var result = CreateMenuObject();
		currentMenu = result.menuObject;
		currentMenuRect = currentMenu.GetComponent<RectTransform>();
		var itemContainer = result.itemContainer;

		// メニュー項目を追加
		foreach (var item in items)
		{
			CreateMenuItem(itemContainer, item);
		}

		// レイアウト計算のためにアクティブ化（Unityのレイアウトはアクティブなオブジェクトにのみ動作）
		currentMenu.SetActive(true);

		// レイアウト更新（複数回実行）
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)itemContainer);
		Canvas.ForceUpdateCanvases();

		// 各テキストの preferredWidth を計算して最大値を求める
		float maxTextWidth = menuWidth; // 最小幅
		foreach (Transform child in itemContainer)
		{
			var textComponent = child.GetComponentInChildren<TextMeshProUGUI>();
			if (textComponent != null)
			{
				// テキストの preferredWidth + 左右パディング(10x2)
				float textWidth = textComponent.preferredWidth + 20f;
				maxTextWidth = Mathf.Max(maxTextWidth, textWidth);
			}
		}

		// 計算した最大幅で各メニュー項目の preferredWidth を更新
		foreach (Transform child in itemContainer)
		{
			var layoutElement = child.GetComponent<LayoutElement>();
			if (layoutElement != null)
			{
				layoutElement.preferredWidth = maxTextWidth;
			}
		}

		// ItemContainerの幅を明示的に0に設定（anchorで決定）
		var containerRect = (RectTransform)itemContainer;
		containerRect.sizeDelta = new Vector2(0, containerRect.sizeDelta.y);

		// メニュー本体のサイズを計算した最大幅に設定
		float menuHeight = containerRect.sizeDelta.y + borderWidth * 2;
		float menuWidthWithBorder = maxTextWidth + borderWidth * 2;
		currentMenuRect.sizeDelta = new Vector2(menuWidthWithBorder, menuHeight);

		// 画面内に収まるように位置調整
		PositionMenu(screenPosition);

		// 一度非表示にしてから再表示（Buttonの色が正しく適用される）
		currentMenu.SetActive(false);
		currentMenu.SetActive(true);
	}

	/// <summary>
	/// メニューオブジェクトを動的生成
	/// </summary>
	private (GameObject menuObject, Transform itemContainer) CreateMenuObject()
	{
		var menuObj = new GameObject("ContextMenu", typeof(RectTransform));
		menuObj.SetActive(false); // 非アクティブ状態で作成
		menuObj.transform.SetParent(menuContainer, false);

		var menuRect = menuObj.GetComponent<RectTransform>();
		menuRect.anchorMin = Vector2.zero;
		menuRect.anchorMax = Vector2.zero;
		menuRect.pivot = new Vector2(0, 1);

		// 背景（外枠）
		var bgImage = menuObj.AddComponent<Image>();
		bgImage.color = borderColor;

		// 内側の背景用パネル
		var innerPanel = new GameObject("InnerPanel", typeof(RectTransform));
		innerPanel.transform.SetParent(menuObj.transform, false);
		var innerRect = innerPanel.GetComponent<RectTransform>();
		innerRect.anchorMin = Vector2.zero;
		innerRect.anchorMax = Vector2.one;
		innerRect.offsetMin = new Vector2(borderWidth, borderWidth);
		innerRect.offsetMax = new Vector2(-borderWidth, -borderWidth);

		var innerBg = innerPanel.AddComponent<Image>();
		innerBg.color = backgroundColor;

		// アイテムコンテナ（垂直レイアウト）
		var containerObj = new GameObject("ItemContainer", typeof(RectTransform));
		containerObj.transform.SetParent(innerPanel.transform, false);
		var containerRect = containerObj.GetComponent<RectTransform>();
		containerRect.anchorMin = new Vector2(0, 1);
		containerRect.anchorMax = new Vector2(1, 1);
		containerRect.pivot = new Vector2(0, 1);
		containerRect.anchoredPosition = Vector2.zero;

		var layoutGroup = containerObj.AddComponent<VerticalLayoutGroup>();
		layoutGroup.childControlWidth = true;
		layoutGroup.childControlHeight = true;
		layoutGroup.childForceExpandWidth = true;
		layoutGroup.childForceExpandHeight = false;
		layoutGroup.spacing = 0;
		layoutGroup.padding = new RectOffset(0, 0, 0, 0);

		var contentSizeFitter = containerObj.AddComponent<ContentSizeFitter>();
		contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		return (menuObj, containerObj.transform);
	}

	/// <summary>
	/// メニュー項目を作成
	/// </summary>
	private void CreateMenuItem(Transform parent, ContextMenuItem item)
	{
		var itemObj = new GameObject($"MenuItem_{item.label}", typeof(RectTransform));
		itemObj.transform.SetParent(parent, false);

		var itemRect = itemObj.GetComponent<RectTransform>();
		var layoutElement = itemObj.AddComponent<LayoutElement>();

		if (item.isSeparator)
		{
			// セパレーター
			layoutElement.preferredHeight = separatorHeight;
			layoutElement.preferredWidth = menuWidth;

			var bgImage = itemObj.AddComponent<Image>();
			bgImage.color = separatorColor;
		}
		else
		{
			// 通常のメニュー項目
			// プレハブの高さを取得（なければデフォルト30）
			float itemHeight = 30f;
			if (menuItemTextPrefab != null)
			{
				var prefabRect = menuItemTextPrefab.GetComponent<RectTransform>();
				if (prefabRect != null)
				{
					itemHeight = prefabRect.sizeDelta.y;
				}
			}

			layoutElement.preferredHeight = itemHeight;
			layoutElement.preferredWidth = menuWidth;

			// 背景（重要：必ず Color.white を設定！）
			// Button の ColorTint は Image の色と乗算合成されるため、
			// Image を白以外にすると Button の色が正しく表示されません。
			var bgImage = itemObj.AddComponent<Image>();
			bgImage.color = Color.white;

			// ボタン
			var button = itemObj.AddComponent<Button>();
			button.targetGraphic = bgImage;
			var colors = button.colors;
			colors.normalColor = itemNormalColor;
			colors.highlightedColor = itemHighlightColor;
			colors.pressedColor = itemPressedColor;
			colors.selectedColor = itemNormalColor;
			colors.disabledColor = itemNormalColor;
			button.colors = colors;
			button.transition = Selectable.Transition.ColorTint;
			button.interactable = item.enabled;

			// テキスト（プレハブから生成）
			if (menuItemTextPrefab != null)
			{
				var textObj = Instantiate(menuItemTextPrefab, itemObj.transform);
				var textRect = textObj.GetComponent<RectTransform>();
				textRect.anchorMin = Vector2.zero;
				textRect.anchorMax = Vector2.one;
				textRect.offsetMin = new Vector2(10, 0);
				textRect.offsetMax = new Vector2(-10, 0);

				var text = textObj.GetComponent<TextMeshProUGUI>();
				if (text != null)
				{
					text.text = item.label;
					if(!item.enabled)
					{
						text.color = Color.gray;
					}
				}
			}
			else
			{
				Debug.LogWarning("[ContextMenu] menuItemTextPrefab is not assigned!");
			}

			// クリック時の処理
			button.onClick.AddListener(() =>
			{
				item.action?.Invoke();
				HideMenu();
			});
		}
	}

	/// <summary>
	/// メニューの位置を画面内に収まるように調整
	/// </summary>
	private void PositionMenu(Vector2 screenPosition)
	{
		if (currentMenuRect == null) return;

		// スクリーン座標→ローカル座標
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			menuContainer, screenPosition, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var localPoint);

		// メニューの左上をマウス位置に配置
		currentMenuRect.pivot = new Vector2(0, 1);
		currentMenuRect.anchoredPosition = localPoint;

		// 画面外にはみ出さないように調整
		var menuSize = currentMenuRect.sizeDelta;
		var containerRect = menuContainer.rect;

		if (localPoint.x + menuSize.x > containerRect.xMax)
		{
			localPoint.x = containerRect.xMax - menuSize.x;
		}
		if (localPoint.x < containerRect.xMin)
		{
			localPoint.x = containerRect.xMin;
		}
		if (localPoint.y - menuSize.y < containerRect.yMin)
		{
			localPoint.y = containerRect.yMin + menuSize.y;
		}
		if (localPoint.y > containerRect.yMax)
		{
			localPoint.y = containerRect.yMax;
		}

		currentMenuRect.anchoredPosition = localPoint;
	}

	/// <summary>
	/// メニューを非表示
	/// </summary>
	public void HideMenu()
	{
		if (currentMenu != null)
		{
			Destroy(currentMenu);
			currentMenu = null;
			currentMenuRect = null;
		}
	}


	/// <summary>
	/// マウスポインタがメニュー上にあるか判定
	/// </summary>
	private bool IsPointerOverMenu(Vector2 screenPosition)
	{
		if (currentMenu == null || canvas == null) return false;

		// Raycastでメニュー上のクリックか判定
		var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
		{
			position = screenPosition
		};

		var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
		UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

		foreach (var result in results)
		{
			if (result.gameObject.transform.IsChildOf(currentMenu.transform))
			{
				return true;
			}
		}

		return false;
	}
}

/// <summary>
/// メニュー項目の定義
/// </summary>
public class ContextMenuItem
{
	public string label;
	public Action action;
	public bool isSeparator;
	public bool enabled;

	public ContextMenuItem(string label, Action action, bool isSeparator = false, bool enabled = true)
	{
		this.label = label;
		this.action = action;
		this.isSeparator = isSeparator;
		this.enabled = enabled;
	}

	public static ContextMenuItem Separator()
	{
		return new ContextMenuItem("---", null, true);
	}
}
