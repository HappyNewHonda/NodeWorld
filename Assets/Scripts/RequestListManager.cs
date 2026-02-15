using System.Collections.Generic;
using System.Linq;
using Data.Master;
using Mono.Cecil.Cil;
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
	/// 更新処理
	/// </summary>
	private void Update()
	{
		UpdateRequestProgress();
	}

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
				progress.SetState(RequestState.InProgress);

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
		progress.SetState(RequestState.Accepted);

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
		progress.SetState(RequestState.Accepted);

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
	public void UpdateRequestProgress()
	{
		// 各依頼の進捗をリアルタイムで判定
		foreach (var itemView in requestItems)
		{
			if (itemView == null || itemView.RequestProgress == null) continue;
			if (itemView.RequestProgress.state != RequestState.InProgress) continue;

			var beforeProgress = itemView.RequestProgress.currentProgress;
			var targetNodes = itemView.RequestData.TargetNodes;
			var progressList = new List<float>();
			switch (itemView.RequestData.Type)
			{
				case 1:
					// RequestItemViewのUIで選択
					break;
				case 2:
					break;
				case 3:
					break;
				case 4:
					break;
				case 5:
					break;
				case 6:
					break;
				case 7:
					break;
				case 8:
					break;
				case 9:
					foreach (var nodeId in itemView.RequestData.TargetNodes)
					{
						foreach (var statistic in UserData.Instance.ResourceStatistics.GetStatisticsByNodeId(nodeId))
						{
							progressList.Add(Mathf.Min(1, statistic.maxAverageOutput / itemView.RequestData.Num));
						}
					}
					if (progressList.Count > 0)
					{
						itemView.RequestProgress.UpdateProgress((int)(progressList.Average() * 100));
					}
					else
					{
						itemView.RequestProgress.UpdateProgress(0);
					}
					break;
				case 10:
					break;

				default:
					Debug.LogWarning($"[RequestProgressManager] No specific handling for request type: {itemView.RequestData.Type}");
					break;
			}

			if (beforeProgress != itemView.RequestProgress.currentProgress)
			{
				// UI更新
				itemView.UpdateDisplay();
			}
		}
	}
}