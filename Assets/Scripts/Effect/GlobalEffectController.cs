using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Define;
using Data.Master;
using Unity.VisualScripting;

namespace Effects
{
	/// <summary>
	/// グローバル効果の管理（称号含む）。シーン常駐。
	/// </summary>
	public class GlobalEffectController : MonoBehaviour
	{
		public static GlobalEffectController Instance { get; private set; }

		private Dictionary<int, List<NodeEffectController>> nodeEffects = new ();

		private readonly Dictionary<int, EffectTypeBucket> globalBuckets = new();

		// 寿命（秒）を個別管理
		private readonly Dictionary<int/*typeId*/, float> globalEffectRemainingSec = new();
		[SerializeField] float tickIntervalSec = 1.0f;
		float acc = 0f;

		//生のグローバル効果（称号表示のデータ源）
		private readonly List<EffectData> globalEffectsRaw = new();

		public IEnumerable<EffectData> EnumerateAllGlobalEffectsRaw() => globalEffectsRaw;

		// グローバル効果が差し替わったら発火（称号パネル更新用）
		public event Action OnGlobalEffectsChanged;
		public event Action<int, float> OnGlobalTimesTicked;

		GlobalEffectController()
		{
			Instance = this;
		}

		void Update()
		{
			if (tickIntervalSec <= 0f) return;

			acc += Time.deltaTime;
			if (acc < tickIntervalSec) return;

			acc -= tickIntervalSec;
			TickGlobalEffects(tickIntervalSec);
		}

		void TickGlobalEffects(float dt)
		{
			if (globalEffectRemainingSec.Count == 0) return;

			// 減算＆失効抽出
			var expiredIds = new List<int>();
			foreach (var kv in globalEffectRemainingSec.ToArray())
			{
				float sec = kv.Value - dt;
				if (sec <= 0f)
				{
					expiredIds.Add(kv.Key);
					globalEffectRemainingSec.Remove(kv.Key);
				}
				else
				{
					globalEffectRemainingSec[kv.Key] = sec;
				}
				OnGlobalTimesTicked?.Invoke(kv.Key, sec);
			}
			if (expiredIds.Count == 0)
			{
				return;
			}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
			EffectLogger.Log(EffectLogger.Category.Effects, $"GlobalExpire ids=[{string.Join(",", expiredIds)}]");
#endif
			// 失効対象を globalEffectsRaw から削除
			globalEffectsRaw.RemoveAll(d => expiredIds.Contains(d.Type));

			// 影響 typeId を収集し直して、バケット再構築 → 差分 Reapply を流用
			var before = new Dictionary<int, EffectTypeBucket>(globalBuckets);
			globalBuckets.Clear();
			foreach (var e in globalEffectsRaw)
			{
				int typeId = e.Type;
				if (!globalBuckets.TryGetValue(typeId, out var bucket))
				{
					bucket = new EffectTypeBucket(typeId);
					globalBuckets.Add(typeId, bucket);
				}
				bucket.Add(e);
			}
			// 影響ノードにだけ再適用（既存の最適化を再利用）
			ReapplyTypesToRelatedNodes(before, globalBuckets); 
			OnGlobalEffectsChanged?.Invoke();
			
			// 失効したタイプの時間表示を、関係ノードにだけ更新要求
			var affectedTypes = new HashSet<int>();
			foreach (var id in expiredIds)
			{
				if (MasterData.Instance.EffectDatas.SelectId.TryGetValue(id, out var d))
					affectedTypes.Add(d.Type);
			}
			if (affectedTypes.Count > 0)
			{
				foreach (var list in nodeEffects.Values)
				{
					foreach (var nec in list) nec.RefreshBadgeTimesForTypes(affectedTypes);
				}
			}
		}

		public float GetMaxRemainingSecondsForType(int typeId)
		{
			float max = -1f;
			// 生のグローバル効果リストから、そのtypeに属するeffectの残寿命を走査
			foreach (var e in globalEffectsRaw) // public列挙APIもありますが内部に直接アクセス可
			{
				if (e.Type != typeId) continue;
				if (e.Duration <= 0) continue; // 無期限は時間表示なし
				if (globalEffectRemainingSec.TryGetValue(e.Type, out var sec))
				{
					if (sec > max) max = sec;
				}
			}
			return max; // 無ければ -1
		}

		public void AddNodeEffectController(NodeEffectController nodeEffect)
		{
			if (!nodeEffects.ContainsKey(nodeEffect.NodeId))
			{
				nodeEffects.Add(nodeEffect.NodeId, new List<NodeEffectController>());
			}
			nodeEffects[nodeEffect.NodeId].Add(nodeEffect);
		}
		public void RemoveNodeEffectController(NodeEffectController nodeEffect)
		{
			if (nodeEffects.TryGetValue(nodeEffect.NodeId, out var list))
			{
				list.Remove(nodeEffect);
				if (list.Count == 0) nodeEffects.Remove(nodeEffect.NodeId);
			}
		}

