// Assets/Scripts/Effects/NodeEffectController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Define;
using Data.Master;
using Effects;

[RequireComponent(typeof(NodeView))]
public class NodeEffectController : MonoBehaviour
{
	public event Action OnNodeEffectsChanged;

	private readonly Dictionary<int /*typeId*/, EffectTypeBucket> localBuckets = new();
	private readonly List<EffectData> nodeEffectsRaw = new();

	private NodeView node;
	public int NodeId { get { return node.nodeId; } }

	// ----- ベース値保持（累積適用を避けるため、初回キャプチャして以後は常にベースから再計算） -----
	private readonly Dictionary<PortView, int> _baseInputRequired = new();
	private readonly Dictionary<PortView, int> _baseOutputProduce = new();

	// 追加出力（副産物）ターゲット：resourceId → 1サイクル当たり加算量
	// ※ 実在する出力ポートが無いリソースはログ警告のみ（フェーズ3でUI/ポート拡張予定）
	private readonly Dictionary<int, int> _extraOutputsPerCycle = new();

	// 瓦礫除去など「出力N回で除去」
	private int _outputsToRemove = 0;

	// 寿命（秒）を個別管理
	private readonly Dictionary<int/*typeId*/, float> localEffectRemainingSec = new();

	[SerializeField] float tickIntervalSec = 1.0f;
	float acc = 0f;

	void Awake()
	{
		node = GetComponent<NodeView>();
	}

	void OnDestroy()
	{
		GlobalEffectController.Instance.RemoveNodeEffectController(this);
	}

	void Update()
	{
		if (tickIntervalSec <= 0f) return;

		acc += Time.deltaTime;
		if (acc < tickIntervalSec) return;

		acc -= tickIntervalSec;
		TickLocalEffects(tickIntervalSec);

		// 残り時間の表示更新
		RefreshAllBadgeTimes();
	}

	/// <summary>このノードに効いている指定タイプ群の「残寿命（max）」を再計算してバッジに反映</summary>
	public void RefreshBadgeTimesForTypes(IEnumerable<int> typeIds)
	{
		if (typeIds == null) return;
		foreach (var typeId in typeIds)
		{
			float sec = GetRemainingSecondsForType(typeId);
			node.UpdateBadgeTime(typeId, sec);
		}
	}

	/// <summary>このノードに効く全タイプの残寿命表示を更新（毎秒の定期更新用）</summary>
	public void RefreshAllBadgeTimes()
	{
		// ローカルに存在するタイプ
		var typeIds = new HashSet<int>(localBuckets.Keys);
		// グローバルでこのノードに効くタイプも追加
		foreach (var b in GlobalEffectController.Instance.EnumerateBuckets())
		{
			if (b.IsTargetNode(NodeId)) typeIds.Add(b.typeId);
		}
		RefreshBadgeTimesForTypes(typeIds);
	}

	/// <summary>typeId の残寿命（秒）。ローカルとグローバルの最大値を採用（最後まで残る時間）</summary>
	private float GetRemainingSecondsForType(int typeId)
	{
		float localMax = -1f;
		foreach (var d in nodeEffectsRaw)
		{
			if (d == null || d.Type != typeId) continue;
			if (d.Duration <= 0) continue; // 無期限は時間表示しない
			if (localEffectRemainingSec.TryGetValue(d.Type, out var sec))
			{
				if (sec > localMax) localMax = sec;
			}
		}
		float globalMax = GlobalEffectController.Instance
			.GetMaxRemainingSecondsForNodeAndType(NodeId, typeId);
		return Mathf.Max(localMax, globalMax);
	}


