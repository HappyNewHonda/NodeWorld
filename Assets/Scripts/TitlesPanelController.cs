// TitlesPanelController.cs（イベント駆動に変更）
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Effects;
using Data.Master;

public class TitlesPanelController : MonoBehaviour
{
	[SerializeField] private Transform container;
	[SerializeField] private EffectBadgeView badgePrefab;

	private readonly Dictionary<int /*typeId*/, EffectBadgeView> _badgesByType = new();

	void OnEnable()
	{
		var g = GlobalEffectController.Instance;
		if (g == null || container == null || badgePrefab == null) return;

		g.OnGlobalEffectsChanged += OnGlobalChanged;     // 付与/失効/差し替え
		g.OnGlobalTimesTicked += RefreshTimesOnce;       // 時間の進行

		ReconcileBadges();

		foreach (var kv in _badgesByType)
		{
			int typeId = kv.Key;
			var view = kv.Value;
			if (view == null) continue;
			float sec = g.GetMaxRemainingSecondsForType(typeId);
			view.SetTimeSeconds(sec);
		}
	}

	void OnDisable()
	{
		var g = GlobalEffectController.Instance;
		if (g == null) return;
		g.OnGlobalEffectsChanged -= OnGlobalChanged;
		g.OnGlobalTimesTicked -= RefreshTimesOnce;
	}

	// 効果の増減・差し替え時（種類が変わる可能性がある）
	private void OnGlobalChanged()
	{
		ReconcileBadges();
	}

	private void ReconcileBadges()
	{
		var g = GlobalEffectController.Instance;
		var typeIdsNow = new HashSet<int>();

		foreach (var b in g.EnumerateBuckets())
		{
			typeIdsNow.Add(b.typeId);
			UpsertBadgeForType(b);
		}

		// 今は存在しない type のバッジを削除
		var toRemove = new List<int>();
		foreach (var typeId in _badgesByType.Keys)
		{
			if (!typeIdsNow.Contains(typeId))
			{
				toRemove.Add(typeId);
			}
		}

		foreach (var typeId in toRemove)
		{
			var view = _badgesByType[typeId];
			if (view != null && view.gameObject != null)
			{
				Destroy(view.gameObject);
			}
			_badgesByType.Remove(typeId);
		}
	}

	private void UpsertBadgeForType(EffectTypeBucket bucket)
	{
		var typeId = bucket.typeId;
		string text = string.IsNullOrEmpty(bucket.displayName)
			? MasterData.Instance.EffectTypeDatas.SelectTypeid[typeId].Description
			: bucket.displayName;

		float sec = GlobalEffectController.Instance.GetMaxRemainingSecondsForType(typeId);

		if (_badgesByType.TryGetValue(typeId, out var view) == false || view == null)
		{
			var v = Instantiate(badgePrefab, container);
			v.Setup(text, color: null, seconds: sec);
			_badgesByType[typeId] = v;
		}
		else
		{
			view.Setup(text, color: null); // テキスト（固定部）はここで
			view.SetTimeSeconds(sec);      // 時間は直後に反映
		}
	}

	/// <summary>
	/// affectedTypeIds==null なら全バッジ、非nullなら該当タイプのみ時間を更新
	/// </summary>
	private void RefreshTimesOnce(int typeId, float sec)
	{
		if (_badgesByType.TryGetValue(typeId, out var view) && view != null)
		{
			view.SetTimeSeconds(sec);
		}
	}
}