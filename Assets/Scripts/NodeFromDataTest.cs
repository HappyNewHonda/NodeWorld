using System.Collections;
using Define;
using Effects;
using UnityEngine;

/// <summary>
/// データからノードを生成するテスト用スクリプト
/// </summary>
public class NodeFromDataTest : MonoBehaviour
{
	public GraphUIManager manager;
	public GlobalEffectController globalEffectController;

	void Start()
	{/*
		// テスト用にいくつかのノードを生成
		manager.CreateNodeFromData(nodeId: NodeId.水処理ユニット, level: 0, position: new Vector2(0, 100));
		var a = manager.CreateNodeFromData(nodeId: NodeId.発電ユニット, level: 0, position: new Vector2(0, -100));
		a.GetComponent<NodeEffectController>().SetNodeEffects(new[]
		{
				MasterData.Instance.EffectDatas.SelectId[EffectId.テスト2]
		});
		manager.CreateNodeFromData(nodeId: NodeId.農業モジュール, level: 1, position: new Vector2(400, -200));
		manager.CreateNodeFromData(nodeId: NodeId.居住モジュール, level: 1, position: new Vector2(400, 0));

		NodeView[] garekis = new[]
		{
			manager.CreateNodeFromData(nodeId: NodeId.瓦礫, level: 1, position: new Vector2(600, -100)),
			manager.CreateNodeFromData(nodeId: NodeId.瓦礫, level: 2, position: new Vector2(600, 100)),
			manager.CreateNodeFromData(nodeId: NodeId.瓦礫, level: 1, position: new Vector2(600, 300)),
		};
		foreach(var node in garekis)
		{
			node.GetComponent<NodeEffectController>().SetNodeEffects(new[]
			{
				MasterData.Instance.EffectDatas.SelectId[EffectId.瓦礫の除去]
			});
		}

		globalEffectController.SetGlobalEffects(new[]
		{
				MasterData.Instance.EffectDatas.SelectId[EffectId.テスト]
		});*/
	}
}
