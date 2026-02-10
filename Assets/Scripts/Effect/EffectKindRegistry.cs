using System.Collections.Generic;

namespace Effects
{
    public enum EffectLogicalKind
	{
		Unknown = 1,
        Node_LevelUp,      // ノードレベルアップ
        Node_MaxLevelUp,   // ノード最大レベルアップ
        Node_InputCostChange_Percent,  // ← ±は別管理（Typeで判定）
        Node_OutputValueChange_Percent, // ← ±は別管理（Typeで判定）
        Node_AddOutputResource, // 追加出力リソース
        Node_AddInputResource,  // 追加入力リソース
        Node_RemoveByOutputCount, // 出力回数による除去
        Global_OpenNewNode, // 新規ノード解放
	}
}