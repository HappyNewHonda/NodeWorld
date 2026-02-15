using System;
using System.Collections.Generic;

/// <summary>
/// 依頼の進行状態
/// </summary>
[Serializable]
public enum RequestState
{
	NotReceived,    // 未受領
	InProgress,     // 進行中
	Completed,      // 達成済み
	Accepted        // 受諾済み（報酬受取済み）
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
		this.state = RequestState.NotReceived;
		this.currentProgress = 0;
		this.targetProgress = targetProgress;
	}

	/// <summary>
	/// 進捗を更新し、必要に応じて状態を自動更新
	/// </summary>
	public void UpdateProgress(int progress)
	{
		currentProgress = progress;
		AutoUpdateState();
	}

	/// <summary>
	/// 進行状況に基づいて状態を自動更新
	/// </summary>
	private void AutoUpdateState()
	{
		// InProgress中に目標達成したらCompletedに変更
		if (state == RequestState.InProgress && IsProgressCompleted())
		{
			state = RequestState.Completed;
		}
	}

	/// <summary>
	/// 進捗が達成されたかチェック
	/// </summary>
	public bool IsProgressCompleted()
	{
		return currentProgress >= targetProgress;
	}

	/// <summary>
	/// 状態を指定して更新
	/// </summary>
	public void SetState(RequestState newState)
	{
		state = newState;
	}
}

/// <summary>
/// すべての依頼の進行状態を管理
/// </summary>
[Serializable]
public class RequestProgressData
{
	public List<RequestProgress> requests = new List<RequestProgress>();

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
	public bool AreAllRequestsAccepted(int chapter, int section)
	{
		var chapterRequests = requests.FindAll(r => r.chapter == chapter && r.section == section);
		// 依頼が0件の場合は自動的に完了とみなす（デモのみのセクション対応）
		if (chapterRequests.Count == 0) return true;
		return chapterRequests.TrueForAll(r => r.state == RequestState.Accepted);
	}
}