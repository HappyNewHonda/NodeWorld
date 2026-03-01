using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Data.Master;
using Define;

/// <summary>
/// ゲーム全体のフロー管理（ステートマシン）
/// </summary>
public class GameFlowManager : MonoBehaviour
{
	public static GameFlowManager Instance { get; private set; }

	[Header("References")]
	[SerializeField] private RequestListManager requestListManager;

	[Header("Demo")]
	[SerializeField] private DemoPlayer demoPlayer;

	public enum GameState
	{
		Initializing,
		PlayingDemo,
		WaitingForUser,
		CheckingRequests,
		TransitioningChapter
	}

	private GameState currentState = GameState.Initializing;
	public GameState CurrentState => currentState;

	/// <summary>
	/// trueの間、全ノードの生産・資源移送を停止する
	/// </summary>
	public bool IsProductionPaused { get; private set; } = false;

	// イベント
	public event Action OnChapterStarted;
	public event Action OnDemoStarted;
	public event Action OnDemoEnded;
	public event Action OnUserInputEnabled;
	public event Action OnAllRequestsCompleted;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Destroy(gameObject);
			return;
		}
	}

	private void Start()
	{
		StartCoroutine(InitializeGame());
	}

	private IEnumerator InitializeGame()
	{
		currentState = GameState.Initializing;

		while (MasterData.Instance == null || UserData.Instance == null)
		{
			yield return null;
		}

		yield return StartCoroutine(StartChapterSection(UserData.Instance.CurrentChapter, UserData.Instance.CurrentSection));
	}

	public IEnumerator StartChapterSection(int chapter, int section)
	{
		Debug.Log($"[GameFlowManager] Starting Chapter {chapter}, Section {section}");

		UserData.Instance.CurrentChapter = chapter;
		UserData.Instance.CurrentSection = section;

		OnChapterStarted?.Invoke();

		if (!UserData.Instance.HasVisitedChapterSection(chapter, section))
		{
			UserData.Instance.MarkChapterSectionVisited(chapter, section);
			requestListManager.ReceiveRequestsForChapterSection(chapter, section);
		}

		requestListManager.DisplayRequests(chapter, section);

		string key = $"{chapter}_{section}";
		bool hasDemo = false;
		if (MasterData.Instance.ChapterDatas.SelectChapterAndSection.TryGetValue(key, out var chapterData))
		{
			if (chapterData.DemoId > 0)
			{
				hasDemo = true;
				yield return StartCoroutine(PlayDemo(chapterData.DemoId));
			}
		}

		EnableUserInput();

		if (hasDemo && UserData.Instance.RequestProgress.AreAllRequestsCleared(chapter, section))
		{
			Debug.Log($"[GameFlowManager] No requests in this section, auto-transitioning to next section");
			yield return StartCoroutine(TransitionToNextChapter());
		}
	}

	private IEnumerator PlayDemo(int demoId)
	{
		currentState = GameState.PlayingDemo;
		IsProductionPaused = true;
		Debug.Log($"[GameFlowManager] Playing Demo: {demoId} (production paused)");
		OnDemoStarted?.Invoke();

		if (demoPlayer != null)
		{
			yield return demoPlayer.Play(demoId);
		}
		else
		{
			Debug.LogWarning("[GameFlowManager] DemoPlayer is not assigned. Skipping demo.");
		}

		IsProductionPaused = false;
		Debug.Log($"[GameFlowManager] Demo Ended (production resumed)");
		OnDemoEnded?.Invoke();
	}

	private void EnableUserInput()
	{
		currentState = GameState.WaitingForUser;
		Debug.Log($"[GameFlowManager] User Input Enabled");
		OnUserInputEnabled?.Invoke();
	}

	public void CheckRequestProgress()
	{
		if (currentState != GameState.WaitingForUser) return;

		int chapter = UserData.Instance.CurrentChapter;
		int section = UserData.Instance.CurrentSection;

		if (UserData.Instance.RequestProgress.AreAllRequestsCleared(chapter, section))
		{
			StartCoroutine(TransitionToNextChapter());
		}
	}

	private IEnumerator TransitionToNextChapter()
	{
		currentState = GameState.TransitioningChapter;
		Debug.Log($"[GameFlowManager] All Requests Completed. Transitioning to next chapter.");
		OnAllRequestsCompleted?.Invoke();

		int nextChapter = UserData.Instance.CurrentChapter;
		int nextSection = UserData.Instance.CurrentSection + 1;

		if (!MasterData.Instance.ChapterDatas.SelectChapterAndSection.ContainsKey($"{nextChapter}_{nextSection}"))
		{
			nextSection = 1;
			nextChapter++;
		}

		yield return StartCoroutine(StartChapterSection(nextChapter, nextSection));
	}
}
