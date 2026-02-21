// Assets/Scripts/Effects/EffectTypeBucket.cs
using System.Collections.Generic;
using Data.Master;

namespace Effects
{
	public class EffectTypeBucket
	{
		public int typeId;
		public string displayName;
		public string description;
		public int valueSum;
		public float durationSecSum; // EffectData.Duration（ms/秒表現はプロジェクト規約に合わせる）
		public bool isPermanent;     // ViewType==100 を“永続”扱いにするなら true
		public bool canStack = true; // 本件では常に true で良い（EffectData側に可否が無ければ）

		public bool affectsAllNodes;
		public bool affectsAllResources;
		public HashSet<int> targetNodes = new();
		public HashSet<int> targetResources = new();

		public EffectTypeBucket(int typeId) { this.typeId = typeId; }

		public void Add(EffectData d)
		{
			if (displayName == null) displayName = d.DisplayName;

			// EffectData.Type は「種別ID（=typeId）」なので、ここでは合算対象の数値を積む
			valueSum += d.Value;

			// Duration：将来のPhase6でtickする想定。ここでは合算しておく（必要ならmax等に変更可）
			// EffectData.Duration が -1 など無期限表現なら 0 足しで良い
			if (d.Duration > 0) durationSecSum += d.Duration;

			// 対象ノード/資源のマージ
			if (d.TargetNodes == null || d.TargetNodes.Length == 0) affectsAllNodes = true;
			else if (!affectsAllNodes) foreach (var n in d.TargetNodes) targetNodes.Add(n);

			if (d.TargetResources == null || d.TargetResources.Length == 0) affectsAllResources = true;
			else if (!affectsAllResources) foreach (var r in d.TargetResources) targetResources.Add(r);

			// “永続”(称号)扱いは ViewType==100 を採用
			if (!isPermanent)
			{
				var t = MasterData.Instance.EffectTypeDatas.SelectTypeId[typeId];
				isPermanent = (t.ViewType == 100);
			}
		}

		public bool IsTargetNode(int nodeId) => affectsAllNodes || targetNodes.Contains(nodeId);
		public bool IsTargetResource(int resId) => affectsAllResources || targetResources.Contains(resId);
	}
}