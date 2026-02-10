using System.Collections.Generic;
using Define;

namespace Effects
{
    /// <summary>
    /// 1ノードに対する効果の合算結果（リソース別）。
    /// ※この段階では「数値の最終結果」ではなく「補正量の集計」を保持する。
    /// </summary>
    public class EffectAggregates
    {
        // リソース別（0=全て含む）%補正：入力必要量
        public readonly Dictionary<int, int> inputPercentByRes = new();

        // リソース別（0=全て含む）%補正：出力量
        public readonly Dictionary<int, int> outputPercentByRes = new();

		// 追加入力：リソース→加算個数
		public readonly Dictionary<int, int> extraInputsByRes = new();
		// 追加出力（副産物など）：リソース→加算個数
		public readonly Dictionary<int, int> extraOutputsByRes = new();

		// ノードの「N回出力で除去」カウント（合算）
		public int removeOnOutputsCount = 0;

        // 表示用（テキスト/称号）
        public readonly List<string> textEffects = new();

        public void Merge(EffectAggregates other)
        {
            AccumulateDict(inputPercentByRes, other.inputPercentByRes);
            AccumulateDict(outputPercentByRes, other.outputPercentByRes);
			AccumulateDict(extraInputsByRes, other.extraInputsByRes);
			AccumulateDict(extraOutputsByRes, other.extraOutputsByRes);
			removeOnOutputsCount += other.removeOnOutputsCount;
            textEffects.AddRange(other.textEffects);
        }

        private static void AccumulateDict(Dictionary<int, int> dst, Dictionary<int, int> src)
        {
            foreach (var kv in src)
            {
                if (dst.ContainsKey(kv.Key)) dst[kv.Key] += kv.Value;
                else dst[kv.Key] = kv.Value;
            }
        }
    }
}