	void TickLocalEffects(float dt)
	{
		if (localEffectRemainingSec.Count == 0) return;

		var expired = new List<int>();
		foreach (var kv in localEffectRemainingSec.ToArray())
		{
			float rest = kv.Value - dt;
			if (rest <= 0f)
			{
				expired.Add(kv.Key);
				localEffectRemainingSec.Remove(kv.Key);
			}
			else
			{
				localEffectRemainingSec[kv.Key] = rest;
			}
		}
		if (expired.Count == 0) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
		EffectLogger.Log(EffectLogger.Category.Effects, $"LocalExpire node={NodeId} effects=[{string.Join(",", expired)}]");
#endif

		// 失効：nodeEffectsRaw から除去
		nodeEffectsRaw.RemoveAll(d => expired.Contains(d.Type));

		// ローカルバケットを再構築（必要タイプのみ最小再構築）
		// 最小差分でもOKだが、実装シンプルさ優先で全再構築→対象 typeId のみ Reapply
		localBuckets.Clear();
		foreach (var e in nodeEffectsRaw)
		{
			int typeId = e.Type;
			if (!localBuckets.TryGetValue(typeId, out var bucket))
			{
				bucket = new EffectTypeBucket(typeId);
				localBuckets.Add(typeId, bucket);
			}
			bucket.Add(e);
		}

		foreach (var t in expired) ReapplyType(t); // 既存APIで差分反映
		OnNodeEffectsChanged?.Invoke(); // バッジ更新などのトリガ

		// 失効タイプの時間表示を即時更新
		RefreshBadgeTimesForTypes(expired);
	}


	public void Setp()
	{
		GlobalEffectController.Instance.AddNodeEffectController(this);
	}

	public void SetNodeEffects(IEnumerable<EffectData> effects)
	{
		localBuckets.Clear();
		nodeEffectsRaw.Clear();
		localEffectRemainingSec.Clear();

		foreach (var e in effects ?? Enumerable.Empty<EffectData>())
		{
			if (e == null) continue;
			int typeId = e.Type;
			if (!localBuckets.TryGetValue(typeId, out var bucket))
			{
				bucket = new EffectTypeBucket(typeId);
				localBuckets.Add(typeId, bucket);
			}
			bucket.Add(e);
			nodeEffectsRaw.Add(e);
			if (e.Duration > 0)
			{
				localEffectRemainingSec[e.Type] = e.Duration;
			}
		}

		OnNodeEffectsChanged?.Invoke();
		ApplyAll();

		// 生成直後に初期時間を反映
		RefreshAllBadgeTimes();
	}

	public void AddLocalEffect(EffectData e)
	{
		if (e == null) return;

		int typeId = e.Type;
		if (!localBuckets.TryGetValue(typeId, out var bucket))
		{
			bucket = new EffectTypeBucket(typeId);
			localBuckets.Add(typeId, bucket);
		}
		bucket.Add(e);
		nodeEffectsRaw.Add(e);

		OnNodeEffectsChanged?.Invoke();
		ReapplyType(typeId); // ←差分再適用（下で実装）
	}

	// EffectData 受けの削除（typeId 一括）
	public int RemoveLocalEffects(EffectData d)
	{
		if (d == null) return 0;
		int typeId = d.Type;
		int beforeCount = nodeEffectsRaw.Count;
		nodeEffectsRaw.RemoveAll(x => x != null && x.Type == typeId);
		bool removed = localBuckets.Remove(typeId);
		if (!removed && nodeEffectsRaw.Count == beforeCount) return 0;
		EnsureBaselinesCaptured();
		ReapplyType(typeId);
		OnNodeEffectsChanged?.Invoke();
		return 1;
	}

	// ベース値が未キャプチャのときに呼ぶ
	private void EnsureBaselinesCaptured()
	{
		if (node == null) return;

		// Input: PortView.RequiredAmount
		foreach (var p in node.inputPorts)
		{
			if (p == null) continue;
			if (_baseInputRequired.ContainsKey(p) == false)
				_baseInputRequired[p] = p.RequiredAmount;
		}

		// Output: PortView.ProduceAmount
		foreach (var p in node.outputPorts)
		{
			if (p == null) continue;
			if (_baseOutputProduce.ContainsKey(p) == false)
				_baseOutputProduce[p] = p.ProduceAmount;
		}
	}

	// NodeView側のポート構成が変わった（例：リソースバッファのStepper変更）ときに呼ぶ
	public void ResetBaselines()
	{
		_baseInputRequired.Clear();
		_baseOutputProduce.Clear();
	}

