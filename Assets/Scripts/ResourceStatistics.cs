using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 素材ごとの統計データ
/// </summary>
[Serializable]
public class ResourceStatistics
{
	/// <summary>
	/// 素材ID
	/// </summary>
	public int resourceId;
	
	/// <summary>
	/// 現在の排出平均（1秒あたり）
	/// </summary>
	public float averageOutput;
	
	/// <summary>
	/// 排出平均の最大値（これまでの記録、1秒あたり）
	/// </summary>
	public float maxAverageOutput;
	
	/// <summary>
	/// 累計排出量
	/// </summary>
	public long totalOutput;
	
	/// <summary>
	/// 秒ごとの生産量を記録（キー：秒数、値：その秒の生産量）
	/// </summary>
	[NonSerialized]
	private Dictionary<int, int> secondlyOutput = new Dictionary<int, int>();
	
	/// <summary>
	/// 平均計算に使用する秒数（直近N秒）
	/// </summary>
	private const int AVERAGE_WINDOW_SECONDS = 5;
	
	/// <summary>
	/// 計測開始時刻（秒）
	/// </summary>
	public float startTime;

	public ResourceStatistics(int resourceId)
	{
		this.resourceId = resourceId;
		this.averageOutput = 0f;
		this.maxAverageOutput = 0f;
		this.totalOutput = 0;
		this.startTime = Time.time;
		this.secondlyOutput = new Dictionary<int, int>();
	}

	/// <summary>
	/// 新しい排出量を記録して平均を更新（1秒あたりの平均）
	/// </summary>
	public void RecordOutput(int amount)
	{
		if (amount <= 0) return;

		totalOutput += amount;
		
		// 現在の秒数を取得（整数）
		int second = Mathf.FloorToInt(Time.time);
		
		// その秒のバケツに加算
		if (!secondlyOutput.ContainsKey(second))
		{
			secondlyOutput[second] = 0;
		}
		secondlyOutput[second] += amount;
		
		// 古いデータを削除（ウィンドウサイズを超えたもの）
		int cutoffSecond = second - AVERAGE_WINDOW_SECONDS;
		var keysToRemove = new List<int>();
		foreach (var key in secondlyOutput.Keys)
		{
			if (key < cutoffSecond)
			{
				keysToRemove.Add(key);
			}
		}
		foreach (var key in keysToRemove)
		{
			secondlyOutput.Remove(key);
		}
		
		// 平均を計算
		if (secondlyOutput.Count > 0)
		{
			int totalInWindow = 0;
			foreach (var value in secondlyOutput.Values)
			{
				totalInWindow += value;
			}
			
			// 実際に記録がある秒数で割る（0の秒は含めない）
			averageOutput = (float)totalInWindow / secondlyOutput.Count;
			
			// 十分な時間が経過している場合のみ最大値を更新（上振れ防止）
			int elapsedSeconds = second - Mathf.FloorToInt(startTime);
			if (averageOutput > maxAverageOutput)
			{
				maxAverageOutput = averageOutput;
			}
		}
	}

	/// <summary>
	/// 統計をリセット
	/// </summary>
	public void Reset()
	{
		averageOutput = 0f;
		totalOutput = 0;
		startTime = Time.time;
		secondlyOutput.Clear();
		// 最大値はリセットしない（記録として残す）
	}
	
	/// <summary>
	/// デシリアライズ後の初期化
	/// </summary>
	public void OnAfterDeserialize()
	{
		if (secondlyOutput == null)
		{
			secondlyOutput = new Dictionary<int, int>();
		}
	}
}

/// <summary>
/// すべての素材の統計データを管理
/// </summary>
[Serializable]
public class ResourceStatisticsData
{
	public List<ResourceStatistics> statistics = new List<ResourceStatistics>();

	/// <summary>
	/// 指定した素材の統計を取得または作成
	/// </summary>
	public ResourceStatistics GetOrCreateStatistics(int resourceId)
	{
		var stat = statistics.Find(s => s.resourceId == resourceId);
		if (stat == null)
		{
			stat = new ResourceStatistics(resourceId);
			statistics.Add(stat);
		}
		else
		{
			// デシリアライズ後の初期化
			stat.OnAfterDeserialize();
		}
		return stat;
	}

	/// <summary>
	/// 素材の排出を記録
	/// </summary>
	public void RecordOutput(int resourceId, int amount)
	{
		var stat = GetOrCreateStatistics(resourceId);
		stat.RecordOutput(amount);
	}

	/// <summary>
	/// 指定した素材の現在の平均排出量を取得（1秒あたり）
	/// </summary>
	public float GetAverageOutput(int resourceId)
	{
		var stat = statistics.Find(s => s.resourceId == resourceId);
		return stat?.averageOutput ?? 0f;
	}

	/// <summary>
	/// 指定した素材の最大平均排出量を取得（1秒あたり）
	/// </summary>
	public float GetMaxAverageOutput(int resourceId)
	{
		var stat = statistics.Find(s => s.resourceId == resourceId);
		return stat?.maxAverageOutput ?? 0f;
	}

	/// <summary>
	/// すべての素材の統計をリセット（最大値は保持）
	/// </summary>
	public void ResetAll()
	{
		foreach (var stat in statistics)
		{
			stat.Reset();
		}
	}
}