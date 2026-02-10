// Assets/Scripts/Debug/NodeEffectsDebugView.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;
using TMPro;
using Effects;

[DefaultExecutionOrder(1001)]
[RequireComponent(typeof(NodeView))]
public class NodeEffectsDebugView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI text;
    NodeView node;
    NodeEffectController nec;
    float acc;

    void Awake()
    {
        node = GetComponent<NodeView>();
        nec  = GetComponent<NodeEffectController>();
        if (nec != null) nec.OnNodeEffectsChanged += RefreshNow;

        // ランタイムで小さなTextを作る（Prefab改変不要）
        if (text == null)
        {
            var go = new GameObject("DebugEffectsText", typeof(RectTransform));
            go.transform.SetParent(node.rt, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-6, -6);

            text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 14;
            text.alignment = TextAlignmentOptions.TopRight;
            text.color = new Color(1,1,1,0.8f);
            text.enableWordWrapping = false;
            text.raycastTarget = false;
        }
    }

    void OnDestroy()
    {
        if (nec != null) nec.OnNodeEffectsChanged -= RefreshNow;
    }

    void Update()
    {
        // 毎秒更新（寿命表示追従）
        acc += Time.deltaTime;
        if (acc >= 1f)
        {
            acc -= 1f;
            RefreshNow();
        }
    }

    void RefreshNow()
    {
        if (nec == null || text == null) return;

        // 現在このノードに効く「マージ済みタイプバケット」を取得
        var buckets = nec.BuildCurrentBucketsForDebug(); // ★ 後述の拡張メソッド
        var sb = new StringBuilder();

        // 1行目：ノード概要
        sb.Append($"<color=#88FFAA>Node {node.nodeId}</color>  Lvl:{node.nodeLevel}\n");

        // 2行目：生産・在庫ざっくり（必要なら詳細化）
        sb.Append($"In:{SumInputs()}  Out:{SumOutputs()}\n");

        // 効果一覧
        foreach (var b in buckets)
        {
            // 残寿命（max）をNode側APIで取得
            float remain = nec.GetRemainingSecondsForType_ForDebug(b.typeId);
            string t = remain > 0 ? Format(remain) : "";
            string src = BucketSource(b, nec.NodeId); // "G/L/GL"
            sb.AppendLine($"{b.typeId,2}:{b.displayName} v={b.valueSum} {t} [{src}]");
        }

        text.text = sb.ToString();
    }

    string Format(float sec)
    {
        int total = Mathf.CeilToInt(sec);
        int m = (total % 3600) / 60;
        int s = total % 60;
        int h = total / 3600;
        return (h>0) ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    string BucketSource(EffectTypeBucket b, int nodeId)
    {
        bool g = false, l = false;
        // Source判定（簡易）：Global にこのバケットtypeIdがあり対象ノードならG
        foreach (var gb in GlobalEffectController.Instance.EnumerateBuckets())
            if (gb.typeId == b.typeId && gb.IsTargetNode(nodeId)) { g = true; break; }
        // Local に同typeIdがあるならL
        l = nec.HasLocalTypeId(b.typeId);
        return g && l ? "GL" : (g ? "G" : (l ? "L" : "-"));
    }

    int SumInputs()
    {
        int sum = 0;
        foreach (var p in node.inputPorts) sum += p?.RequiredAmount ?? 0;
        return sum;
    }
    int SumOutputs()
    {
        int sum = 0;
        foreach (var p in node.outputPorts) sum += p?.ProduceAmount ?? 0;
        return sum;
    }
}
#endif