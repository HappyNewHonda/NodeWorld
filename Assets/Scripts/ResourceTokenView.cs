using UnityEngine;
using UnityEngine.UI;

public class ResourceTokenView : MonoBehaviour
{
	public Image icon;
	public float normalOffset = 0f;

	RectTransform rt;
	EdgeView edge;
	PortView destinationPort;
	int transferAmount;

	// セーブ時にアクセス用
	public EdgeView sourceEdge => edge;
	public int amount => transferAmount;

	private float duration = 1.0f;
	private float elapsed;
	private float progress;

	public void Initialize(EdgeView e, Sprite sprite, Color color, float durationSeconds, PortView destination, int amount)
	{
		edge = e;
		destinationPort = destination;
		transferAmount = amount;
		duration = Mathf.Max(0.01f, durationSeconds);
		elapsed = 0f;
		progress = 0f;

		rt = GetComponent<RectTransform>();
		if (icon)
		{
			icon.sprite = sprite;
			icon.color = color;
			icon.raycastTarget = false;
		}

		UpdatePosition(TrimmedDistance(0f));
	}

	void LateUpdate()
	{
		if (edge == null) return;

		elapsed += Time.deltaTime;
		progress = Mathf.Clamp01(elapsed / duration);

		float t = progress;
		float d = TrimmedDistance(t);

		if (progress >= 1f)
		{
			UpdatePosition(TrimmedDistance(1f));
			
			if (destinationPort != null)
			{
				destinationPort.SetQuantity(destinationPort.Quantity + transferAmount);
			}
			
			if (edge != null)
			{
				edge.OnTokenArrived();
			}
			
			Destroy(gameObject);
			return;
		}

		UpdatePosition(d);
	}

	float TrimmedDistance(float t01)
	{
		float L = Mathf.Max(0f, edge.Length);
		float usable = Mathf.Max(0f, L);
		float d = usable * Mathf.Clamp01(t01);
		return Mathf.Clamp(d, 0f, L);
	}

	void UpdatePosition(float distance)
	{
		var p = edge.GetPointAtDistance(distance);

		if (normalOffset != 0f)
		{
			float eps = Mathf.Min(4f, edge.Length * 0.01f);
			var p1 = edge.GetPointAtDistance(Mathf.Clamp(distance + eps, 0, edge.Length));
			var dir = (p1 - p).normalized;
			var n = new Vector2(-dir.y, dir.x);
			p += n * normalOffset;
		}

		rt.anchoredPosition = p;
	}

	public void OnEdgeResampled()
	{
		UpdatePosition(TrimmedDistance(progress));
	}
}
