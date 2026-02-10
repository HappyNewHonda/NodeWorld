
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 拡縮（Zoom）に追従して格子線を描画するuGUIコンポーネント。
/// GraphRoot配下に置くとパン/ズームに自然追従します。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIGraphGrid : Graphic
{
	[Header("拡縮参照")]
	public RectTransform graphRoot;

	[Header("グリッド間隔")]
	public float spacingLow = 50f;
	public float spacingHigh = 25f;

	[Header("線の太さ")]
	public float minorThicknessPx = 1f;
	public float majorThicknessPx = 2f;

	[Header("色")]
	public Color minorColor = new Color(1f, 1f, 1f, 0.15f);
	public Color majorColor = new Color(1f, 1f, 1f, 0.35f);

	[Header("原点オフセット")]
	public Vector2 gridOriginLocal = Vector2.zero;

	// 内部キャッシュ
	float lastScale = -1f;

	protected override void OnEnable()
	{
		base.OnEnable();
		SetVerticesDirty();
	}

	void Update()
	{
		// 拡縮が変わった時だけ再描画要求
		float s = CurrentScale();
		if (!Mathf.Approximately(s, lastScale))
		{
			lastScale = s;
			SetVerticesDirty();
		}
	}

	float CurrentScale()
	{
		if (graphRoot == null) return 1f;
		// GraphRootの等方スケールを想定
		return graphRoot.localScale.x;
	}

	// 画面見た目の太さを維持するため、ローカル座標系に合わせて太さを補正
	float LocalThickness(float px)
	{
		float s = Mathf.Max(0.0001f, CurrentScale());
		// GraphRoot配下なら拡縮によりローカル距離が伸びるため、太さを 1/scale で薄くすると見た目が一定に近付く
		return px / s;
	}

	// 指定X位置に縦線、指定Y位置に横線を引く（太さに応じて矩形で描く）
	void AddVerticalLine(VertexHelper vh, float x, float thickness, Color color, float yMin, float yMax)
	{
		float half = thickness * 0.5f;
		var p0 = new Vector2(x - half, yMin);
		var p1 = new Vector2(x + half, yMin);
		var p2 = new Vector2(x + half, yMax);
		var p3 = new Vector2(x - half, yMax);
		AddQuad(vh, p0, p1, p2, p3, color);
	}

	void AddHorizontalLine(VertexHelper vh, float y, float thickness, Color color, float xMin, float xMax)
	{
		float half = thickness * 0.5f;
		var p0 = new Vector2(xMin, y - half);
		var p1 = new Vector2(xMax, y - half);
		var p2 = new Vector2(xMax, y + half);
		var p3 = new Vector2(xMin, y + half);
		AddQuad(vh, p0, p1, p2, p3, color);
	}

	void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color color)
	{
		int idx = vh.currentVertCount;
		UIVertex v = UIVertex.simpleVert; v.color = color;

		v.position = p0; vh.AddVert(v);
		v.position = p1; vh.AddVert(v);
		v.position = p2; vh.AddVert(v);
		v.position = p3; vh.AddVert(v);

		vh.AddTriangle(idx, idx + 1, idx + 2);
		vh.AddTriangle(idx, idx + 2, idx + 3);
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		vh.Clear();
		Rect rect = GetPixelAdjustedRect();

		float xMin = rect.xMin;
		float xMax = rect.xMax;
		float yMin = rect.yMin;
		float yMax = rect.yMax;

		// 現在スケールに応じて感覚を選択
		float s = CurrentScale();
		float spacing = (s < 1.2f) ? spacingLow : spacingHigh;

		// 太さ補正（見た目ピクセル相当）
		float minorT = LocalThickness(minorThicknessPx);
		float majorT = LocalThickness(majorThicknessPx);

		// 原点からの最初のラインインデックスを算出し、可視範囲内を描画
		// X方向
		{
			// グリッド原点からのローカル座標
			float firstIndexX = Mathf.Floor((xMin - gridOriginLocal.x) / spacing);
			float lastIndexX = Mathf.Ceil((xMax - gridOriginLocal.x) / spacing);

			for (int ix = (int)firstIndexX; ix <= (int)lastIndexX; ix++)
			{
				float x = gridOriginLocal.x + ix * spacing;
				bool isMajor = (ix % 5 == 0);
				float t = isMajor ? majorT : minorT;
				Color c = isMajor ? majorColor : minorColor;

				AddVerticalLine(vh, x, t, c, yMin, yMax);
			}
		}

		// Y方向
		{
			float firstIndexY = Mathf.Floor((yMin - gridOriginLocal.y) / spacing);
			float lastIndexY = Mathf.Ceil((yMax - gridOriginLocal.y) / spacing);

			for (int iy = (int)firstIndexY; iy <= (int)lastIndexY; iy++)
			{
				float y = gridOriginLocal.y + iy * spacing;
				bool isMajor = (iy % 5 == 0);
				float t = isMajor ? majorT : minorT;
				Color c = isMajor ? majorColor : minorColor;

				AddHorizontalLine(vh, y, t, c, xMin, xMax);
			}
		}
	}
}
