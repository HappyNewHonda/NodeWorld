using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Define;

/// <summary>
/// 水処理ユニット、発電ユニット、治安維持ユニットに対して
/// お金を投資して出力を増やす機能。
/// $1 ごとに出力 +10%。お金は生産開始時に消費。
/// </summary>
public class NodeInvestmentController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button plusButton;
    [SerializeField] private Button minusButton;
    [SerializeField] private TextMeshProUGUI investmentAmountText;

    private NodeView nodeView;

    /// <summary>現在設定中の投資額（未消費、次の生産で使う予定額）</summary>
    private int investmentAmount = 0;

    /// <summary>投資対象のノードIDセット</summary>
    private static readonly int[] InvestableNodeIds = new[]
    {
        NodeId.水処理ユニット,
        NodeId.発電ユニット,
        NodeId.治安維持ユニット
    };

    public const string InvestmentObjectCreatePath = "Canvas/UIRoot/Right/TabUIContainer/ContentArea/Content1/Investment";

	public int InvestmentAmount => investmentAmount;

    /// <summary>$1あたりの出力増加率（%）</summary>
    public const int PERCENT_PER_DOLLAR = 10;

    public void Setup(NodeView node)
    {
        nodeView = node;
	}

    void Start()
    {
        // 投資対象かどうかチェック
        bool isInvestable = IsInvestableNode(nodeView.nodeId);

        if (!isInvestable) return;

        if (plusButton != null)
            plusButton.onClick.AddListener(OnPlusClicked);
        if (minusButton != null)
            minusButton.onClick.AddListener(OnMinusClicked);

        UpdateDisplay();
    }

    void OnEnable()
    {
        if (UserData.Instance != null)
            UserData.Instance.OnMoneyChanged += OnMoneyChanged;
    }

    void OnDisable()
    {
        if (UserData.Instance != null)
            UserData.Instance.OnMoneyChanged -= OnMoneyChanged;
    }

    private void OnMoneyChanged(int newMoney)
    {
        UpdateDisplay();
    }

    /// <summary>
    /// 投資対象のノードかどうか
    /// </summary>
    public static bool IsInvestableNode(int nodeId)
    {
        foreach (var id in InvestableNodeIds)
        {
            if (id == nodeId) return true;
        }
        return false;
    }

    private void OnPlusClicked()
    {
        // お金が足りる場合のみ増加
        // ただし実際の消費は生産開始時なので、ここでは設定のみ
        investmentAmount++;
        UpdateDisplay();
    }

    private void OnMinusClicked()
    {
        if (investmentAmount > 0)
        {
            investmentAmount--;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// 投資額を直接設定（ロード時用）
    /// </summary>
    public void SetInvestmentAmount(int amount)
    {
        investmentAmount = Mathf.Max(0, amount);
        UpdateDisplay();
    }

    /// <summary>
    /// 表示を更新
    /// </summary>
    public void UpdateDisplay()
	{
		int boostPercent = investmentAmount * PERCENT_PER_DOLLAR;
		investmentAmountText.text = boostPercent > 0 ? $"+{boostPercent}%" : "0%" + $" (${investmentAmount})";

		// +ボタン：お金が足りなくなったら無効化
		plusButton.interactable = UserData.Instance != null &&
								 UserData.Instance.Money >= (investmentAmount + 1);

		// -ボタン：0以下なら無効化
		minusButton.interactable = investmentAmount > 0;
	}

    /// <summary>
    /// 生産開始時に呼ばれる。お金を消費し、ブースト倍率を返す。
    /// お金が足りない場合は払える分だけ消費する。
    /// </summary>
    /// <returns>出力倍率（1.0 = ブーストなし）</returns>
    public float ConsumeInvestmentAndGetMultiplier()
    {
        if (investmentAmount <= 0) return 1f;

        // 実際に払える額を計算
        int canPay = Mathf.Min(investmentAmount, UserData.Instance.Money);
        if (canPay <= 0) return 1f;

        // お金を消費
        UserData.Instance.SpendMoney(canPay);

        // ブースト倍率を計算
        float multiplier = 1f + (canPay * PERCENT_PER_DOLLAR / 100f);

        Debug.Log($"[Investment] Node '{nodeView.titleText.text}' invested ${canPay}, output multiplier: {multiplier:F2}");

        return multiplier;
    }

    /// <summary>
    /// 現在のブースト倍率（表示用、実消費なし）
    /// </summary>
    public float GetCurrentMultiplierPreview()
    {
        int canPay = Mathf.Min(investmentAmount, UserData.Instance != null ? UserData.Instance.Money : 0);
        return 1f + (canPay * PERCENT_PER_DOLLAR / 100f);
    }
}
