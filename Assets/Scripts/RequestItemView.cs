using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Master;
using Define;
using Unity.VisualScripting;

/// <summary>
/// 依頼アイテムの表示コンポーネント
/// </summary>
public class RequestItemView : MonoBehaviour
{
	[Header("UI References")]
	[SerializeField] private TextMeshProUGUI titleText;
	[SerializeField] private TextMeshProUGUI messageText;
	[SerializeField] private TextMeshProUGUI descriptionText;
	[SerializeField] private TextMeshProUGUI clientNameText;
	[SerializeField] private TextMeshProUGUI succesedEffectText;
	[SerializeField] private TextMeshProUGUI failedEffectText;
	[SerializeField] private TextMeshProUGUI progressText;
	[SerializeField] private Image clientFaceImage;
	[SerializeField] private Button acceptButton;
	[SerializeField] private Button rejectButton;
	[SerializeField] private Button selectButton;
	[SerializeField] private GameObject completedBadge;

	private RequestData requestData;
	private RequestProgress requestProgress;
	private RequestTypeData requestTypeData;
	private RequestClientData requestClientData;

	public RequestData RequestData => requestData;
	public RequestProgress RequestProgress => requestProgress;

	private Action<RequestItemView> onAcceptCallback;
	private Action<RequestItemView> onRejectCallback;

	/// <summary>
	/// 依頼データを設定して表示を更新
	/// </summary>
	public void SetupRequest(RequestData data, RequestProgress progress)
	{
		var master = MasterData.Instance;

		requestData = data;
		requestProgress = progress;
		requestTypeData = master.RequestTypeDatas.SelectId[data.Type];
		requestClientData = master.RequestClientDatas.SelectId[data.FaceID];

		UpdateDisplay();
	}

	/// <summary>
	/// 表示を更新
	/// </summary>
	public void UpdateDisplay()
	{
		if (requestData == null) return;
		var master = MasterData.Instance;

		// タイトルと説明
		titleText.text = requestData.DisplayTitle;
		messageText.text = requestData.Description;

		// 依頼主の名前
		clientNameText.text = requestClientData.DisplayName;

		// 依頼内容の説明
		string target = "";
		foreach (var node in requestData.TargetNodes)
		{
			if (target != "") target += ", ";
			target += master.NodeDatas.SelectId[node].DisplayName;
		}
		descriptionText.text = string.Format(requestTypeData.Description, target, requestData.Num, requestData.Level);

		// 進捗表示
		progressText.text = $"{requestProgress.currentProgress} / {requestProgress.targetProgress}";

		// 状態に応じた表示切り替え
		UpdateStateDisplay();

		// 成功・失敗効果の表示
		UpdateEffectDisplay();
	}

	/// <summary>
	/// 状態に応じた表示を更新
	/// </summary>
	private void UpdateStateDisplay()
	{
		// バッジの表示切り替え
		completedBadge.SetActive(requestProgress.state == RequestState.Completed);

		progressText.transform.parent.gameObject.SetActive(false);
		acceptButton.gameObject.SetActive(false);
		rejectButton.gameObject.SetActive(false);
		selectButton.gameObject.SetActive(false);
		completedBadge.SetActive(false);
		switch (requestProgress.state)
		{
			case RequestState.NotReceived:
				selectButton.gameObject.SetActive(true);
				rejectButton.gameObject.SetActive(true);
				break;
			case RequestState.InProgress:
				progressText.transform.parent.gameObject.SetActive(true);
				break;
			case RequestState.Completed:
				completedBadge.SetActive(true);
				acceptButton.gameObject.SetActive(true);
				break;
			case RequestState.Accepted:
				break;
		}

		// ボタンの有効/無効切り替え
		bool canAccept = requestProgress.state == RequestState.Completed;
		acceptButton.interactable = canAccept;
		rejectButton.interactable = canAccept;
	}

	/// <summary>
	/// エフェクトの表示を更新
	/// </summary>
	private void UpdateEffectDisplay()
	{
		var master = MasterData.Instance;

		if (requestData.SuccesedEffect > EffectId.なし)
		{
			var effect = master.EffectDatas.SelectId[requestData.SuccesedEffect];
			var effectType = master.EffectTypeDatas.SelectTypeid[effect.Type];

			var target = "";
			foreach (var node in effect.TargetNodes)
			{
				if (target != "") target += ", ";
				target += master.NodeDatas.SelectId[node].DisplayName;
			}
			string resource = "";
			foreach (var res in effect.TargetResources)
			{
				if (resource != "") resource += ", ";
				resource += master.ResourceDatas.SelectId[res].DisplayName;
			}
			succesedEffectText.gameObject.transform.parent.gameObject.SetActive(true);
			succesedEffectText.text = string.Format(effectType.Description, target, effect.Value, resource, effect.Duration);
		}

		else
		{
			succesedEffectText.gameObject.transform.parent.gameObject.SetActive(false);
		}

		if (requestData.FailedEffect > EffectId.なし)
		{
			var effect = master.EffectDatas.SelectId[requestData.FailedEffect];
			var effectType = master.EffectTypeDatas.SelectTypeid[effect.Type];

			var target = "";
			foreach (var node in effect.TargetNodes)
			{
				if (target != "") target += ", ";
				target += master.NodeDatas.SelectId[node].DisplayName;
			}
			string resource = "";
			foreach (var res in effect.TargetResources)
			{
				if (resource != "") resource += ", ";
				resource += master.ResourceDatas.SelectId[res].DisplayName;
			}
			failedEffectText.gameObject.transform.parent.gameObject.SetActive(true);
			failedEffectText.text = string.Format(effectType.Description, target, effect.Value, resource, effect.Duration);
		}
		else
		{
			failedEffectText.gameObject.transform.parent.gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// Acceptボタンのリスナーを設定
	/// </summary>
	public void SetOnAcceptListener(Action<RequestItemView> callback)
	{
		onAcceptCallback = callback;
		if (acceptButton != null)
		{
			acceptButton.onClick.RemoveAllListeners();
			acceptButton.onClick.AddListener(OnAcceptClicked);
		}
	}

	/// <summary>
	/// Rejectボタンのリスナーを設定
	/// </summary>
	public void SetOnRejectListener(Action<RequestItemView> callback)
	{
		onRejectCallback = callback;
		if (rejectButton != null)
		{
			rejectButton.onClick.RemoveAllListeners();
			rejectButton.onClick.AddListener(OnRejectClicked);
		}
	}

	private void OnAcceptClicked()
	{
		onAcceptCallback?.Invoke(this);
	}

	private void OnRejectClicked()
	{
		onRejectCallback?.Invoke(this);
	}

}