	/// <summary>
	/// 効果をポート/ノードへ反映（ベース値から再計算）。
	/// </summary>
	public void ApplyAll()
	{
		EnsureBaselinesCaptured();

		// 1) グローバル＋ローカルのタイプバケットを列挙
		var allBuckets = new List<EffectTypeBucket>();
		// グローバル
		foreach (var gb in GlobalEffectController.Instance.EnumerateBuckets())
		{
			if (gb.IsTargetNode(NodeId))
			{
				allBuckets.Add(gb);
			}
		}
		// ローカル
		allBuckets.AddRange(localBuckets.Values);

		// 2) 一旦 EffectAggregates に落としてから既存 Apply ロジックを流用
		var agg = BucketsToAggregates(allBuckets);

		// 1) 入力ポート：RequiredAmount を再計算
		foreach (var p in node.inputPorts.ToArray())
		{
			if (p == null) continue;

			// ベースを取得（無ければ現在値をベースに採用）
			if (_baseInputRequired.TryGetValue(p, out var baseReq) == false)
			{
				baseReq = p.RequiredAmount;
				_baseInputRequired[p] = baseReq;
			}

			// リソース別 % / 平加 を合成（resourceId と 0=全て を合算）
			int resId = (int)p.resourceType;

			int percent = GetFromDict(agg.inputPercentByRes, resId) + GetFromDict(agg.inputPercentByRes, ResourceId.全て);
			int flat = GetFromDict(agg.extraInputsByRes, resId) + GetFromDict(agg.extraInputsByRes, ResourceId.全て);
			int req = ApplyPercentCeil(baseReq, percent);
			req = Mathf.Max(0, req + flat);

			// MaxStock は既存実装に合わせて「必要数と同値」に保つ
			p.Initialize(maxStock: req, requiredOrProduceAmount: req);

			// リソースバッファなら、必要量の変化を踏まえて再計算
			if (p.IsResourceBuffer)
			{
				p.RecalculateResourceBufferValues();
			}
		}

		// 2) 出力ポート：ProduceAmount を再計算
		//    MaxStock は「生産量 × 10」の既存ルールに追随（NodeView.Setup と同様）
		foreach (var p in node.outputPorts.ToArray())
		{
			if (p == null) continue;

			// ベースを取得
			if (_baseOutputProduce.TryGetValue(p, out var baseProd) == false)
			{
				baseProd = p.ProduceAmount;
				_baseOutputProduce[p] = baseProd;
			}

			int resId = (int)p.resourceType;
			int percent = GetFromDict(agg.outputPercentByRes, resId) + GetFromDict(agg.outputPercentByRes, ResourceId.全て);
			int prod = ApplyPercentCeil(baseProd, percent);
			prod = Mathf.Max(0, prod);

			int maxStock = prod * 10;
			p.Initialize(maxStock: maxStock, requiredOrProduceAmount: prod);

			if (p.IsResourceBuffer)
			{
				p.RecalculateResourceBufferValues();
			}
		}

		// 3) 追加出力 副産物（リソース→加算個数/サイクル）を記憶
		_extraOutputsPerCycle.Clear();
		foreach (var kv in agg.extraOutputsByRes)
		{
			// outputPort が無いリソースは、ひとまずログのみ（フェーズ3でUI/ポート追加）
			bool hasPort = node.outputPorts.Any(op => (int)op.resourceType == kv.Key);
			if (!hasPort)
			{
				Debug.LogWarning($"[NodeEffectController] Extra output for resource={kv.Key} has no output port on nodeId={NodeId}. (Will be ignored until ports/UI are expanded)");
			}
			_extraOutputsPerCycle[kv.Key] = kv.Value;
		}

		// 4) 出力N回で除去（例：瓦礫）カウンタ設定
		_outputsToRemove = Mathf.Max(0, agg.removeOnOutputsCount);

		// バッジ表示（このノードに効いているタイプ群だけセット）
		var bucketsForThisNode = new List<EffectTypeBucket>();
		foreach (var gb in GlobalEffectController.Instance.EnumerateBuckets())
		{
			if (gb.IsTargetNode(NodeId)) bucketsForThisNode.Add(gb);
		}
		bucketsForThisNode.AddRange(localBuckets.Values);
		node.SetBadges(bucketsForThisNode);
		RefreshAllBadgeTimes();
	}

