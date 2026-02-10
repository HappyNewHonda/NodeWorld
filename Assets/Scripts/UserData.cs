using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ユーザーのゲームデータ（お金など）を管理
/// </summary>
public class UserData : MonoBehaviour
{
	public static UserData Instance;

	[Header("UI References")]
	[SerializeField] private TMPro.TextMeshProUGUI moneyText;

	[Header("User Resources")]
	[SerializeField] private int money = 10; // 初期所持金

	[Header("Progress")]
	public int CurrentChapter = 1;
	public int CurrentSection = 0;

	// 依頼の進行状態
	public RequestProgressData RequestProgress = new RequestProgressData();

	// 訪問済みのチャプター・セクション
	public HashSet<string> VisitedChapterSections = new HashSet<string>();

	// 素材の統計データ
	public ResourceStatisticsData ResourceStatistics = new ResourceStatisticsData();

	public int Money
	{
		get => money;
		set
		{
			money = Mathf.Max(0, value); // 負の値を防止
			UpdateMoneyUI(); // UI更新
			OnMoneyChanged?.Invoke(money); // イベント発火
			Debug.Log($"[UserData] Money changed: {money}");
		}
	}

	// お金が変更されたときのイベント（UI更新用）
	public event Action<int> OnMoneyChanged;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Destroy(gameObject);
		}

		// 初期表示を更新
		UpdateMoneyUI();
	}

	/// <summary>
	/// お金を追加
	/// </summary>
	public bool AddMoney(int amount)
	{
		if (amount < 0)
		{
			Debug.LogWarning($"[UserData] Cannot add negative amount: {amount}");
			return false;
		}

		Money += amount;
		return true;
	}

	/// <summary>
	/// お金を消費（残高不足の場合false）
	/// </summary>
	public bool SpendMoney(int amount)
	{
		if (amount < 0)
		{
			Debug.LogWarning($"[UserData] Cannot spend negative amount: {amount}");
			return false;
		}

		if (money < amount)
		{
			Debug.LogWarning($"[UserData] Not enough money: need {amount}, have {money}");
			return false;
		}

		Money -= amount;
		return true;
	}

	/// <summary>
	/// チャプター・セクションを訪問済みとしてマーク
	/// </summary>
	public void MarkChapterSectionVisited(int chapter, int section)
	{
		VisitedChapterSections.Add($"{chapter}_{section}");
	}

	/// <summary>
	/// チャプター・セクションが訪問済みかチェック
	/// </summary>
	public bool HasVisitedChapterSection(int chapter, int section)
	{
		return VisitedChapterSections.Contains($"{chapter}_{section}");
	}

	/// <summary>
	/// 素材の排出を記録（統計更新）
	/// </summary>
	public void RecordResourceOutput(int resourceId, int amount)
	{
		ResourceStatistics.RecordOutput(resourceId, amount);
		Debug.Log($"[UserData] Recorded output: ResourceID={resourceId}, Amount={amount}, AvgOutput={ResourceStatistics.GetAverageOutput(resourceId):F2}, MaxAvgOutput={ResourceStatistics.GetMaxAverageOutput(resourceId):F2}");
	}

	/// <summary>
	/// 指定した素材の現在の平均排出量を取得
	/// </summary>
	public float GetResourceAverageOutput(int resourceId)
	{
		return ResourceStatistics.GetAverageOutput(resourceId);
	}

	/// <summary>
	/// 指定した素材の最大平均排出量を取得
	/// </summary>
	public float GetResourceMaxAverageOutput(int resourceId)
	{
		return ResourceStatistics.GetMaxAverageOutput(resourceId);
	}

	private void UpdateMoneyUI()
	{
		if (moneyText != null)
		{
			moneyText.text = $"{money}";
		}
	}

	/// <summary>
	/// セーブ用データを取得
	/// </summary>
	public SavedUserData GetSaveData()
	{
		return new SavedUserData
		{
			money = this.money,
			currentChapter = this.CurrentChapter,
			currentSection = this.CurrentSection,
			requestProgress = this.RequestProgress,
			visitedChapterSections = new List<string>(this.VisitedChapterSections),
			resourceStatistics = this.ResourceStatistics
		};
	}

	/// <summary>
	/// セーブデータから復元
	/// </summary>
	public void LoadFromSaveData(SavedUserData saveData)
	{
		if (saveData == null)
		{
			Debug.LogWarning("[UserData] SaveData is null, using defaults");
			return;
		}

		this.Money = saveData.money;
		this.CurrentChapter = saveData.currentChapter;
		this.CurrentSection = saveData.currentSection;
		this.RequestProgress = saveData.requestProgress ?? new RequestProgressData();
		this.VisitedChapterSections = new HashSet<string>(saveData.visitedChapterSections ?? new List<string>());
		this.ResourceStatistics = saveData.resourceStatistics ?? new ResourceStatisticsData();

		Debug.Log($"[UserData] Loaded: Money={this.Money}, Chapter={this.CurrentChapter}, Section={this.CurrentSection}");
	}
}

/// <summary>
/// セーブ用のデータ構造
/// </summary>
[Serializable]
public class SavedUserData
{
	public int money;
	public int currentChapter;
	public int currentSection;
	public RequestProgressData requestProgress;
	public List<string> visitedChapterSections;
	public ResourceStatisticsData resourceStatistics;
}
