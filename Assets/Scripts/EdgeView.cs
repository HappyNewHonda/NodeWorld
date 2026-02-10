using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using Coffee.UIEffects;

public class EdgeView : MonoBehaviour
{
	public UILineRenderer uiLine;
	public PathGradientEffect pathGradient;

	[Range(8, 128)] public int bezierSegments = 32;
	[Range(0f, 1f)] public float tangentFactor = 0.35f;

	[Header("Hit / Select")]
	public float pickRadiusPx = 10f;
	public float selectedThicknessScale = 1.6f;
	public float selectedColorBoost = 0.15f;

	[Header("UIEffect (Hover)")]
	public UIEffectTweener hoverTweener;

	[Header("UIEffect (Select)")]
	public UIEffectTweener selectTweener;

	GraphUIManager manager;
	public PortView fromPort { get; private set; }
	public PortView toPort { get; private set; }
	Sprite tokenSprite;
	Color tokenColor;
	public bool isTransferring { get; private set; } = false;


	readonly List<Vector2> points = new();
	readonly List<float> cumLen = new();
	float totalLength;
	public float Length { get { return totalLength; } }

	public RectTransform tokenParent;
	public ResourceTokenView tokenPrefab;

	bool isSelected;


	SelectionController selection;

	public void Initialize(GraphUIManager m, bool isPreview)
	{
		manager = m;
		var rt = GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0, 0);

		if (!isPreview)
		{
			selection = SelectionController.Instance;
		}

		// エッジは常に非選択・非ホバー状態で開始
		if (hoverTweener != null)
		{
			hoverTweener.PlayReverse();
		}
		if (selectTweener != null)
		{
			selectTweener.PlayReverse();
		}

