using System.Collections.Generic;
using Data.Master;
using UnityEngine;

/// <summary>
/// 依頼リストを管理するコンポーネント
/// Content2/Scroll Snap Vertical Multiple/List に配置
/// </summary>
public class RequestListManager : MonoBehaviour
{
	[Header("Prefab Settings")]
	[SerializeField] private GameObject requestItemPrefab;

	[Header("Container Settings")]
	[SerializeField] private Transform container;

	private List<RequestItemView> requestItems = new List<RequestItemView>();

	/// <summary>
	/// 指定したチャプター・セクションの依頼を受け取る（初回のみ）
	/// </summary>
	public void ReceiveRequestsForChapterSection(int chapter, int section)
	{
		if (MasterData.Instance == null || MasterData.Instance.RequestDatas == null) return;

		string key = $"{chapter}_{section}";
		if (MasterData.Instance.RequestDatas.SelectChapterAndSection.TryGetValue(key, out var requests))
		{
			foreach (var requestData in requests)
			{
				// 依頼の進行状態を作成
				var progress = UserData.Instance.RequestProgress.GetOrCreateRequest(
					chapter, section, requestData.DisplayTitle, requestData.Num);
				progress.state = RequestState.InProgress;

				Debug.Log($"[RequestListManager] Received Request: {requestData.DisplayTitle}");
			}
		}
	}

	/// <summary>
	/// 指定した章・節の依頼リストを表示
	/// </summary>
	public void DisplayRequests(int chapter, int section)
	{
		ClearAllRequests();

		if (MasterData.Instance == null || MasterData.Instance.RequestDatas == null) return;

		string key = $"{chapter}_{section}";
		if (MasterData.Instance.RequestDatas.SelectChapterAndSection.TryGetValue(key, out var requests))
		{
			foreach (var requestData in requests)
			{
				CreateRequestItem(requestData, chapter, section);
			}
		}
	}

	/// <summary>
	/// すべての依頼を表示
	/// </summary>
	public void DisplayAllRequests()
	{
		ClearAllRequests();

		if (MasterData.Instance == null || MasterData.Instance.RequestDatas == null || MasterData.Instance.RequestDatas.data == null) return;

		foreach (var requestData in MasterData.Instance.RequestDatas.data)
		{
			CreateRequestItem(requestData, requestData.Chapter, requestData.Section);
		}
	}

	/// <summary>
	/// 依頼アイテムを生成
	/// </summary>
	private void CreateRequestItem(RequestData data, int chapter, int section)
	{
		if (requestItemPrefab == null) return;

		Transform parent = container != null ? container : transform;
		GameObject itemObj = Instantiate(requestItemPrefab, parent);
		RequestItemView itemView = itemObj.GetComponent<RequestItemView>();

		if (itemView != null)
		{
			// 進行状態を取得
			var progress = UserData.Instance.RequestProgress.GetOrCreateRequest(
				chapter, section, data.DisplayTitle, data.Num);

			itemView.SetupRequest(data, progress);
			itemView.SetOnAcceptListener(OnRequestAccepted);
			itemView.SetOnRejectListener(OnRequestRejected);
			requestItems.Add(itemView);
		}
	}

	/// <summary>
	/// すべての依頼アイテムをクリア
	/// </summary>
	private void ClearAllRequests()
	{
		foreach (var item in requestItems)
		{
			if (item != null)
				Destroy(item.gameObject);
		}
		requestItems.Clear();
	}

	/// <summary>
	/// 依頼が受諾された時の処理
	/// </summary>
	private void OnRequestAccepted(RequestItemView itemView)
	{
		Debug.Log($"[RequestListManager] Request Accepted: {itemView.RequestData.DisplayTitle}");

		var progress = itemView.RequestProgress;
		progress.state = RequestState.Accepted;

		// 成功特典を適用
		ApplySuccessEffect(itemView.RequestData);

		// UI更新
		itemView.UpdateDisplay();

		// すべての依頼が完了したかチェック
		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.CheckRequestProgress();
		}
	}

	/// <summary>
	/// 依頼が拒否された時の処理
	/// </summary>
	private void OnRequestRejected(RequestItemView itemView)
	{
		Debug.Log($"[RequestListManager] Request Rejected: {itemView.RequestData.DisplayTitle}");

		var progress = itemView.RequestProgress;
		progress.state = RequestState.Accepted; // 拒否でも受諾扱い（失敗ペナルティ適用）

		// 失敗ペナルティを適用
		ApplyFailureEffect(itemView.RequestData);

		// UI更新
		itemView.UpdateDisplay();

		// すべての依頼が完了したかチェック
		if (GameFlowManager.Instance != null)
		{
			GameFlowManager.Instance.CheckRequestProgress();
		}
	}

	/// <summary>
	/// 成功特典を適用
	/// </summary>
	private void ApplySuccessEffect(RequestData requestData)
	{
		if (requestData.SuccesedEffect > 0)
		{
			// TODO: エフェクトの適用処理
			Debug.Log($"[RequestListManager] Applied Success Effect: {requestData.SuccesedEffect}");
		}
	}

	/// <summary>
	/// 失敗ペナルティを適用
	/// </summary>
	private void ApplyFailureEffect(RequestData requestData)
	{
		if (requestData.FailedEffect > 0)
		{
			// TODO: エフェクトの適用処理
			Debug.Log($"[RequestListManager] Applied Failure Effect: {requestData.FailedEffect}");
		}
	}

	/// <summary>
	/// 依頼の進捗を更新
	/// </summary>
	public void UpdateRequestProgress(int chapter, int section, string title, int progress)
	{
		var requestProgress = UserData.Instance.RequestProgress.GetOrCreateRequest(chapter, section, title, 0);
		requestProgress.currentProgress = progress;

		// 達成済みかチェック
		if (requestProgress.currentProgress >= requestProgress.targetProgress && requestProgress.state == RequestState.InProgress)
		{
			requestProgress.state = RequestState.Completed;
			Debug.Log($"[RequestListManager] Request Completed: {title}");
		}

		// UI更新
		RefreshRequestDisplay(chapter, section, title);
	}

	/// <summary>
	/// 特定の依頼のUI表示を更新
	/// </summary>
	private void RefreshRequestDisplay(int chapter, int section, string title)
	{
		var itemView = requestItems.Find(item =>
			item.RequestData.Chapter == chapter &&
			item.RequestData.Section == section &&
			item.RequestData.DisplayTitle == title);

		if (itemView != null)
		{
			itemView.UpdateDisplay();
		}
	}

	/// <summary>
	/// 現在表示されている依頼リストを取得
	/// </summary>
	public List<RequestItemView> GetCurrentRequests()
	{
		return new List<RequestItemView>(requestItems);
	}
}