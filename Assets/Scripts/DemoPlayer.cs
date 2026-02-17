using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Define;

/// <summary>
/// デモ再生エンジン。
/// 各DemoIdに対応するステップ列（コルーチン）を順次実行する。
/// </summary>
public class DemoPlayer : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private GraphUIManager graphUIManager;

	[Header("Timing Defaults")]
	[SerializeField] private float defaultStepInterval = 0.6f;
	[SerializeField] private float defaultPostDelay = 0.5f;

	/// <summary>
	/// 指定したDemoIdのデモを再生する（コルーチン）。
	/// GameFlowManagerから呼ばれる。
	/// </summary>
	public IEnumerator Play(int demoId)
	{
		Debug.Log($"[DemoPlayer] Starting demo {demoId}");

		var steps = BuildSteps(demoId);
		if (steps == null || steps.Count == 0)
		{
			Debug.LogWarning($"[DemoPlayer] No steps defined for demoId: {demoId}");
			yield break;
		}

		foreach (var step in steps)
		{
			yield return StartCoroutine(step);
		}

		Debug.Log($"[DemoPlayer] Demo {demoId} completed");
	}

	/// <summary>
	/// DemoIdに応じたステップ列を構築する。
	/// 新しいデモを追加する場合はここにcaseを足す。
	/// </summary>
	private List<IEnumerator> BuildSteps(int demoId)
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

	// =====================================================================
	// デモ定義
	// =====================================================================

	/// <summary>
	/// Demo 1: 初期配置（水処理・発電・農業・居住ノードを配置）
	/// </summary>
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

	/// <summary>
	/// Demo 2: エッジのつなぎ方チュートリアル
	/// </summary>
	private List<IEnumerator> BuildDemo2()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 2: エッジのつなぎ方を学ぶ"),
			StepWait(defaultPostDelay),
		};
	}

	/// <summary>
	/// Demo 3: レベルアップチュートリアル
	/// </summary>
	private List<IEnumerator> BuildDemo3()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 3: レベルアップを学ぶ"),
			StepWait(defaultPostDelay),
		};
	}

	/// <summary>
	/// Demo 4: 資金と出力増加チュートリアル
	/// </summary>
	private List<IEnumerator> BuildDemo4()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 4: 資金の使い方を学ぶ"),
			StepWait(defaultPostDelay),
		};
	}

	/// <summary>
	/// Demo 5: 区の方針決めデモ
	/// </summary>
	private List<IEnumerator> BuildDemo5()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 5: 区の方針決め"),
			StepWait(defaultPostDelay),
		};
	}

	/// <summary>
	/// Demo 6: 第2章導入
	/// </summary>
	private List<IEnumerator> BuildDemo6()
	{
		return new List<IEnumerator>
		{
			StepLog("Demo 6: 第2章開始"),
			StepWait(defaultPostDelay),
		};
	}

	// =====================================================================
	// ステップ用コルーチンビルダー
	// =====================================================================

	/// <summary>待機</summary>
	private IEnumerator StepWait(float seconds)
	{
		yield return new WaitForSeconds(seconds);
	}

	/// <summary>ログ出力</summary>
	private IEnumerator StepLog(string message)
	{
		Debug.Log($"[DemoPlayer] {message}");
		yield break;
	}

	/// <summary>カメラ（GraphRoot）の位置とスケールを設定</summary>
	private IEnumerator StepSetCameraPosition(Vector3 position, Vector3 scale)
	{
		var root = graphUIManager.graphRoot;
		root.localPosition = position;
		root.localScale = scale;
		Debug.Log($"[DemoPlayer] Camera set to pos={position}, scale={scale}");
		yield break;
	}

	/// <summary>ノードを生成</summary>
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

	/// <summary>
	/// 2つのノード間にエッジを接続する。
	/// fromNode/toNodeはnodeLayer内のインデックスではなく、直接参照で渡す。
	/// </summary>
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

		// 既存エッジを除去（入力は1本制約）
		inPort.RemoveEdgeAll();

		var edge = Instantiate(graphUIManager.edgePrefab, graphUIManager.edgesLayer);
		edge.Initialize(graphUIManager, isPreview: false);
		edge.BindPorts(outPort, inPort);

		Debug.Log($"[DemoPlayer] Connected edge: {fromNode.titleText.text}[out:{fromPortIndex}] -> {toNode.titleText.text}[in:{toPortIndex}]");
		yield break;
	}

	/// <summary>ノードを削除</summary>
	private IEnumerator StepRemoveNode(NodeView node)
	{
		if (node == null) yield break;
		Debug.Log($"[DemoPlayer] Removing node: {node.titleText.text}");
		graphUIManager.RemoveNode(node);
		yield break;
	}

	/// <summary>グラフをクリア</summary>
	private IEnumerator StepClearGraph()
	{
		graphUIManager.ClearGraph();
		Debug.Log("[DemoPlayer] Graph cleared");
		yield break;
	}

	/// <summary>任意のActionを実行（柔軟な拡張用）</summary>
	private IEnumerator StepAction(Action action)
	{
		action?.Invoke();
		yield break;
	}

	/// <summary>
	/// nodeLayer内の全NodeViewから、指定nodeIdに一致するものを検索して返す。
	/// 複数ある場合はリストで返す。デモ定義で「生成したノードを後で接続する」ときに使う。
	/// </summary>
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

	/// <summary>
	/// 指定nodeIdのノードのうち最初に見つかったものを返す（便利メソッド）
	/// </summary>
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