	/// <summary>
	/// 変更が入った「1タイプ」だけを再適用する軽量API。
	/// 将来的には ApplyAll の常用をやめ、このAPIを呼ぶ運用に移行。
	/// </summary>
	public void ReapplyType(int typeId)
	{
		EnsureBaselinesCaptured();

		// グローバル/ローカルの同typeIdバケットを準備
		EffectTypeBucket gb = GlobalEffectController.Instance
			.EnumerateBuckets()
			.FirstOrDefault(b => b.typeId == typeId && b.IsTargetNode(NodeId));
		localBuckets.TryGetValue(typeId, out var lb);

		var kind = (EffectLogicalKind)typeId;
		switch (kind)
		{
			case EffectLogicalKind.Node_InputCostChange_Percent:
				ReapplyInputPercent(typeId, gb, lb);
				break;

			case EffectLogicalKind.Node_AddInputResource:
				ReapplyAddInputFlat(typeId, gb, lb);
				break;

			case EffectLogicalKind.Node_OutputValueChange_Percent:
				ReapplyOutputPercent(typeId, gb, lb);
				break;

			case EffectLogicalKind.Node_AddOutputResource:
				ReapplyAddOutputFlat(typeId, gb, lb);
				break;

			case EffectLogicalKind.Node_RemoveByOutputCount:
				ReapplyRemovalCounter(gb, lb);
				break;

			default:
				// テキスト系などはバッジのみ差分
				var badgeBucket = MergeBucketTemp(typeId, gb, lb);
				if (badgeBucket != null)
				{
					node.UpsertBadgeForType(badgeBucket);
				}
				else
				{
					node.RemoveBadgeForType(typeId);
					node.UpdateBadgeTime(typeId, GetRemainingSecondsForType(typeId));
				}
				return;
		}

		// バッジ（このtypeIdのみ）差分更新
		if (kind == EffectLogicalKind.Node_RemoveByOutputCount)
		{
			if (_outputsToRemove > 0)
			{
				node.UpdateRemoveByOutputCountBadge(_outputsToRemove);
			}
			else
			{
				node.RemoveBadgeForType((int)EffectLogicalKind.Node_RemoveByOutputCount);
			}
		}
		else
		{
			var badgeBucket = MergeBucketTemp(typeId, gb, lb);
			if (badgeBucket != null)
			{
				node.UpsertBadgeForType(badgeBucket);
			}
			else
			{
				node.RemoveBadgeForType(typeId);
				node.UpdateBadgeTime(typeId, GetRemainingSecondsForType(typeId));
			}
		}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
		EffectLogger.LogVerbose(EffectLogger.Category.Effects, $"ReapplyType node={NodeId} type={typeId}");
#endif
	}

	// 単純合成（表示用・差分用）: gb と lb を足し合わせた一時バケット
	private static EffectTypeBucket MergeBucketTemp(int typeId, params EffectTypeBucket[] src)
	{
		EffectTypeBucket merged = null;
		foreach (var b in src)
		{
			if (b == null) continue;
			merged ??= new EffectTypeBucket(typeId);
			merged.valueSum += b.valueSum;
			merged.durationSecSum += b.durationSecSum;
			merged.displayName ??= b.displayName;
			merged.isPermanent |= b.isPermanent;
			merged.canStack &= b.canStack;
			merged.affectsAllNodes |= b.affectsAllNodes;
			merged.affectsAllResources |= b.affectsAllResources;
			if (!merged.affectsAllNodes) foreach (var n in b.targetNodes) merged.targetNodes.Add(n);
			if (!merged.affectsAllResources) foreach (var r in b.targetResources) merged.targetResources.Add(r);
		}
		return merged;
	}