		isSelected = false;
	}


	/// <summary>
	/// エッジの表示優先度を変更（ノードより手前に表示する場合は100以上）
	/// </summary>
	public void BindPorts(PortView from, PortView to)
	{
		fromPort = from;
		toPort = to;

		fromPort.BindEdge(this);
		if (fromPort.IsResourceBuffer)
		{
			if (fromPort.resourceType != to.resourceType && to.resourceType != Define.ResourceId.全て)
			{
				fromPort.SetType(to.resourceType);
			}
		}

		toPort.BindEdge(this);
		if (toPort.IsResourceBuffer)
		{
			if (toPort.resourceType != from.resourceType && from.resourceType != Define.ResourceId.全て)
			{
				toPort.SetType(from.resourceType);
			}
		}

		var uiManager = GraphUIManager.Instance;
		SetColor(uiManager.GetPortColor(fromPort), uiManager.GetPortColor(toPort));
		UpdateFromPorts();

		tokenSprite = fromPort.GetComponent<Image>().sprite;
		tokenColor = uiManager.GetPortColor(fromPort);
	}

	public void UnbindPorts()
	{
		if (fromPort != null && fromPort.edges.Contains(this))
		{
			fromPort.edges.Remove(this);
			fromPort = null;
		}
		if (toPort != null && toPort.edges.Contains(this))
		{
			toPort.edges.Remove(this);
			if (toPort.IsResourceBuffer)
			{
				toPort.SetType(Define.ResourceId.全て);
			}
			toPort = null;
		}
	}

	public void UpdateFromPorts()
	{
		if (fromPort == null || toPort == null) return;

		RebuildCurve(fromPort.PortWorldCenter() + Vector3.right * 1, toPort.PortWorldCenter() + Vector3.left * 1);
	}

	public void SetColor(Color start, Color end)
	{
		var g = new UnityEngine.Gradient();
		g.SetKeys(
			new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
			new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
		);
		pathGradient.SetGradient(g);

		var hoverLine = hoverTweener.GetComponent<PathGradientEffect>();
		hoverLine.SetGradient(g);
	}

	public void SetSelected(bool on)
	{
		if (isSelected == on) return;
		isSelected = on;

		if (on)
		{
			selectTweener?.PlayForward();
		}
		else
		{
			selectTweener?.PlayReverse();
		}
	}

	public void OnPointerEnter()
	{
		hoverTweener?.PlayForward();
	}

	public void OnPointerExit()
	{
		hoverTweener?.PlayReverse();
	}

	/// <summary>
	/// 右クリック処理（GraphUIManagerから呼ばれる）
	/// </summary>
	public void OnRightClick(Vector2 screenPosition)
	{
		ShowContextMenu(screenPosition);
	}

	/// <summary>
	/// 右クリックメニューを表示
	/// </summary>
	/// <summary>
	/// 右クリックメニューを表示
	/// </summary>
	/// <summary>
	/// 右クリックメニューを表示
	/// </summary>
	private void ShowContextMenu(Vector2 screenPosition)
	{
		if (ContextMenuController.Instance == null) return;

		// このエッジが選択されていない場合、このエッジのみを選択
		if (selection != null && !selection.IsSelected(this))
		{
			selection.SelectOnly(this);
		}

		var items = new List<ContextMenuItem>
		{
			new ContextMenuItem("削除 (Delete)", () => selection?.DeleteSelection())
		};

		ContextMenuController.Instance.ShowMenu(screenPosition, items);
	}


	public void ForceHoverOff()
	{
		hoverTweener?.PlayReverse();
	}


	public bool HitTestScreen(Vector2 screenPos, Camera eventCamera)
	{
		if (uiLine == null || uiLine.Points == null || uiLine.Points.Length < 2) return false;

		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			(RectTransform)transform, screenPos, eventCamera, out var local);

		var pts = uiLine.Points;

		var startPoint = pts[0];
		var endPoint = pts[pts.Length - 1];
		Rect rect = Rect.MinMaxRect(
			Mathf.Min(startPoint.x, endPoint.x),
			Mathf.Min(startPoint.y, endPoint.y),
			Mathf.Max(startPoint.x, endPoint.x),
			Mathf.Max(startPoint.y, endPoint.y)
		);
		if (rect.Contains(local) == false)
		{
			return false;
		}

		float minSqr = float.PositiveInfinity;
		for (int i = 1; i < pts.Length; i++)
		{
			float d2 = SqrDistancePointToSegment(local, pts[i - 1], pts[i]);
			if (d2 < minSqr) minSqr = d2;
		}

		float scale = 1f;
		var parent = transform.parent as RectTransform;
		if (parent != null)
		{
			scale = parent.lossyScale.x;
			if (Mathf.Approximately(scale, 0f)) scale = 1f;
		}

		float radiusLocal = pickRadiusPx / scale;
		return minSqr <= radiusLocal * radiusLocal;
	}

	float SqrDistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		var ab = b - a;
		float t = Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
		t = Mathf.Clamp01(t);
		var q = a + t * ab;
		return (p - q).sqrMagnitude;
	}

	public void RebuildCurve(Vector3 start, Vector3 end)
	{
		var parent = (RectTransform)transform;
		var p0 = manager.WorldToLocalOn(parent, start);
		var p3 = manager.WorldToLocalOn(parent, end);

		Vector2 dir = (p3 - p0);
		float dist = dir.magnitude;
		float tOff = Mathf.Clamp01(tangentFactor) * dist;

		var t0 = new Vector2(Mathf.Sign(dir.x) * tOff, 0f);
		var t1 = new Vector2(-Mathf.Sign(dir.x) * tOff, 0f);
		var p1 = p0 + t0;
		var p2 = p3 + t1;

		SampleCubic(points, p0, p1, p2, p3, Mathf.Max(8, bezierSegments));
		BuildCumulativeLengths();

		uiLine.Points = points.ToArray();
		uiLine.SetAllDirty();
		uiLine.Rebuild(CanvasUpdate.Prelayout);

		var hoverLine = hoverTweener.GetComponent<UILineRenderer>();
		hoverLine.Points = uiLine.Points;
		hoverLine.SetAllDirty();
		hoverLine.Rebuild(CanvasUpdate.Prelayout);

		var selectLine = selectTweener.GetComponent<UILineRenderer>();
		selectLine.Points = uiLine.Points;
		selectLine.SetAllDirty();
		selectLine.Rebuild(CanvasUpdate.Prelayout);
	}

	static Vector2 Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
	{
		float u = 1f - t;
		return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
	}

	static void SampleCubic(List<Vector2> dst, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int segments)
	{
		dst.Clear();
		for (int i = 0; i <= segments; i++)
		{
			float t = i / (float)segments;
			dst.Add(Cubic(p0, p1, p2, p3, t));
		}
	}

	void BuildCumulativeLengths()
	{
		cumLen.Clear();
		totalLength = 0f;
		cumLen.Add(0f);
		for (int i = 1; i < points.Count; i++)
		{
			totalLength += Vector2.Distance(points[i - 1], points[i]);
			cumLen.Add(totalLength);
		}
	}

	public Vector2 GetPointAtDistance(float d)
	{
		if (points.Count == 0) return Vector2.zero;
		if (d <= 0) return points[0];
		if (d >= totalLength) return points[^1];

		for (int i = 1; i < cumLen.Count; i++)
		{
			if (cumLen[i] >= d)
			{
				float segLen = cumLen[i] - cumLen[i - 1];
				float t = (d - cumLen[i - 1]) / Mathf.Max(1e-4f, segLen);
				return Vector2.Lerp(points[i - 1], points[i], t);
			}
		}
		return points[^1];
	}

	public ResourceTokenView SpawnToken(float durationSeconds, int amount)
	{
		if (isTransferring) return null;

		if (tokenPrefab == null) return null;
		if (tokenParent == null) return null;

		isTransferring = true;
		var token = Instantiate(tokenPrefab, tokenParent);
		token.Initialize(this, tokenSprite, tokenColor, durationSeconds, toPort, amount);
		token.transform.SetAsLastSibling();
		return token;
	}

	public void OnTokenArrived()
	{
		isTransferring = false;
	}
}
