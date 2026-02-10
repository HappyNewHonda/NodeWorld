using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class TabController : MonoBehaviour
{
	public static TabController Instance { get; private set; }

	
	[Header("Tab Buttons")]
	public Button tab1Button;
	public Button tab2Button;
	public Button tab3Button;
	public Button tab4Button;

	[Header("Content Panels")]
	public GameObject content1;
	public GameObject content2;
	public GameObject content3;
	public GameObject content4;

	[Header("Animation Settings")]
	public float animationDuration = 0.3f;
	public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
	public float tabButtonsWidth = 90f; // タブボタンの幅

	private int currentTab = 0;
	private bool isShown = false;
	private Vector2 shownPosition;
	private Coroutine slideCoroutine;
	private RectTransform rectTransform;

	// Hide位置を動的に計算するプロパティ
	private Vector2 HiddenPosition => new Vector2(rectTransform.rect.width - tabButtonsWidth, shownPosition.y);

	TabController()
	{
		Instance = this;
	}
	void Start()
	{
		rectTransform = GetComponent<RectTransform>();

		// TabUIContainer全体の初期位置を記録
		shownPosition = rectTransform.anchoredPosition;

		// イベントリスナーを設定
		tab1Button.onClick.AddListener(() => OnTabClicked(0));
		tab2Button.onClick.AddListener(() => OnTabClicked(1));
		tab3Button.onClick.AddListener(() => OnTabClicked(2));
		tab4Button.onClick.AddListener(() => OnTabClicked(3));

		// 初期タブを設定
		SwitchTab(0);

		// 初期状態をHideに設定
		rectTransform.anchoredPosition = HiddenPosition;
	}

	void Update()
	{
		// マウスクリック判定は ZoomPanController に移譲
	}

	private void OnTabClicked(int tabIndex)
	{
		// タブをクリックしたら表示状態にする
		if (!isShown)
		{
			Show();
		}
		SwitchTab(tabIndex);
	}

	public void SwitchTab(int tabIndex)
	{
		currentTab = tabIndex;

		// すべてのコンテンツを非表示
		content1.SetActive(false);
		content2.SetActive(false);
		content3.SetActive(false);
		content4.SetActive(false);

		// 選択されたタブとコンテンツを表示
		switch (tabIndex)
		{
			case 0:
				content1.SetActive(true);
				break;
			case 1:
				content2.SetActive(true);
				RebuildHierarchy((RectTransform)content2.GetComponentInChildren<VerticalLayoutGroup>().transform);
				break;
			case 2:
				content3.SetActive(true);
				break;
			case 3:
				content4.SetActive(true);
				break;
		}
	}

	private void RebuildHierarchy(RectTransform target)
	{
		LayoutRebuilder.ForceRebuildLayoutImmediate(target);
		LayoutRebuilder.ForceRebuildLayoutImmediate(target);
	}



	public void Show()
	{
		if (isShown) return;
		isShown = true;

		if (slideCoroutine != null)
		{
			StopCoroutine(slideCoroutine);
		}
		slideCoroutine = StartCoroutine(SlideContainer(HiddenPosition, shownPosition));
	}

	public void Hide()
	{
		if (!isShown) return;
		isShown = false;

		if (slideCoroutine != null)
		{
			StopCoroutine(slideCoroutine);
		}
		slideCoroutine = StartCoroutine(SlideContainer(shownPosition, HiddenPosition));
	}

	private IEnumerator SlideContainer(Vector2 from, Vector2 to)
	{
		float elapsed = 0f;

		while (elapsed < animationDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / animationDuration);
			float curveValue = animationCurve.Evaluate(t);

			Vector2 newPosition = Vector2.Lerp(from, to, curveValue);
			rectTransform.anchoredPosition = newPosition;

			yield return null;
		}

		// 最終位置を確実に設定
		rectTransform.anchoredPosition = to;
		slideCoroutine = null;
	}

	public void Toggle()
	{
		if (isShown)
		{
			Hide();
		}
		else
		{
			Show();
		}
	}
}