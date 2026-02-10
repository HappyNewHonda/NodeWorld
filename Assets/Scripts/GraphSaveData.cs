using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// グラフ全体のセーブデータ
/// </summary>
[Serializable]
public class GraphSaveData
{
	public List<SavedNode> nodes = new List<SavedNode>();
	public List<SavedEdge> edges = new List<SavedEdge>();
	public string saveVersion = "1.0";
	public string saveTimestamp;
	public SavedUserData userData; // ユーザーデータ（お金など）
	public List<int> globalEffects;
	public List<SavedGlobalEffectState> globalEffectStates;


	public GraphSaveData()
	{
		saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
	}
}

/// <summary>
/// ノードのセーブデータ
/// </summary>
[Serializable]
public class SavedNode
{
	public int nodeId;
	public int nodeLevel;
	public float posX;
	public float posY;
	public int[] inputQuantities;    // 各入力ポートの所持数
	public int[] outputQuantities;   // 各出力ポートの所持数
	public float productionProgress; // 生産進行度（0～1）
	public bool isProducing;         // 生産中フラグ
	public int[] activeEffects;      // EffectData.
	public SavedNodeEffectState[] effectStates;
}

/// <summary>
/// ノード内の「状態を持つエフェクト」の現在値を保存する汎用DTO
/// 必要な項目だけ使う（未使用は既定値 or 省略）
/// </summary>
[Serializable]
public class SavedNodeEffectState
{
	public int typeId;             // EffectType（= EffectLogicalKind と1:1対応）
	public int remainingCount;     // 例：Node_RemoveByOutputCount の「残り回数」
	public float durationLeftSec;  // 例：寿命エフェクトの残り時間（将来拡張）
}

/// <summary>
/// エッジのセーブデータ
/// </summary>
[Serializable]
public class SavedEdge
{
	public int fromNodeIndex;  // nodes配列のインデックス
	public int fromPortIndex;  // 出力ポート番号
	public int toNodeIndex;    // nodes配列のインデックス
	public int toPortIndex;    // 入力ポート番号
}

[Serializable]
public class SavedGlobalEffectState
{
	public int typeId;
	public float durationLeftSec;
}
