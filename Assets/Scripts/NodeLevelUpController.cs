using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Master;
using System.Linq;

/// <summary>
/// ノードのレベルアップを管理するコンポーネント。
/// NodeView と同じ GameObject にアタッチする。
/// </summary>
[RequireComponent(typeof(NodeView))]
public class NodeLevelUpController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button levelUpButton;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI costText;

    private NodeView nodeView;
    private NodeEffectController nodeEffectController;

    void Awake()
    {
        nodeView = GetComponent<NodeView>();
        nodeEffectController = GetComponent<NodeEffectController>();
    }

    void Start()
    {
        if (levelUpButton != null)
        {
            levelUpButton.onClick.AddListener(OnLevelUpClicked);
        }
        UpdateDisplay();
    }

    void OnEnable()
    {
        if (UserData.Instance != null)
        {
            UserData.Instance.OnMoneyChanged += OnMoneyChanged;
        }
    }

    void OnDisable()
    {
        if (UserData.Instance != null)
        {
            UserData.Instance.OnMoneyChanged -= OnMoneyChanged;
        }
    }

    private void OnMoneyChanged(int newMoney)
    {
        UpdateDisplay();
    }

    /// <summary>
    /// 表示を更新（レベル、コスト、ボタンの有効/無効）
    /// </summary>
    public void UpdateDisplay()
    {
        if (nodeView == null) return;

        // レベル表示
        if (levelText != null)
        {
            levelText.text = $"Lv.{nodeView.nodeLevel}";
        }

        // 次のレベルのコストと可否を計算
        int nextCost = GetNextLevelCost();
        bool canLevelUp = CanLevelUp();

        // コスト表示
        if (costText != null)
        {
            if (nextCost < 0)
            {
                costText.text = "MAX";
            }
            else
            {
                costText.text = $"${nextCost}";
            }
        }

        // ボタンの有効/無効
        if (levelUpButton != null)
        {
            levelUpButton.interactable = canLevelUp;
        }
    }

    /// <summary>
    /// レベルアップ可能かどうか
    /// </summary>
    public bool CanLevelUp()
    {
        int nextCost = GetNextLevelCost();
        if (nextCost < 0) return false; // 次のレベルが存在しない

        // お金が足りるか
        if (UserData.Instance.Money < nextCost) return false;

        // 次のレベルのIOデータが存在するか
        return HasNextLevelIoData();
    }

    /// <summary>
    /// 次のレベルのコストを取得。次がなければ -1 を返す。
    /// </summary>
    public int GetNextLevelCost()
    {
        if (nodeView == null) return -1;

        var master = MasterData.Instance;
        if (master == null || master.NodeCostDatas == null) return -1;

        // NodeCostData は「Id=nodeId, Count=何番目のノードか」ではなく、
        // このプロジェクトでは Count をレベルとして使う場合もあるが、
        // 実際のデータを見ると Count は「生成数」を表している。
        // レベルアップコストは NodeIoData の次レベルが存在するかで判定し、
        // コストは NodeCostData の Count=現在のレベル+1 のエントリを使う。

        // NodeCostData を探す
        if (!master.NodeCostDatas.SelectId.ContainsKey(nodeView.nodeId))
            return -1;

        var costArray = master.NodeCostDatas.SelectId[nodeView.nodeId];
        int nextLevel = nodeView.nodeLevel + 1;

        // Count が次のレベルに対応するエントリを探す
        // NodeCostData の Count は「作成済み数」で、レベルとは別用途の場合がある。
        // ここではレベルアップコストとして nodeLevel+1 番目のエントリを使う。
        // 配列のインデックス = nodeLevel（0-indexed）とする。
        if (nodeView.nodeLevel < costArray.Length)
        {
            return costArray[nodeView.nodeLevel].Cost;
        }

        return -1; // これ以上レベルアップできない
    }

    /// <summary>
    /// 次のレベルのIOデータが存在するか
    /// </summary>
    private bool HasNextLevelIoData()
    {
        if (nodeView == null) return false;

        var master = MasterData.Instance;
        if (master == null || master.NodeIoDatas == null) return false;

        if (!master.NodeIoDatas.SelectId.ContainsKey(nodeView.nodeId))
            return false;

        var ioArray = master.NodeIoDatas.SelectId[nodeView.nodeId];
        int nextLevel = nodeView.nodeLevel + 1;
        return ioArray.Any(io => io.Level == nextLevel);
    }

    /// <summary>
    /// レベルアップボタンが押された
    /// </summary>
    private void OnLevelUpClicked()
    {
        if (!CanLevelUp()) return;

        int cost = GetNextLevelCost();
        if (cost < 0) return;

        // お金を消費
        if (!UserData.Instance.SpendMoney(cost))
        {
            Debug.LogWarning("[LevelUp] Not enough money");
            return;
        }

        int nextLevel = nodeView.nodeLevel + 1;

        // 次のレベルのIOデータを取得
        var master = MasterData.Instance;
        var ioArray = master.NodeIoDatas.SelectId[nodeView.nodeId];
        var nextIo = ioArray.FirstOrDefault(io => io.Level == nextLevel);
        if (nextIo == null)
        {
            Debug.LogError($"[LevelUp] No IoData for nodeId={nodeView.nodeId}, level={nextLevel}");
            return;
        }

        // 現在のエッジ接続を保存
        var savedEdges = SaveCurrentEdges();

        // ノードデータ名を取得
        var nodeData = master.NodeDatas.SelectId[nodeView.nodeId];

        // Setupを呼び直してポート構成を再構築
        nodeView.Setup(
            nodeData.DisplayName,
            GraphUIManager.Instance,
            nextIo.InputResourceTypes ?? new int[0],
            nextIo.OutputResourceTypes ?? new int[0],
            nextIo.InputValues,
            nextIo.OutputValues,
            nextIo.OutputSec / 1000f,
            nodeView.nodeId,
            nextLevel
        );

        // エッジ接続を復元
        RestoreEdges(savedEdges);

        // 表示更新
        UpdateDisplay();

        Debug.Log($"[LevelUp] Node '{nodeData.DisplayName}' leveled up to Lv.{nextLevel} (cost: ${cost})");
    }

    /// <summary>
    /// 現在のエッジ接続情報を保存
    /// </summary>
    private System.Collections.Generic.List<SavedEdgeConnection> SaveCurrentEdges()
    {
        var saved = new System.Collections.Generic.List<SavedEdgeConnection>();

        // 入力ポートのエッジ
        for (int i = 0; i < nodeView.inputPorts.Count; i++)
        {
            var port = nodeView.inputPorts[i];
            foreach (var edge in port.edges.ToArray())
            {
                if (edge == null || edge.fromPort == null) continue;
                saved.Add(new SavedEdgeConnection
                {
                    isInput = true,
                    portIndex = i,
                    resourceType = port.resourceType,
                    otherPort = edge.fromPort,
                    isOtherOutput = true
                });
            }
        }

        // 出力ポートのエッジ
        for (int i = 0; i < nodeView.outputPorts.Count; i++)
        {
            var port = nodeView.outputPorts[i];
            foreach (var edge in port.edges.ToArray())
            {
                if (edge == null || edge.toPort == null) continue;
                saved.Add(new SavedEdgeConnection
                {
                    isInput = false,
                    portIndex = i,
                    resourceType = port.resourceType,
                    otherPort = edge.toPort,
                    isOtherOutput = false
                });
            }
        }

        // 既存のエッジをすべて切断（Setup で新しいポートが作られるため）
        foreach (var port in nodeView.inputPorts)
            port.RemoveEdgeAll();
        foreach (var port in nodeView.outputPorts)
            port.RemoveEdgeAll();

        return saved;
    }

    /// <summary>
    /// 保存したエッジ接続を復元
    /// </summary>
    private void RestoreEdges(System.Collections.Generic.List<SavedEdgeConnection> savedEdges)
    {
        var mgr = GraphUIManager.Instance;

        foreach (var se in savedEdges)
        {
            PortView myPort = null;
            PortView otherPort = se.otherPort;

            if (otherPort == null || otherPort.gameObject == null) continue;

            if (se.isInput)
            {
                // 入力ポート：同じリソースタイプのポートを探す
                myPort = FindPortByResourceType(nodeView.inputPorts, se.resourceType, se.portIndex);
                if (myPort == null) continue;

                // otherPort(output) -> myPort(input) の接続
                if (!mgr.CanConnect(otherPort, myPort)) continue;

                myPort.RemoveEdgeAll();
                var edge = Instantiate(mgr.edgePrefab, mgr.edgesLayer);
                edge.Initialize(mgr, isPreview: false);
                edge.BindPorts(otherPort, myPort);
            }
            else
            {
                // 出力ポート：同じリソースタイプのポートを探す
                myPort = FindPortByResourceType(nodeView.outputPorts, se.resourceType, se.portIndex);
                if (myPort == null) continue;

                // myPort(output) -> otherPort(input) の接続
                if (!mgr.CanConnect(myPort, otherPort)) continue;

                otherPort.RemoveEdgeAll();
                var edge = Instantiate(mgr.edgePrefab, mgr.edgesLayer);
                edge.Initialize(mgr, isPreview: false);
                edge.BindPorts(myPort, otherPort);
            }
        }
    }

    /// <summary>
    /// リソースタイプが一致するポートを探す（同タイプが複数あればインデックス優先）
    /// </summary>
    private PortView FindPortByResourceType(
        System.Collections.Generic.List<PortView> ports, int resourceType, int preferredIndex)
    {
        // まず同じインデックスで同じリソースタイプがあるか
        if (preferredIndex >= 0 && preferredIndex < ports.Count)
        {
            if (ports[preferredIndex].resourceType == resourceType)
                return ports[preferredIndex];
        }

        // なければリソースタイプで最初に見つかるものを返す
        foreach (var p in ports)
        {
            if (p.resourceType == resourceType) return p;
        }

        return null;
    }

    private struct SavedEdgeConnection
    {
        public bool isInput;
        public int portIndex;
        public int resourceType;
        public PortView otherPort;
        public bool isOtherOutput;
    }
}