	// 入力％補正：RequiredAmount を％で再計算（ALL と個別の合算）
	private void ReapplyInputPercent(int typeId, EffectTypeBucket gb, EffectTypeBucket lb)
	{
		int Sign() => MasterData.Instance.EffectTypeDatas.SelectTypeId[typeId].Evaluation;

		// 対象判定（このノードの入力ポートの rid に対し、ALL or rid が対象なら適用）
		bool Affects(int rid)
		{
			bool g = gb != null && (gb.affectsAllResources || gb.targetResources.Contains(rid) || gb.targetResources.Contains(ResourceId.全て));
			bool l = lb != null && (lb.affectsAllResources || lb.targetResources.Contains(rid) || lb.targetResources.Contains(ResourceId.全て));
			return g || l;
		}

		// rid ごとの％（ALL + rid の合算）
		int PercentFor(int rid)
		{
			int v = 0;
			if (gb != null)
			{
				if (gb.affectsAllResources || gb.targetResources.Contains(ResourceId.全て)) v += Sign() * gb.valueSum;
				if (gb.targetResources.Contains(rid)) v += Sign() * gb.valueSum;
			}
			if (lb != null)
			{
				if (lb.affectsAllResources || lb.targetResources.Contains(ResourceId.全て)) v += Sign() * lb.valueSum;
				if (lb.targetResources.Contains(rid)) v += Sign() * lb.valueSum;
			}
			return v;
		}

		foreach (var p in node.inputPorts.ToArray())
		{
			if (p == null) continue;
			int rid = (int)p.resourceType;
			if (!Affects(rid)) continue;

			if (_baseInputRequired.TryGetValue(p, out int baseReq) == false)
				_baseInputRequired[p] = baseReq = p.RequiredAmount;

			int percent = PercentFor(rid);
			int req = Mathf.Max(0, ApplyPercentCeil(baseReq, percent));
			// 入力は MaxStock=Required とする既存方針
			p.Initialize(maxStock: req, requiredOrProduceAmount: req);
			if (p.IsResourceBuffer) p.RecalculateResourceBufferValues();
		}
	}

	// 追加入力（個数）：RequiredAmount に「平加」する（ALL + rid）
	private void ReapplyAddInputFlat(int typeId, EffectTypeBucket gb, EffectTypeBucket lb)
	{
		int Sign() => MasterData.Instance.EffectTypeDatas.SelectTypeId[typeId].Evaluation;

		// このタイプは Sign は関係なく「値を加算」
		bool Affects(int rid)
		{
			bool g = gb != null && (gb.affectsAllResources || gb.targetResources.Contains(rid) || gb.targetResources.Contains(ResourceId.全て));
			bool l = lb != null && (lb.affectsAllResources || lb.targetResources.Contains(rid) || lb.targetResources.Contains(ResourceId.全て));
			return g || l;
		}
		int FlatFor(int rid)
		{
			int v = 0;
			if (gb != null)
			{
				if (gb.affectsAllResources || gb.targetResources.Contains(ResourceId.全て)) v += Sign() * gb.valueSum;
				if (gb.targetResources.Contains(rid)) v += Sign() * gb.valueSum;
			}
			if (lb != null)
			{
				if (lb.affectsAllResources || lb.targetResources.Contains(ResourceId.全て)) v += Sign() * lb.valueSum;
				if (lb.targetResources.Contains(rid)) v += Sign() * lb.valueSum;
			}
			return v;
		}

		foreach (var p in node.inputPorts.ToArray())
		{
			if (p == null) continue;
			int rid = (int)p.resourceType;
			if (!Affects(rid)) continue;

			if (_baseInputRequired.TryGetValue(p, out int baseReq) == false)
				_baseInputRequired[p] = baseReq = p.RequiredAmount;

			int req = Mathf.Max(0, baseReq + FlatFor(rid));
			p.Initialize(maxStock: req, requiredOrProduceAmount: req);
			if (p.IsResourceBuffer) p.RecalculateResourceBufferValues();
		}
	}

