using Data.Master;
using UnityEngine;

public class MasterData : MonoBehaviour
{
	/// <summary>
	/// 素材データ
	/// </summary>
	public ResourceDatas ResourceDatas { get; private set; }
	[SerializeField]
	private TextAsset ResourceDataJson;
	/// <summary>
	/// ノードデータ
	/// </summary>
	public NodeDatas NodeDatas { get; private set; }
	[SerializeField]
	private TextAsset NodeDataJson;
	public NodeCostDatas NodeCostDatas { get; private set; }
	[SerializeField]
	private TextAsset NodeCostDataJson;
	public NodeIoDatas NodeIoDatas { get; private set; }
	[SerializeField]
	private TextAsset NodeIoDataJson;
	/// <summary>
	/// エフェクトデータ
	/// </summary>
	public EffectDatas EffectDatas { get; private set; }
	[SerializeField]
	private TextAsset EffectDataJson;
	/// <summary>
	/// エフェクトデータ
	/// </summary>
	public EffectTypeDatas EffectTypeDatas { get; private set; }
	[SerializeField]
	private TextAsset EffectTypeDataJson;
	/// <summary>
	/// 依頼データ
	/// </summary>
	public RequestDatas RequestDatas { get; private set; }
	[SerializeField]
	private TextAsset RequestDataJson;
	public RequestTypeDatas RequestTypeDatas { get; private set; }
	[SerializeField]
	private TextAsset RequestTypeDataJson;
	public RequestClientDatas RequestClientDatas { get; private set; }
	[SerializeField]
	private TextAsset RequestClientDataJson;
	/// <summary>
	/// 章データ
	/// </summary>
	public ChapterDatas ChapterDatas { get; private set; }
	[SerializeField]
	private TextAsset ChapterDataJson;

	public static MasterData Instance { get; private set; }


	private void Awake()
	{
		// シングルトン設定
		if (Instance != null && Instance != this)
		{
			Debug.LogError("２つ目のマスターデータが作成されました");
		}
		Instance = this;

		if (ResourceDataJson != null)
		{
			ResourceDatas = JsonUtility.FromJson<ResourceDatas>(ResourceDataJson.text);
		}

		if (NodeDataJson != null)
		{
			NodeDatas = JsonUtility.FromJson<NodeDatas>(NodeDataJson.text);
		}

		if (NodeCostDataJson != null)
		{
			NodeCostDatas = JsonUtility.FromJson<NodeCostDatas>(NodeCostDataJson.text);
		}

		if (NodeIoDataJson != null)
		{
			NodeIoDatas = JsonUtility.FromJson<NodeIoDatas>(NodeIoDataJson.text);
		}

		if (EffectDataJson != null)
		{
			EffectDatas = JsonUtility.FromJson<EffectDatas>(EffectDataJson.text);
		}

		if (EffectTypeDataJson != null)
		{
			EffectTypeDatas = JsonUtility.FromJson<EffectTypeDatas>(EffectTypeDataJson.text);
		}

		if (RequestDataJson != null)
		{
			RequestDatas = JsonUtility.FromJson<RequestDatas>(RequestDataJson.text);
		}

		if (RequestClientDataJson != null)
		{
			RequestClientDatas = JsonUtility.FromJson<RequestClientDatas>(RequestClientDataJson.text);
		}

		if (RequestTypeDataJson != null)
		{
			RequestTypeDatas = JsonUtility.FromJson<RequestTypeDatas>(RequestTypeDataJson.text);
		}

		if (ChapterDataJson != null)
		{
			ChapterDatas = JsonUtility.FromJson<ChapterDatas>(ChapterDataJson.text);
		}
	}
}
