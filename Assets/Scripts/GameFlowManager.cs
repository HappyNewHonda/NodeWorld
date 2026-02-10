using System.Collections;
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

	public enum GameState
	{
		Initializing,       // 初期化中
		PlayingDemo,        // デモ再生中
		WaitingForUser,     // ユーザー操作待ち
		CheckingRequests,   // 依頼チェック中
		TransitioningChapter // チャプター移行中
	}

	private GameState currentState = GameState.Initializing;
	public GameState CurrentState => currentState;

	// イベント
	public System.Action OnChapterStarted;
	public System.Action OnDemoStarted;
	public System.Action OnDemoEnded;
	public System.Action OnUserInputEnabled;
	public System.Action OnAllRequestsCompleted;

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

	/// <summary>
	/// ゲームの初期化
	/// </summary>
	private IEnumerator InitializeGame()
	{
		currentState = GameState.Initializing;

		// MasterDataとUserDataの準備を待つ
		while (MasterData.Instance == null || UserData.Instance == null)
		{
			yield return null;
		}

		// 最初のチャプターを開始
		yield return StartCoroutine(StartChapterSection(UserData.Instance.CurrentChapter, UserData.Instance.CurrentSection));
	}

	/// <summary>
	/// チャプター・セクションを開始
	/// </summary>
	public IEnumerator StartChapterSection(int chapter, int section)
	{
		Debug.Log($"[GameFlowManager] Starting Chapter {chapter}, Section {section}");

		UserData.Instance.CurrentChapter = chapter;
		UserData.Instance.CurrentSection = section;

		OnChapterStarted?.Invoke();

		// ①初めてのチャプター・セクションなら依頼を受け取る
		if (!UserData.Instance.HasVisitedChapterSection(chapter, section))
		{
			UserData.Instance.MarkChapterSectionVisited(chapter, section);
			requestListManager.ReceiveRequestsForChapterSection(chapter, section);
		}

		// 依頼リストを表示
		requestListManager.DisplayRequests(chapter, section);

		// ②DemoIDが設定されていたらデモを再生
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

		// ③ユーザー操作可能に
		EnableUserInput();

		// デモ後、依頼が0件の場合は自動的に次のセクションへ
		if (hasDemo && UserData.Instance.RequestProgress.AreAllRequestsAccepted(chapter, section))
		{
			Debug.Log($"[GameFlowManager] No requests in this section, auto-transitioning to next section");
			yield return StartCoroutine(TransitionToNextChapter());
		}
	}

	/// <summary>
	/// デモを再生
	/// </summary>
	private IEnumerator PlayDemo(int demoId)
	{
		currentState = GameState.PlayingDemo;
		Debug.Log($"[GameFlowManager] Playing Demo: {demoId}");
		OnDemoStarted?.Invoke();

		// TODO: デモ再生の実装
		// 仮実装: 3秒待機
		yield return playDemo(demoId);

		Debug.Log($"[GameFlowManager] Demo Ended");
		OnDemoEnded?.Invoke();
	}

	/// <summary>
	/// ユーザー操作を有効化
	/// </summary>
	private void EnableUserInput()
	{
		currentState = GameState.WaitingForUser;
		Debug.Log($"[GameFlowManager] User Input Enabled");
		OnUserInputEnabled?.Invoke();
	}

	/// <summary>
	/// 依頼の進捗をチェック（定期的に呼ばれる想定）
	/// </summary>
	public void CheckRequestProgress()
	{
		if (currentState != GameState.WaitingForUser) return;

		int chapter = UserData.Instance.CurrentChapter;
		int section = UserData.Instance.CurrentSection;

		// すべての依頼が受諾済みかチェック
		if (UserData.Instance.RequestProgress.AreAllRequestsAccepted(chapter, section))
		{
			StartCoroutine(TransitionToNextChapter());
		}
	}

	/// <summary>
	/// 次のチャプターへ移行
	/// </summary>
	private IEnumerator TransitionToNextChapter()
	{
		currentState = GameState.TransitioningChapter;
		Debug.Log($"[GameFlowManager] All Requests Completed. Transitioning to next chapter.");
		OnAllRequestsCompleted?.Invoke();

		// 次のチャプター・セクションを決定
		int nextChapter = UserData.Instance.CurrentChapter;
		int nextSection = UserData.Instance.CurrentSection + 1;

		// セクションの最大数を確認（UserDataから取得）
		if (!MasterData.Instance.ChapterDatas.SelectChapterAndSection.ContainsKey($"{nextChapter}_{nextSection}"))
		{
			nextSection = 1;
			nextChapter++;
		}

		// 次のチャプターを開始
		yield return StartCoroutine(StartChapterSection(nextChapter, nextSection));
	}
	private IEnumerator playDemo(int demoId)
	{
		GraphUIManager manager = GraphUIManager.Instance;
		switch (demoId)
		{
			case 1:
				GraphUIManager.Instance.graphRoot.localPosition = new Vector3(100, -75, 0);
				GraphUIManager.Instance.graphRoot.localScale = new Vector3(1.5f, 1.5f, 1);
				manager.CreateNodeFromData(nodeId: NodeId.水処理ユニット, level: 0, position: new Vector2(-550, 200));
				manager.CreateNodeFromData(nodeId: NodeId.発電ユニット, level: 0, position: new Vector2(-550, -100));
				manager.CreateNodeFromData(nodeId: NodeId.農業モジュール, level: 1, position: new Vector2(-125, 275));
				manager.CreateNodeFromData(nodeId: NodeId.居住モジュール, level: 1, position: new Vector2(225, -25));
				break;
			case 2:
				Debug.Log("DemoPlayer: Demo 2 started.");
				break;
			default:
				Debug.Log("Demo Not Found. demoId : " + demoId);
				yield break;
		}
	}
}