		/// <summary>外部（シナリオ等）からグローバル効果を設定/差し替え。</summary>
		public void SetGlobalEffects(IEnumerable<EffectData> effects)
		{
			// 0) 旧状態をコピー（差分判定用）
			var before = new Dictionary<int, EffectTypeBucket>(globalBuckets);

			// 生の効果を保存（称号列挙に使用）
			globalEffectsRaw.Clear();
			globalEffectRemainingSec.Clear();

			if (effects != null)
			{
				foreach (var e in effects.Where(x => x != null))
				{
					globalEffectsRaw.Add(e);
					if (e.Duration > 0)
					{
						globalEffectRemainingSec[e.Type] = e.Duration;
						OnGlobalTimesTicked?.Invoke(e.Type, globalEffectRemainingSec[e.Id]);
					}
				}
			}

			// 1) タイプ単位で合成して保持（新状態）
			globalBuckets.Clear();
			foreach (var e in globalEffectsRaw)
			{
				if (e == null) continue;
				int typeId = e.Type;
				if (!globalBuckets.TryGetValue(typeId, out var bucket))
				{
					bucket = new EffectTypeBucket(typeId);
					globalBuckets.Add(typeId, bucket);
				}
				bucket.Add(e);
			}
			// 2) タイプ別に差分適用（ReapplyType）
			ReapplyTypesToRelatedNodes(before, globalBuckets);

			// 3) 称号パネル更新トリガ
			OnGlobalEffectsChanged?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
			EffectLogger.Log(EffectLogger.Category.Effects, $"SetGlobalEffects count={(effects == null ? 0 : effects.Count())}");
#endif
		}

		public void ApplyGlobalEffectStates(IEnumerable<SavedGlobalEffectState> states)
		{
			if (states == null) return;
			bool any = false;
			foreach (var s in states)
			{
				if (s == null) continue;
				if (globalEffectRemainingSec.ContainsKey(s.typeId))
				{
					globalEffectRemainingSec[s.typeId] = Mathf.Max(0f, s.durationLeftSec);
					OnGlobalTimesTicked?.Invoke(s.typeId, globalEffectRemainingSec[s.typeId]);
					any = true;
				}
			}
			// 表示を即時更新したい場合は、関連タイプの時間だけ再描画要求してもOK
			if (any)
			{
				OnGlobalEffectsChanged?.Invoke(); // Node側Updateでも1秒毎に更新はかかる

				// ロード直後に“全ノードの”時間表示を即時更新（1回だけ・軽微）
				foreach (var list in nodeEffects.Values)
				{
					foreach (var nec in list)
					{
						nec.RefreshAllBadgeTimes();
					}
				}
			}
		}


		/// <summary>
		/// 永続効果（称号）を列挙。isPermanent または ViewType==100 を対象にする。
		/// UI側で displayName を用いて表示する想定。
		/// </summary>
		public IEnumerable<EffectData> EnumeratePermanentEffects(bool includeViewTypeEternal = true)
		{
			foreach (var e in globalEffectsRaw)
			{
				if (!includeViewTypeEternal) continue;

				var t = MasterData.Instance.EffectTypeDatas.SelectTypeId[e.Type];
				//if (t.ViewType == 100)
				{
					yield return e;
				}
			}
		}

		/// <summary>現在のグローバル効果を列挙。</summary>
		public IEnumerable<EffectTypeBucket> EnumerateBuckets() => globalBuckets.Values;

		/// <summary>
		/// あるノードIDに対して、リソース別の補正を集計して返す。
		/// </summary>
		public IEnumerable<EffectTypeBucket> GetBucketsForNode(int nodeId)
		{
			foreach (var b in globalBuckets.Values)
			{
				if (b.IsTargetNode(nodeId))
				{
					yield return b;
				}
			}
		}

		/// <summary>このノードに効いている「typeId」のグローバル効果のうち、残り時間の最大値を返す（なければ -1）</summary>
		public float GetMaxRemainingSecondsForNodeAndType(int nodeId, int typeId)
		{
			float max = -1f;
			foreach (var e in globalEffectsRaw)
			{
				if (e.Type != typeId) continue;
				if (e.Duration <= 0) continue;

				// 対象ノードに効くか
				if (Array.Exists(e.TargetNodes, n => n == nodeId))
				{
					if (globalEffectRemainingSec.TryGetValue(e.Type, out var sec))
					{
						if (sec > max)
						{
							max = sec;
						}
					}
				}
			}
			return max;
		}


		// 追加：タイプ別に ReapplyType を呼ぶ最適化
		private void ReapplyTypesToRelatedNodes(Dictionary<int, EffectTypeBucket> before, Dictionary<int, EffectTypeBucket> after)
		{
			var typeIds = new HashSet<int>();
			if (before != null) foreach (var t in before.Keys) typeIds.Add(t);
			if (after != null) foreach (var t in after.Keys) typeIds.Add(t);
			if (typeIds.Count == 0) return;

			foreach (var typeId in typeIds)
			{
				// C# definite assignment 対策：先に null で初期化してから TryGetValue
				EffectTypeBucket bOld = null;
				EffectTypeBucket bNew = null;
				if (before != null) before.TryGetValue(typeId, out bOld);
				if (after != null) after.TryGetValue(typeId, out bNew);


				bool allOld = bOld?.affectsAllNodes == true;
				bool allNew = bNew?.affectsAllNodes == true;
				bool affectsAll = allOld || allNew;

				if (affectsAll)
				{
					// 全ノードへこの typeId の差分適用
					foreach (var list in nodeEffects.Values)
						foreach (var nec in list) nec.ReapplyType(typeId);
				}
				else
				{
					// 旧/新の対象ノードIDの和集合にだけ適用
					var nodes = new HashSet<int>();
					if (bOld != null) foreach (var n in bOld.targetNodes) nodes.Add(n);
					if (bNew != null) foreach (var n in bNew.targetNodes) nodes.Add(n);

					foreach (var nodeId in nodes)
					{
						if (nodeEffects.TryGetValue(nodeId, out var list))
						{
							foreach (var nec in list)
							{
								nec.ReapplyType(typeId);
							}
						}
					}
				}
			}
		}

		public IEnumerable<(int typeId, float sec)> GetGlobalStatesForSave()
		{
			foreach (var kv in globalEffectRemainingSec)
				yield return (kv.Key, Mathf.Max(0f, kv.Value));
		}
	}
}