	// 出力％補正：ProduceAmount を％で再計算（ALL + rid）
	private void ReapplyOutputPercent(int typeId, EffectTypeBucket gb, EffectTypeBucket lb)
	{
		int Sign() => MasterData.Instance.EffectTypeDatas.SelectTypeId[typeId].Evaluation;

		bool Affects(int rid)
		{
			bool g = gb != null && (gb.affectsAllResources || gb.targetResources.Contains(rid) || gb.targetResources.Contains(ResourceId.全て));
			bool l = lb != null && (lb.affectsAllResources || lb.targetResources.Contains(rid) || lb.targetResources.Contains(ResourceId.全て));
			return g || l;
		}
		int PercentFor(int rid)
		{
			int v = 0;
			if (gb != null)
			{
				if (gb.affectsAllResources || gb.targetResources.Contains(ResourceId.全て)) v += Sign() * gb.valueSum;
				if (gb.targetResources.Contains(rid)) v += Sign() * gb.valueSum;
			}
			if (lb != null)
			{
				if (lb.affectsAllResources || lb.targetResources.Contains(ResourceId.全て)) v += Sign() * lb.valueSum;
				if (lb.targetResources.Contains(rid)) v += Sign() * lb.valueSum;
			}
			return v;
		}

		foreach (var p in node.outputPorts.ToArray())
		{
			if (p == null) continue;
			int rid = (int)p.resourceType;
			if (!Affects(rid)) continue;

			if (_baseOutputProduce.TryGetValue(p, out int baseProd) == false)
				_baseOutputProduce[p] = baseProd = p.ProduceAmount;

			int prod = Mathf.Max(0, ApplyPercentCeil(baseProd, PercentFor(rid)));
			int maxStock = prod * 10;
			p.Initialize(maxStock: maxStock, requiredOrProduceAmount: prod);
			if (p.IsResourceBuffer) p.RecalculateResourceBufferValues();
		}
	}

	// 追加出力（個数）：1サイクルごとの「副産物」量を辞書に保持（ALL + rid）
	private void ReapplyAddOutputFlat(int typeId, EffectTypeBucket gb, EffectTypeBucket lb)
	{
		// 影響資源集合（このノードの出力ポート基準で絞る）
		var affectedRes = new HashSet<int>();
		IEnumerable<int> NodeOutputResourceIds() => node.outputPorts.Select(op => (int)op.resourceType);

		if (gb != null)
		{
			if (gb.affectsAllResources || gb.targetResources.Contains(ResourceId.全て)) affectedRes.UnionWith(NodeOutputResourceIds());
			else affectedRes.UnionWith(gb.targetResources);
		}
		if (lb != null)
		{
			if (lb.affectsAllResources || lb.targetResources.Contains(ResourceId.全て)) affectedRes.UnionWith(NodeOutputResourceIds());
			else affectedRes.UnionWith(lb.targetResources);
		}

		int FlatFor(int rid)
		{
			int v = 0;
			if (gb != null)
			{
				if (gb.affectsAllResources || gb.targetResources.Contains(ResourceId.全て)) v += gb.valueSum;
				if (gb.targetResources.Contains(rid)) v += gb.valueSum;
			}
			if (lb != null)
			{
				if (lb.affectsAllResources || lb.targetResources.Contains(ResourceId.全て)) v += lb.valueSum;
				if (lb.targetResources.Contains(rid)) v += lb.valueSum;
			}
			return v;
		}

		foreach (var rid in affectedRes)
		{
			int val = FlatFor(rid);
			if (val == 0) _extraOutputsPerCycle.Remove(rid);
			else _extraOutputsPerCycle[rid] = val;
		}

		// 追加出力は UI 数値（ProduceAmount/MaxStock）には直接影響しないため、ポート再初期化は不要
		// ただし UI に「追加出力 +N」のバッジは ReapplyType 側の差分更新で反映される
	}

