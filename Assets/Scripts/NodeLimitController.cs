using UnityEngine;

/// <summary>
/// ノード数の上限を管理するシングルトン。
/// 現在は定数30。将来はマスターデータで管理予定。
/// </summary>
public class NodeLimitController : MonoBehaviour
{
    public static NodeLimitController Instance { get; private set; }

    /// <summary>
    /// ノード配置上限（暫定定数。将来マスターデータ化予定）
    /// </summary>
    public const int MAX_NODE_COUNT = 30;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 現在のノード数を取得
    /// </summary>
    public int GetCurrentNodeCount()
    {
        var nodeLayer = GraphUIManager.Instance?.nodeLayer;
        if (nodeLayer == null) return 0;

        int count = 0;
        foreach (Transform t in nodeLayer)
        {
            var nv = t.GetComponent<NodeView>();
            if (nv != null && nv.nodeId >= 0) count++;
        }
        return count;
    }

    /// <summary>
    /// ノードを追加できるか
    /// </summary>
    public bool CanAddNode()
    {
        return GetCurrentNodeCount() < MAX_NODE_COUNT;
    }

    /// <summary>
    /// 指定数のノードを追加できるか
    /// </summary>
    public bool CanAddNodes(int count)
    {
        return GetCurrentNodeCount() + count <= MAX_NODE_COUNT;
    }

    /// <summary>
    /// 残り配置可能数
    /// </summary>
    public int RemainingSlots()
    {
        return Mathf.Max(0, MAX_NODE_COUNT - GetCurrentNodeCount());
    }
}
