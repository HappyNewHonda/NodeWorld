using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 依頼の進行状態
/// </summary>
[Serializable]
public enum RequestState
{
	NotReceived,    // 未受領
	InProgress,     // 進行中
	Completed,      // 達成済み
	Cleared         // 報酬受取済み
}

/// <summary>
/// 個別の依頼の進行状態
/// </summary>
[Serializable]
public class RequestProgress
{
	public int chapter;
	public int section;
	public string title;
	public RequestState state;
	public int currentProgress; // 現在の進捗（ノード数など）
	public int targetProgress;  // 目標の進捗

	public RequestProgress()
	{
	}

	public RequestProgress(int chapter, int section, string title, int targetProgress)
	{
		this.chapter = chapter;
		this.section = section;
		this.title = title;
		this.SetState(RequestState.NotReceived);
		this.currentProgress = 0;
		this.targetProgress = targetProgress;
	}

	/// <summary>
	/// 進捗を更新し、必要に応じて状態を自動更新
	/// </summary>
	public void UpdateProgress(int progress, bool checkOver)
	{
		// InProgress中に目標達成したらCompletedに変更
		if (state == RequestState.InProgress)
		{
			currentProgress = progress;
			if (checkOver)
			{
				if (currentProgress >= targetProgress)
				{
					SetState(RequestState.Completed);
				}
			}
			else
			{
				if (currentProgress <= targetProgress)
				{
					SetState(RequestState.Completed);
				}
			}
		}
	}

	/// <summary>
	/// 状態を指定して更新
	/// </summary>
	public void SetState(RequestState newState)
	{
		UnityEngine.Debug.Log($"[RequestProgress] chage state {state} -> {newState} : {chapter}, {section}, {title}");
		state = newState;
	}
}

/// <summary>
/// すべての依頼の進行状態を管理
/// </summary>
[Serializable]
public class RequestProgressData
{
	[UnityEngine.SerializeField]
	private List<RequestProgress> requests = new List<RequestProgress>();

	/// <summary>
	/// 依頼を追加または取得
	/// </summary>
	public RequestProgress GetOrCreateRequest(int chapter, int section, string title, int targetProgress)
	{
		var request = requests.Find(r => r.chapter == chapter && r.section == section && r.title == title);
		if (request == null)
		{
			request = new RequestProgress(chapter, section, title, targetProgress);
			requests.Add(request);
		}
		return request;
	}

	/// <summary>
	/// すべての依頼が受諾済みかチェック（依頼が0件の場合もtrueを返す）
	/// </summary>
	public bool AreAllRequestsCleared(int chapter, int section)
	{
		var chapterRequests = requests.FindAll(r => r.chapter == chapter && r.section == section);
		// 依頼が0件の場合は自動的に完了とみなす（デモのみのセクション対応）
		if (chapterRequests.Count == 0) return true;
		return chapterRequests.TrueForAll(r => r.state == RequestState.Cleared);
	}
}