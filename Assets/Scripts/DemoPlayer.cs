using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Define;
using Effects;
using Data.Master;

/// <summary>
/// デモ再生エンジン。
/// 各DemoIdに対応するステップ列（コルーチン）を順次実行する。
/// demo_data.json の Type に基づいてステップを自動構築する。
/// </summary>
public class DemoPlayer : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private GraphUIManager graphUIManager;

	[Header("Timing Defaults")]
	[SerializeField] private float defaultStepInterval = 0.6f;
	[SerializeField] private float defaultPostDelay = 0.5f;

	[Header("Prefab Paths")]
	[SerializeField] private string prefabBasePath = "Prefabs/Demo/";

	[Header("Message UI")]
	[SerializeField] private RectTransform messageContainer;
	[SerializeField] private GameObject messagePrefab;

	// 管理ID → 生成済みオブジェクトのマップ（デモ中のみ有効）
	private readonly Dictionary<int, GameObject> managedObjects = new();

	// 管理ID → 生成済みNodeViewのマップ
	private readonly Dictionary<int, NodeView> managedNodes = new();

	// 現在表示中のメッセージオブジェクト
	private DemoMessageView currentMessageView;

	// 現在表示中のAIオブジェクト
	private DemoAIView currentAIView;

	/// <summary>
	/// 指定したDemoIdのデモを再生する（コルーチン）。
	/// GameFlowManagerから呼ばれる。
	/// </summary>
	public IEnumerator Play(int demoId)
	{
		Debug.Log($"[DemoPlayer] Starting demo {demoId}");

		// 管理マップをクリア
		managedObjects.Clear();
		managedNodes.Clear();

		var steps = BuildStepsFromData(demoId);
		if (steps == null || steps.Count == 0)
		{
			Debug.LogWarning($"[DemoPlayer] No steps defined for demoId: {demoId}");
			yield break;
		}

		foreach (var step in steps)
		{
			yield return StartCoroutine(step);
		}

		// デモ終了時に管理マップをクリア（オブジェクトは残す）
		managedObjects.Clear();
		managedNodes.Clear();

		Debug.Log($"[DemoPlayer] Demo {demoId} completed");
	}

	/// <summary>
	/// DemoDataからステップ列を自動構築する。
	/// </summary>
	private List<IEnumerator> BuildStepsFromData(int demoId)
	{
		if (MasterData.Instance == null || MasterData.Instance.DemoDatas == null)
		{
			Debug.LogError("[DemoPlayer] MasterData or DemoDatas is null");
			return null;
		}

		if (!MasterData.Instance.DemoDatas.SelectId.TryGetValue(demoId, out var demoDataArray))
		{
			Debug.LogWarning($"[DemoPlayer] No DemoData found for demoId: {demoId}");
			return BuildStepsFallback(demoId);
		}

		var steps = new List<IEnumerator>();

		foreach (var data in demoDataArray)
		{
			var step = CreateStepFromDemoData(data);
			if (step != null)
			{
				steps.Add(step);
			}
		}

		return steps;
	}

	/// <summary>
	/// DemoDataの1行をコルーチンステップに変換する。
	/// </summary>
	private IEnumerator CreateStepFromDemoData(DemoData data)
	{
		int managedId = Mathf.RoundToInt(data.Postion.z);
		Vector2 position = new Vector2(data.Postion.x, data.Postion.y);

		switch (data.Type)
		{
			case "PrefabLoad":
				return StepPrefabLoad(data.Parameter, position, managedId);

			case "PrefabDelete":
				return StepPrefabDelete(managedId);

			case "ShowMessage":
				return StepShowMessage(data.Parameter, position, managedId);

			case "DeleteMessage":
				return StepDeleteMessage(managedId);

			case "ShowAI":
				return StepShowAI(data.Parameter, position, managedId);

			case "DeleteAI":
				return StepDeleteAI(managedId);

			case "ChangeAIImage":
				return StepChangeAIImage(data.Parameter, managedId);

			case "ShowAIText":
				return StepShowAIText(data.Parameter, managedId);

			case "DeleteAIText":
				return StepDeleteAIText(managedId);

			case "AddNode":
				return StepAddNode(data.Parameter, position, managedId);

			case "DeleteNode":
				return StepDeleteNode(managedId);

			case "AddEffect":
				return StepAddEffect(data.Parameter, managedId);

			case "RemoveEffect":
				return StepRemoveEffect(data.Parameter, managedId);

			case "WaitDelayMillSec":
				return StepWaitDelayMillSec(data.Parameter);

			case "AnimationPlay":
				return StepAnimationPlay(data.Parameter, managedId);

			case "WaitAnimation":
				return StepWaitAnimation(data.Parameter, managedId);

			case "SetCameraPosition":
				return StepSetCameraPositionAnimated(position, data.Parameter);

			case "SetCameraScale":
				return StepSetCameraScaleAnimated(
					new Vector3(data.Postion.x, data.Postion.y, data.Postion.z),
					data.Parameter);

			case "WaitClick":
				return StepWaitClick();

			default:
				Debug.LogWarning($"[DemoPlayer] Unknown demo type: {data.Type}");
				return null;
		}
	}

	// =====================================================================
	// ステップ実装
	// =====================================================================

	/// <summary>プレハブをロードして管理IDに登録</summary>
	private IEnumerator StepPrefabLoad(string prefabName, Vector2 position, int managedId)
	{
		string path = prefabBasePath + prefabName;
		var prefab = Resources.Load<GameObject>(path);
		if (prefab == null)
		{
			Debug.LogError($"[DemoPlayer] Prefab not found: {path}");
			yield break;
		}

		var obj = Instantiate(prefab, transform);
		obj.name = $"Demo_{prefabName}_{managedId}";

		// UI要素の場合はCanvasの子にする
		var rectTransform = obj.GetComponent<RectTransform>();
		if (rectTransform != null)
		{
			var canvas = graphUIManager.canvas;
			if (canvas != null)
			{
				obj.transform.SetParent(canvas.transform, false);
				rectTransform.anchoredPosition = position;
			}
		}
		else
		{
			obj.transform.position = new Vector3(position.x, position.y, 0f);
		}

		RegisterManagedObject(managedId, obj);
		Debug.Log($"[DemoPlayer] PrefabLoad: {prefabName} at {position}, managedId={managedId}");
		yield break;
	}

	/// <summary>管理IDのプレハブを削除</summary>
	private IEnumerator StepPrefabDelete(int managedId)
	{
		DestroyManagedObject(managedId);
		Debug.Log($"[DemoPlayer] PrefabDelete: managedId={managedId}");
		yield break;
	}

	/// <summary>メッセージを画面下部に表示</summary>
	private IEnumerator StepShowMessage(string message, Vector2 position, int managedId)
	{
		// 既存メッセージがあれば削除
		if (currentMessageView != null)
		{
			Destroy(currentMessageView.gameObject);
			currentMessageView = null;
		}

		if (messagePrefab != null && messageContainer != null)
		{
			var obj = Instantiate(messagePrefab, messageContainer);
			var rt = obj.GetComponent<RectTransform>();
			rt.anchoredPosition = position;

			// DemoMessageView があればそちら経由
			currentMessageView = obj.GetComponent<DemoMessageView>();
			currentMessageView.SetText(message);
		}

		RegisterManagedObject(managedId, currentMessageView.gameObject);
		Debug.Log($"[DemoPlayer] ShowMessage: \"{message}\" at {position}");

		// 画面タップ待ち
		while (true)
		{
			if (Input.GetMouseButtonDown(0))
			{
				break;
			}
			yield return null;
		}
	}

	/// <summary>メッセージを削除</summary>
	private IEnumerator StepDeleteMessage(int managedId)
	{
		if (managedId > 0)
		{
			DestroyManagedObject(managedId);
		}
		else if (currentMessageView != null)
		{
			Destroy(currentMessageView.gameObject);
			currentMessageView = null;
		}
		Debug.Log($"[DemoPlayer] DeleteMessage: managedId={managedId}");
		yield break;
	}

	/// <summary>AIキャラクターを表示</summary>
	private IEnumerator StepShowAI(string aiPrefabName, Vector2 position, int managedId)
	{
		string path = prefabBasePath + aiPrefabName;
		var prefab = Resources.Load<GameObject>(path);
		if (prefab == null)
		{
			Debug.LogError($"[DemoPlayer] AI Prefab not found: {path}");
			yield break;
		}

		// 既存AIがあれば削除
		if (currentAIView != null)
		{
			Destroy(currentAIView.gameObject);
		}

		var obj = Instantiate(prefab, transform);
		obj.name = $"Demo_AI_{aiPrefabName}";

		currentAIView = obj.GetComponent<DemoAIView>();
		var rt = currentAIView.GetComponent<RectTransform>();
		rt.anchoredPosition = position;

		RegisterManagedObject(managedId, obj);
		Debug.Log($"[DemoPlayer] ShowAI: {aiPrefabName} at {position}, managedId={managedId}");
		yield break;
	}

	/// <summary>AIキャラクターを削除</summary>
	private IEnumerator StepDeleteAI(int managedId)
	{
		if (managedId > 0)
		{
			DestroyManagedObject(managedId);
		}

		if (currentAIView != null)
		{
			Destroy(currentAIView.gameObject);
			currentAIView = null;
		}
		Debug.Log($"[DemoPlayer] DeleteAI: managedId={managedId}");
		yield break;
	}

	/// <summary>AIの画像を変更</summary>
	private IEnumerator StepChangeAIImage(string imageName, int managedId)
	{
		// DemoAIView 経由で切り替え（存在すれば）
		DemoAIView aiView = null;

		var targetObj = GetManagedObject(managedId);
		if (targetObj != null)
		{
			aiView = targetObj.GetComponent<DemoAIView>();
		}
		if (aiView == null)
		{
			aiView = currentAIView;
		}

		if (aiView != null)
		{
			aiView.SetImage(imageName);
			Debug.Log($"[DemoPlayer] ChangeAIImage(via DemoAIView): {imageName}, managedId={managedId}");
			yield break;
		}

		// フォールバック：従来のロジック
		GameObject targetAI = targetObj ?? currentAIView.gameObject;
		if (targetAI == null)
		{
			Debug.LogWarning($"[DemoPlayer] ChangeAIImage: AI object not found for managedId={managedId}");
			yield break;
		}

		string path = prefabBasePath + "AIImages/" + imageName;
		var sprite = Resources.Load<Sprite>(path);
		if (sprite == null)
		{
			sprite = Resources.Load<Sprite>("Sprites/AI/" + imageName);
		}

		if (sprite != null)
		{
			var image = targetAI.GetComponentInChildren<Image>();
			if (image != null)
			{
				image.sprite = sprite;
			}
		}
		else
		{
			Debug.LogWarning($"[DemoPlayer] ChangeAIImage: Sprite not found: {imageName}");
		}

		Debug.Log($"[DemoPlayer] ChangeAIImage: {imageName}, managedId={managedId}");
		yield break;
	}

	/// <summary>AIのテキストを表示</summary>
	private IEnumerator StepShowAIText(string text, int managedId)
	{
		DemoAIView aiView = null;
		var targetObj = GetManagedObject(managedId);
		if (targetObj != null) aiView = targetObj.GetComponent<DemoAIView>();
		if (aiView == null) aiView = currentAIView;

		if (aiView != null)
		{
			aiView.SetText(text);
			Debug.Log($"[DemoPlayer] ShowAIText(via DemoAIView): \"{text}\", managedId={managedId}");

			// 画面タップ待ち
			while (true)
			{
				if (Input.GetMouseButtonDown(0))
				{
					break;
				}
				yield return null;
			}
		}
		else
		{
			Debug.Log($"[DemoPlayer] ShowAIText: \"{text}\", managedId={managedId}");
		}
	}

	/// <summary>AIのテキストを非表示</summary>
	private IEnumerator StepDeleteAIText(int managedId)
	{
		DemoAIView aiView = null;
		var targetObj = GetManagedObject(managedId);
		if (targetObj != null) aiView = targetObj.GetComponent<DemoAIView>();
		if (aiView == null) aiView = currentAIView;

		if (aiView != null)
		{
			aiView.SetTextVisible(false);
			Debug.Log($"[DemoPlayer] DeleteAIText(via DemoAIView): managedId={managedId}");
			yield break;
		}

		var textComponent = aiView.GetAIText();
		if (textComponent != null)
		{
			textComponent.text = "";
			textComponent.gameObject.SetActive(false);
		}

		Debug.Log($"[DemoPlayer] DeleteAIText: managedId={managedId}");
		yield break;
	}

	/// <summary>ノードを追加して管理IDに登録</summary>
	private IEnumerator StepAddNode(string nodeIdParam, Vector2 position, int managedId)
	{
		if (!int.TryParse(nodeIdParam, out int nodeId))
		{
			Debug.LogError($"[DemoPlayer] AddNode: Invalid nodeId parameter: {nodeIdParam}");
			yield break;
		}

		// NodeIoDataから初期レベルを取得
		int level = GetInitialLevel(nodeId);

		var node = graphUIManager.CreateNodeFromData(nodeId, level, position);
		if (node != null)
		{
			if (managedId > 0)
			{
				managedNodes[managedId] = node;
				RegisterManagedObject(managedId, node.gameObject);
			}
			Debug.Log($"[DemoPlayer] AddNode: nodeId={nodeId}, level={level} at {position}, managedId={managedId}");
		}
		else
		{
			Debug.LogWarning($"[DemoPlayer] AddNode: Failed to create node: nodeId={nodeId}");
		}
		yield break;
	}

	/// <summary>管理IDのノードを削除</summary>
	private IEnumerator StepDeleteNode(int managedId)
	{
		if (managedNodes.TryGetValue(managedId, out var node) && node != null)
		{
			graphUIManager.RemoveNode(node);
			managedNodes.Remove(managedId);
			managedObjects.Remove(managedId);
			Debug.Log($"[DemoPlayer] DeleteNode: managedId={managedId}");
		}
		else
		{
			Debug.LogWarning($"[DemoPlayer] DeleteNode: Node not found for managedId={managedId}");
		}
		yield break;
	}

	/// <summary>管理IDのノードにエフェクトを付与</summary>
	private IEnumerator StepAddEffect(string effectIdParam, int managedId)
	{
		if (!int.TryParse(effectIdParam, out int effectId))
		{
			Debug.LogError($"[DemoPlayer] AddEffect: Invalid effectId parameter: {effectIdParam}");
			yield break;
		}

		if (!MasterData.Instance.EffectDatas.SelectId.TryGetValue(effectId, out var effectData))
		{
			Debug.LogError($"[DemoPlayer] AddEffect: EffectData not found for id={effectId}");
			yield break;
		}

		// managedId==0 ならグローバルエフェクト
		if (managedId == 0)
		{
			var globals = GlobalEffectController.Instance.EnumerateAllGlobalEffectsRaw().ToList();
			globals.Add(effectData);
			GlobalEffectController.Instance.SetGlobalEffects(globals);
			Debug.Log($"[DemoPlayer] AddEffect(Global): effectId={effectId}");
		}
		else
		{
			// 管理IDのノードにローカルエフェクト付与
			if (managedNodes.TryGetValue(managedId, out var node) && node != null)
			{
				var nec = node.GetComponent<NodeEffectController>();
				if (nec != null)
				{
					nec.AddLocalEffect(effectData);
					Debug.Log($"[DemoPlayer] AddEffect(Local): effectId={effectId} -> managedId={managedId}");
				}
			}
			else
			{
				Debug.LogWarning($"[DemoPlayer] AddEffect: Node not found for managedId={managedId}");
			}
		}
		yield break;
	}

	/// <summary>管理IDのノードからエフェクトを除去</summary>
	private IEnumerator StepRemoveEffect(string effectIdParam, int managedId)
	{
		if (!int.TryParse(effectIdParam, out int effectId))
		{
			Debug.LogError($"[DemoPlayer] RemoveEffect: Invalid effectId parameter: {effectIdParam}");
			yield break;
		}

		if (!MasterData.Instance.EffectDatas.SelectId.TryGetValue(effectId, out var effectData))
		{
			Debug.LogError($"[DemoPlayer] RemoveEffect: EffectData not found for id={effectId}");
			yield break;
		}

		if (managedId == 0)
		{
			var globals = GlobalEffectController.Instance.EnumerateAllGlobalEffectsRaw()
				.Where(e => e.Id != effectId).ToList();
			GlobalEffectController.Instance.SetGlobalEffects(globals);
			Debug.Log($"[DemoPlayer] RemoveEffect(Global): effectId={effectId}");
		}
		else
		{
			if (managedNodes.TryGetValue(managedId, out var node) && node != null)
			{
				var nec = node.GetComponent<NodeEffectController>();
				nec?.RemoveLocalEffects(effectData);
				Debug.Log($"[DemoPlayer] RemoveEffect(Local): effectId={effectId} <- managedId={managedId}");
			}
		}
		yield break;
	}

	/// <summary>指定ミリ秒待機</summary>
	private IEnumerator StepWaitDelayMillSec(string millisParam)
	{
		if (!int.TryParse(millisParam, out int millis))
		{
			Debug.LogWarning($"[DemoPlayer] WaitDelayMillSec: Invalid parameter: {millisParam}");
			yield break;
		}

		float seconds = millis / 1000f;
		Debug.Log($"[DemoPlayer] WaitDelayMillSec: {millis}ms ({seconds}s)");
		yield return new WaitForSeconds(seconds);
	}

	/// <summary>管理IDのオブジェクトでアニメーションを再生</summary>
	private IEnumerator StepAnimationPlay(string stateName, int managedId)
	{
		var obj = GetManagedObject(managedId);
		if (obj == null)
		{
			Debug.LogWarning($"[DemoPlayer] AnimationPlay: Object not found for managedId={managedId}");
			yield break;
		}

		var animator = obj.GetComponentInChildren<Animator>();
		if (animator == null)
		{
			Debug.LogWarning($"[DemoPlayer] AnimationPlay: No Animator found on managedId={managedId}");
			yield break;
		}

		animator.Play(stateName);
		Debug.Log($"[DemoPlayer] AnimationPlay: state={stateName}, managedId={managedId}");
		yield break;
	}

	/// <summary>管理IDのオブジェクトのアニメーション完了を待つ</summary>
	private IEnumerator StepWaitAnimation(string stateName, int managedId)
	{
		var obj = GetManagedObject(managedId);
		if (obj == null)
		{
			Debug.LogWarning($"[DemoPlayer] WaitAnimation: Object not found for managedId={managedId}");
			yield break;
		}

		var animator = obj.GetComponentInChildren<Animator>();
		if (animator == null)
		{
			Debug.LogWarning($"[DemoPlayer] WaitAnimation: No Animator found on managedId={managedId}");
			yield break;
		}

		// 1フレーム待ってからステート情報を取得
		yield return null;

		var stateInfo = animator.GetCurrentAnimatorStateInfo(0);

		if (string.IsNullOrEmpty(stateName))
		{
			// 空文字の場合：現在のステートの完了を待つ
			Debug.Log($"[DemoPlayer] WaitAnimation: Waiting for current state to complete, managedId={managedId}");
			while (true)
			{
				stateInfo = animator.GetCurrentAnimatorStateInfo(0);
				if (stateInfo.normalizedTime >= 1f && !animator.IsInTransition(0))
				{
					break;
				}
				yield return null;
			}
		}
		else
		{
			// 指定ステートの完了を待つ
			Debug.Log($"[DemoPlayer] WaitAnimation: Waiting for state={stateName}, managedId={managedId}");

			// 指定ステートに到達するまで待つ
			int stateHash = Animator.StringToHash(stateName);
			float timeout = 10f;
			float elapsed = 0f;

			while (elapsed < timeout)
			{
				stateInfo = animator.GetCurrentAnimatorStateInfo(0);
				if (stateInfo.shortNameHash == stateHash)
				{
					break;
				}
				elapsed += Time.deltaTime;
				yield return null;
			}

			// ステートの完了を待つ
			while (true)
			{
				stateInfo = animator.GetCurrentAnimatorStateInfo(0);
				if (stateInfo.shortNameHash == stateHash &&
					stateInfo.normalizedTime >= 1f &&
					!animator.IsInTransition(0))
				{
					break;
				}

				// ステートがすでに別のものに遷移していたら終了
				if (stateInfo.shortNameHash != stateHash && !animator.IsInTransition(0))
				{
					break;
				}

				yield return null;
			}
		}

		Debug.Log($"[DemoPlayer] WaitAnimation: Complete, managedId={managedId}");
	}

	/// <summary>カメラ位置をアニメーション付きで設定</summary>
	private IEnumerator StepSetCameraPositionAnimated(Vector2 targetPosition, string durationParam)
	{
		float duration = 0f;
		if (!string.IsNullOrEmpty(durationParam))
		{
			float.TryParse(durationParam, out duration);
			duration /= 1000f; // ミリ秒→秒に変換
		}

		var root = graphUIManager.graphRoot;
		var targetPos = new Vector3(targetPosition.x, targetPosition.y, root.localPosition.z);

		if (duration <= 0f)
		{
			// 即座に設定
			root.localPosition = targetPos;
			Debug.Log($"[DemoPlayer] SetCameraPosition(instant): {targetPos}");
		}
		else
		{
			// アニメーション
			var startPos = root.localPosition;
			float elapsed = 0f;

			Debug.Log($"[DemoPlayer] SetCameraPosition(animated): {startPos} -> {targetPos}, duration={duration}s");

			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				float eased = EaseInOutCubic(t);
				root.localPosition = Vector3.Lerp(startPos, targetPos, eased);
				yield return null;
			}

			root.localPosition = targetPos;
		}
	}

	/// <summary>カメラスケールをアニメーション付きで設定</summary>
	private IEnumerator StepSetCameraScaleAnimated(Vector3 targetScale, string durationParam)
	{
		float duration = 0f;
		if (!string.IsNullOrEmpty(durationParam))
		{
			float.TryParse(durationParam, out duration);
			duration /= 1000f; // ミリ秒→秒に変換
		}

		var root = graphUIManager.graphRoot;

		if (duration <= 0f)
		{
			root.localScale = targetScale;
			Debug.Log($"[DemoPlayer] SetCameraScale(instant): {targetScale}");
		}
		else
		{
			var startScale = root.localScale;
			float elapsed = 0f;

			Debug.Log($"[DemoPlayer] SetCameraScale(animated): {startScale} -> {targetScale}, duration={duration}s");

			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				float eased = EaseInOutCubic(t);
				root.localScale = Vector3.Lerp(startScale, targetScale, eased);
				yield return null;
			}

			root.localScale = targetScale;
		}
	}

	/// <summary>画面タップを待つ</summary>
	private IEnumerator StepWaitClick()
	{
		Debug.Log($"[DemoPlayer] WaitClick: Waiting for screen tap...");
		while (true)
		{
			if (Input.GetMouseButtonDown(0))
			{
				break;
			}
			yield return null;
		}
	}

	// =====================================================================
	// ユーティリティ
	// =====================================================================

	/// <summary>管理IDにオブジェクトを登録（ID=0は無視）</summary>
	private void RegisterManagedObject(int managedId, GameObject obj)
	{
		if (managedId <= 0 || obj == null) return;

		// 既存オブジェクトがあれば上書き（古い方は削除しない＝呼び出し側の責任）
		managedObjects[managedId] = obj;
	}

	/// <summary>管理IDからオブジェクトを取得</summary>
	private GameObject GetManagedObject(int managedId)
	{
		if (managedId <= 0) return null;
		managedObjects.TryGetValue(managedId, out var obj);
		return obj;
	}

	/// <summary>管理IDのオブジェクトを破棄</summary>
	private void DestroyManagedObject(int managedId)
	{
		if (managedId <= 0) return;

		if (managedObjects.TryGetValue(managedId, out var obj) && obj != null)
		{
			Destroy(obj);
		}
		managedObjects.Remove(managedId);
		managedNodes.Remove(managedId);
	}

	/// <summary>ノードの初期レベルを取得</summary>
	private int GetInitialLevel(int nodeId)
	{
		if (MasterData.Instance.NodeIoDatas.SelectId.TryGetValue(nodeId, out var ioArray))
		{
			// Level=0のデータがあればレベル0（水処理・発電などの基礎ユニット）
			if (ioArray.Any(io => io.Level == 0))
				return 0;
			// なければ最小レベル
			return ioArray.Min(io => io.Level);
		}
		return 1;
	}

	/// <summary>イージング関数（Cubic InOut）</summary>
	private static float EaseInOutCubic(float t)
	{
		return t < 0.5f
			? 4f * t * t * t
			: 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
	}

	// =====================================================================
	// フォールバック（データが見つからない場合の既存ハードコード処理）
	// =====================================================================

	private List<IEnumerator> BuildStepsFallback(int demoId)
	{
		switch (demoId)
		{
			case 1: return BuildDemo1();
			case 2: return BuildDemo2();
			case 3: return BuildDemo3();
			case 4: return BuildDemo4();
			case 5: return BuildDemo5();
			case 6: return BuildDemo6();
			default:
				Debug.LogWarning($"[DemoPlayer] Unknown demoId: {demoId}");
				return null;
		}
	}

	private List<IEnumerator> BuildDemo1()
	{
		return new List<IEnumerator>
		{
			StepSetCameraPosition(new Vector3(100, -75, 0), new Vector3(1.5f, 1.5f, 1)),
			StepWait(0.3f),
			StepCreateNode(NodeId.水処理ユニット, 0, new Vector2(-550, 200)),
			StepWait(defaultStepInterval),
			StepCreateNode(NodeId.発電ユニット, 0, new Vector2(-550, -100)),
			StepWait(defaultStepInterval),
			StepCreateNode(NodeId.農業モジュール, 1, new Vector2(-125, 275)),
			StepWait(defaultStepInterval),
			StepCreateNode(NodeId.居住モジュール, 1, new Vector2(225, -25)),
			StepWait(defaultPostDelay),
		};
	}

	private List<IEnumerator> BuildDemo2()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 2: エッジのつなぎ方を学ぶ"),
			StepWait(defaultPostDelay),
		};
	}

	private List<IEnumerator> BuildDemo3()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 3: レベルアップを学ぶ"),
			StepWait(defaultPostDelay),
		};
	}

	private List<IEnumerator> BuildDemo4()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 4: 資金の使い方を学ぶ"),
			StepWait(defaultPostDelay),
		};
	}

	private List<IEnumerator> BuildDemo5()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 5: 区の方針決め"),
			StepWait(defaultPostDelay),
		};
	}

	private List<IEnumerator> BuildDemo6()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 6: 第2章開始"),
			StepWait(defaultPostDelay),
		};
	}

	// =====================================================================
	// 既存ステップ用コルーチンビルダー（フォールバック用に維持）
	// =====================================================================

	private IEnumerator StepWait(float seconds)
	{
		yield return new WaitForSeconds(seconds);
	}

	private IEnumerator StepLog(string message)
	{
		Debug.Log($"[DemoPlayer] {message}");
		yield break;
	}

	private IEnumerator StepSetCameraPosition(Vector3 position, Vector3 scale)
	{
		var root = graphUIManager.graphRoot;
		root.localPosition = position;
		root.localScale = scale;
		Debug.Log($"[DemoPlayer] Camera set to pos={position}, scale={scale}");
		yield break;
	}

	private IEnumerator StepCreateNode(int nodeId, int level, Vector2 position)
	{
		var node = graphUIManager.CreateNodeFromData(nodeId, level, position);
		if (node != null)
		{
			Debug.Log($"[DemoPlayer] Created node: {node.titleText.text} at {position}");
		}
		else
		{
			Debug.LogWarning($"[DemoPlayer] Failed to create node: id={nodeId}, level={level}");
		}
		yield break;
	}

	private IEnumerator StepConnectEdge(NodeView fromNode, int fromPortIndex, NodeView toNode, int toPortIndex)
	{
		if (fromNode == null || toNode == null)
		{
			Debug.LogWarning("[DemoPlayer] StepConnectEdge: node is null");
			yield break;
		}
		if (fromPortIndex < 0 || fromPortIndex >= fromNode.outputPorts.Count)
		{
			Debug.LogWarning($"[DemoPlayer] StepConnectEdge: fromPortIndex {fromPortIndex} out of range");
			yield break;
		}
		if (toPortIndex < 0 || toPortIndex >= toNode.inputPorts.Count)
		{
			Debug.LogWarning($"[DemoPlayer] StepConnectEdge: toPortIndex {toPortIndex} out of range");
			yield break;
		}

		var outPort = fromNode.outputPorts[fromPortIndex];
		var inPort = toNode.inputPorts[toPortIndex];

		if (!graphUIManager.CanConnect(outPort, inPort))
		{
			Debug.LogWarning($"[DemoPlayer] StepConnectEdge: cannot connect {outPort.resourceType} -> {inPort.resourceType}");
			yield break;
		}

		inPort.RemoveEdgeAll();

		var edge = Instantiate(graphUIManager.edgePrefab, graphUIManager.edgesLayer);
		edge.Initialize(graphUIManager, isPreview: false);
		edge.BindPorts(outPort, inPort);

		Debug.Log($"[DemoPlayer] Connected edge: {fromNode.titleText.text}[out:{fromPortIndex}] -> {toNode.titleText.text}[in:{toPortIndex}]");
		yield break;
	}

	private IEnumerator StepRemoveNode(NodeView node)
	{
		if (node == null) yield break;
		Debug.Log($"[DemoPlayer] Removing node: {node.titleText.text}");
		graphUIManager.RemoveNode(node);
		yield break;
	}

	private IEnumerator StepClearGraph()
	{
		graphUIManager.ClearGraph();
		Debug.Log("[DemoPlayer] Graph cleared");
		yield break;
	}

	private IEnumerator StepAction(Action action)
	{
		action?.Invoke();
		yield break;
	}

	private List<NodeView> FindNodesByNodeId(int nodeId)
	{
		var result = new List<NodeView>();
		if (graphUIManager.nodeLayer == null) return result;

		foreach (Transform t in graphUIManager.nodeLayer)
		{
			var nv = t.GetComponent<NodeView>();
			if (nv != null && nv.nodeId == nodeId)
			{
				result.Add(nv);
			}
		}
		return result;
	}

	private NodeView FindFirstNode(int nodeId)
	{
		if (graphUIManager.nodeLayer == null) return null;
		foreach (Transform t in graphUIManager.nodeLayer)
		{
			var nv = t.GetComponent<NodeView>();
			if (nv != null && nv.nodeId == nodeId) return nv;
		}
		return null;
	}
}