	private void ReapplyRemovalCounter(EffectTypeBucket gb, EffectTypeBucket lb)
	{
		int num = (gb?.valueSum ?? 0) + (lb?.valueSum ?? 0);
		_outputsToRemove = Mathf.Max(0, num);

		// バッジの差分更新のみ
		var merged = MergeBucketTemp((int)EffectLogicalKind.Node_RemoveByOutputCount, gb, lb);
		if (merged != null) node.UpsertBadgeForType(merged);
		else node.RemoveBadgeForType((int)EffectLogicalKind.Node_RemoveByOutputCount);
	}

	private static readonly int[] sAllRes = { ResourceId.全て };
	// バケット群を従来の EffectAggregates に変換（既存ApplyAllロジックの再利用に使う）
	private static EffectAggregates BucketsToAggregates(IEnumerable<EffectTypeBucket> buckets)
	{
		var agg = new EffectAggregates();
		foreach (var b in buckets)
		{
			var kind = (EffectLogicalKind)b.typeId;
			var sign = MasterData.Instance.EffectTypeDatas.SelectTypeId[b.typeId].Evaluation; // -1/0/+1
			int signedValue = sign * b.valueSum;

			switch (kind)
			{
				case EffectLogicalKind.Node_InputCostChange_Percent:
					foreach (var r in (IEnumerable<int>)(b.affectsAllResources ? sAllRes : b.targetResources))
						Add(agg.inputPercentByRes, r, signedValue);
					break;
				case EffectLogicalKind.Node_OutputValueChange_Percent:
					foreach (var r in (IEnumerable<int>)(b.affectsAllResources ? sAllRes : b.targetResources))
						Add(agg.outputPercentByRes, r, signedValue);
					break;
				case EffectLogicalKind.Node_AddInputResource:
					foreach (var r in (IEnumerable<int>)(b.affectsAllResources ? sAllRes : b.targetResources))
						Add(agg.extraInputsByRes, r, b.valueSum);
					break;
				case EffectLogicalKind.Node_AddOutputResource:
					foreach (var r in (IEnumerable<int>)(b.affectsAllResources ? sAllRes : b.targetResources))
						Add(agg.extraOutputsByRes, r, b.valueSum);
					break;
				case EffectLogicalKind.Node_RemoveByOutputCount:
					agg.removeOnOutputsCount += b.valueSum;
					break;
				case EffectLogicalKind.Global_OpenNewNode:
				case EffectLogicalKind.Node_LevelUp:
				case EffectLogicalKind.Node_MaxLevelUp:
				case EffectLogicalKind.Unknown:
				default:
					if (!string.IsNullOrEmpty(b.displayName)) agg.textEffects.Add(b.displayName);
					break;
			}
		}
		return agg;
	}

	private static void Add(Dictionary<int, int> dict, int key, int delta)
	{
		if (dict.ContainsKey(key)) dict[key] += delta;
		else dict[key] = delta;
	}

	// ----- 生産完了時フック（NodeView側イベントから呼ばれる） -----

	/// <summary>
	/// NodeViewが1サイクルの生産を完了したときに呼ぶ（追加出力・除去処理）。
	/// </summary>
	public void OnProductionCompleted()
	{
		if (node == null) return;

		// A) 追加出力（該当する出力ポートの在庫を増やす）
		if (_extraOutputsPerCycle.Count > 0)
		{
			foreach (var kv in _extraOutputsPerCycle)
			{
				int resId = kv.Key;
				int addAmount = kv.Value;
				var port = node.outputPorts.FirstOrDefault(op => (int)op.resourceType == resId);
				if (port != null)
				{
					// MaxStock を超えない範囲で在庫加算
					int newQuantity = Mathf.Min(port.Quantity + addAmount, port.MaxStock);
					port.SetQuantity(newQuantity);
				}
			}
		}

		// B) 出力回数による除去
		if (_outputsToRemove > 0)
		{
			_outputsToRemove--;

			if (_outputsToRemove <= 0)
			{
				// ノードを削除
				GraphUIManager.Instance.RemoveNode(node);
			}
			else
			{
				// まだ残るなら、残回数バッジを即時更新
				node.UpdateRemoveByOutputCountBadge(_outputsToRemove);
			}
		}
	}

