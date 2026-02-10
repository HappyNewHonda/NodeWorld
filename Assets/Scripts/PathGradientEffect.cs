using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

/// <summary>
/// UILineRenderer のメッシュに対して「曲線パス長に沿った」グラデーションを適用する BaseMeshEffect
/// 同じ GameObject に UILineRenderer が付いている前提
/// </summary>
[RequireComponent(typeof(Graphic))]
public class PathGradientEffect : BaseMeshEffect
{
	UnityEngine.Gradient gradient = new UnityEngine.Gradient();

	UILineRenderer line;         // UI Extensions のライン（※パッケージに応じて名前空間変更）
	List<Vector2> pts = new List<Vector2>();  // サンプル済み頂点（UIローカル）
	List<float> cumLen = new List<float>();
	float totalLen;

	protected override void Awake()
	{
		base.Awake();
		line = GetComponent<UILineRenderer>();
		if (line == null)
		{
			Debug.LogWarning("PathGradientEffect: UILineRenderer が見つかりません。");
		}
	}

	// 色をセットする
	public void SetGradient(UnityEngine.Gradient g)
	{
		gradient = g;

		// 頂点再計算要求
		var graphic = GetComponent<Graphic>();
		if (graphic) graphic.SetVerticesDirty();
	}

	// UILineRenderer の点列から累積距離テーブルを作成
	void BuildTable()
	{
		pts.Clear();
		cumLen.Clear();
		totalLen = 0f;

		if (line == null || line.Points == null || line.Points.Length < 2) return;

		// UIローカル座標で取得
		pts.AddRange(line.Points);
		cumLen.Add(0f);
		for (int i = 1; i < pts.Count; i++)
		{
			totalLen += Vector2.Distance(pts[i - 1], pts[i]);
			cumLen.Add(totalLen);
		}
	}

	// 頂点を「最寄りの線分」に投影して、累積距離の t を返す
	float PathT(Vector2 vLocal)
	{
		if (pts.Count < 2 || totalLen <= 1e-4f) return 0f;
		float bestT = 0f, bestDist = float.PositiveInfinity;

		for (int i = 1; i < pts.Count; i++)
		{
			Vector2 a = pts[i - 1], b = pts[i];
			Vector2 ab = b - a;
			float ab2 = Vector2.Dot(ab, ab);
			if (ab2 < 1e-6f) continue;

			// 線分 a-b への最近点の係数 u（0..1）
			float u = Mathf.Clamp01(Vector2.Dot(vLocal - a, ab) / ab2);
			Vector2 p = a + u * ab;
			float d = (vLocal - p).sqrMagnitude;
			if (d < bestDist)
			{
				bestDist = d;
				// a までの累積距離 + 線分内距離
				float segLen = Vector2.Distance(a, b);
				float len = cumLen[i - 1] + segLen * u;
				bestT = Mathf.Clamp01(len / totalLen);
			}
		}
		return bestT;
	}

	// 頂点カラーを書き換え
	public override void ModifyMesh(VertexHelper vh)
	{
		if (!IsActive()) return;
		BuildTable();

		var verts = new List<UIVertex>();
		vh.GetUIVertexStream(verts);

		// Graphic（UI/Default系）の材質は頂点カラーを乗算してくれる必要があります
		for (int i = 0; i < verts.Count; i++)
		{
			var v = verts[i];
			// 頂点はローカル座標。Graphic.rectTransform を原点にしている
			Vector2 vLocal = v.position;
			float t = PathT(vLocal);
			Color c = gradient.Evaluate(t);
			v.color = c;
			verts[i] = v;
		}

		vh.Clear();
		vh.AddUIVertexTriangleStream(verts);
	}
}
