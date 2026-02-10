
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Splines;
using UnityEngine.Rendering;

public class SplineKnotsToVFXBuffer : MonoBehaviour
{
	[Header("References")]
	public SplineContainer splineContainer;   // 対象スプライン
	public VisualEffect vfx;                  // VFX Graph

	[Header("Sampling")]
	[Min(0)] public int samplesPerSegment = 8; // ノット間の中間サンプル数（0なら元のノットのみ）

	private GraphicsBuffer buffer;
	private int lastSampleCount = -1;

	void Start()
	{
		if (splineContainer == null || vfx == null)
		{
			Debug.LogWarning("SplineContainer または VFX が設定されていません！");
			return;
		}

		UpdateBufferAndVFX();
	}

	// 動的にスプラインや密度が変わる可能性があるなら、必要に応じて Update で呼ぶ
	// void Update() => UpdateBufferAndVFX();

	void UpdateBufferAndVFX()
	{
		var spline = splineContainer.Spline;
		int knotCount = spline.Count;
		if (knotCount == 0) return;

		// 区間数（開スプラインは N-1, 閉スプラインは N）
		int segmentCount = spline.Closed ? knotCount : Mathf.Max(0, knotCount - 1);

		// 1 区間あたり (samplesPerSegment + 1) 点、全体は最後の重複終点を除いて合計：
		//   segmentCount * samplesPerSegment + 1
		int totalSamples = (segmentCount > 0)
			? segmentCount * samplesPerSegment + 1
			: knotCount; // 念のため

		// バッファの（再）確保
		if (buffer == null || lastSampleCount != totalSamples)
		{
			buffer?.Release();
			buffer = new GraphicsBuffer(
				GraphicsBuffer.Target.Structured,
				totalSamples,
				sizeof(float) * 3 // Vector3
			);
			lastSampleCount = totalSamples;
		}

		// リサンプリング：t を [0,1] で走査
		// UnityEngine.Splines の正規化 t でスプライン上の位置を取得
		Vector3[] points = new Vector3[totalSamples];
		for (int i = 0; i < totalSamples; i++)
		{
			float t = (totalSamples <= 1) ? 0f : (float)i / (totalSamples - 1);
			// EvaluatePosition はローカル座標を返すため、ワールドへ変換
			Vector3 localPos = SplineUtility.EvaluatePosition(spline, t);
			points[i] = splineContainer.transform.TransformPoint(localPos);
		}

		// バッファへ設定
		buffer.SetData(points);

		// VFX へ渡す（プロパティ名は VFX Graph 側に合わせてください）
		vfx.SetGraphicsBuffer("SplinePoints", buffer);
		vfx.SetInt("PointCount", totalSamples);
	}

	void OnDestroy()
	{
		buffer?.Release();
	}
}