	// ===== 内部ユーティリティ =====

	private static int GetFromDict(Dictionary<int, int> dict, int key)
		=> dict.TryGetValue(key, out var v) ? v : 0;

	private static int ApplyPercentCeil(int baseValue, int percent /* ±合算済み */)
	{
		float f = baseValue * (100f + percent) / 100f;
		return Mathf.CeilToInt(f);
	}

	/// <summary>
	/// セーブ用に、現在のローカル効果を EffectRuntime の形でスナップショット。
	/// ※localBuckets（合算）を1エントリ=1エフェクトとして出力します
	/// </summary>
	public IEnumerable<int> GetLocalEffectIdsForSave()
	{
		foreach (var d in nodeEffectsRaw)
		{
			if (d != null)
			{
				yield return d.Id;
			}
		}
	}

	// --- 状態を保存するスナップショットAPI ---
	public IEnumerable<SavedNodeEffectState> GetStatefulEffectStatesForSave()
	{
		// Node_RemoveByOutputCount のみ保存（将来拡張時に増やす）
		if (_outputsToRemove > 0)
		{
			yield return new SavedNodeEffectState
			{
				typeId = (int)EffectLogicalKind.Node_RemoveByOutputCount,
				remainingCount = _outputsToRemove,
				durationLeftSec = 0f // 未使用
			};
		}
		foreach (var kv in localEffectRemainingSec)
		{
			yield return new SavedNodeEffectState
			{
				typeId = kv.Key,
				remainingCount = 0, // 未使用
				durationLeftSec = Mathf.Max(0f, kv.Value)
			};
		}
	}

	// --- 状態をロード後に適用（EffectData 適用の“上書き”） ---
	public void ApplyStatefulEffectStates(IEnumerable<SavedNodeEffectState> states)
	{
		if (states == null) return;
		foreach (var s in states)
		{
			if (s == null) continue;
			var kind = (EffectLogicalKind)s.typeId;
			switch (kind)
			{
				case EffectLogicalKind.Node_RemoveByOutputCount:
					_outputsToRemove = Mathf.Max(0, s.remainingCount);
					// バッジ即時更新（0ならバッジ消去）
					if (_outputsToRemove > 0)
						node.UpdateRemoveByOutputCountBadge(_outputsToRemove);
					else
						node.RemoveBadgeForType((int)EffectLogicalKind.Node_RemoveByOutputCount);
					break;

				default:
					// Duration を持つタイプの残時間を復元（effectIdの推定が必要）
					// セーブ簡略化のため typeId 単位で上書き（typeIdに紐づく全 effectId を等分でも可）
					// 精密にやるなら SavedNodeEffectState に effectId を追加してください。
					if (s.durationLeftSec > 0f)
					{
						// 簡易：この typeId を持つ EffectData（ローカル）全部に同値セット
						foreach (var d in nodeEffectsRaw.Where(d => d.Type == s.typeId))
						{
							localEffectRemainingSec[d.Type] = s.durationLeftSec;
						}
					}
					break;
			}
		}
		RefreshAllBadgeTimes();
	}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
	// NodeEffectController.cs（デバッグ用の軽量公開API）

	// 現在このノードに効く「マージ済み」タイプバケットを非破壊で返す（ApplyAllと同じ合成だが副作用なし）
	public IEnumerable<EffectTypeBucket> BuildCurrentBucketsForDebug()
	{
		var list = new List<EffectTypeBucket>();
		// Global
		foreach (var gb in GlobalEffectController.Instance.EnumerateBuckets())
			if (gb.IsTargetNode(NodeId)) list.Add(gb);
		// Local
		foreach (var lb in localBuckets.Values) list.Add(lb);
		return list;
	}

	// Local に指定 typeId が存在するか
	public bool HasLocalTypeId(int typeId) => localBuckets.ContainsKey(typeId);

	// 残寿命（Debug用公開ラッパ）：既存の GetRemainingSecondsForType を参照
	public float GetRemainingSecondsForType_ForDebug(int typeId) => GetRemainingSecondsForType(typeId);
#endif

}