// Assets/Scripts/Editor/GlobalEffectControllerEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Effects;

[CustomEditor(typeof(GlobalEffectController))]
public class GlobalEffectControllerEditor : Editor
{
    bool _foldSummary = true;
    bool _foldBuckets = true;
    bool _foldRaw = false;
    bool _foldNodeMap = false;
    bool _foldOps = false;

    static Type _loggerType;
    static FieldInfo fEnableEffects, fEnableProduction, fEnableTransfer, fEnableSaveLoad, fEnableLayout, fVerbose;

    // private fields（読み取りのみ）
    FieldInfo fRemain;   // Dictionary<int,float> globalEffectRemainingSec
    FieldInfo fNodeMap;  // Dictionary<int, List<NodeEffectController>> nodeEffects
    FieldInfo fTickInt;  // float tickIntervalSec

    void OnEnable()
    {
        _loggerType = Type.GetType("EffectLogger, Assembly-CSharp");
        if (_loggerType != null)
        {
            fEnableEffects    = _loggerType.GetField("EnableEffects");
            fEnableProduction = _loggerType.GetField("EnableProduction");
            fEnableTransfer   = _loggerType.GetField("EnableTransfer");
            fEnableSaveLoad   = _loggerType.GetField("EnableSaveLoad");
            fEnableLayout     = _loggerType.GetField("EnableLayout");
            fVerbose          = _loggerType.GetField("Verbose");
        }

        var t = typeof(GlobalEffectController);
        fRemain  = t.GetField("globalEffectRemainingSec", BindingFlags.Instance | BindingFlags.NonPublic);
        fNodeMap = t.GetField("nodeEffects", BindingFlags.Instance | BindingFlags.NonPublic);
        fTickInt = t.GetField("tickIntervalSec", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public override void OnInspectorGUI()
    {
        var g = (GlobalEffectController)target;

        // --- EffectLogger toggles ---
        DrawLoggerToggles();

        // --- Summary ---
        _foldSummary = EditorGUILayout.BeginFoldoutHeaderGroup(_foldSummary, "Summary");
        if (_foldSummary)
        {
            float tick = fTickInt != null ? (float)fTickInt.GetValue(g) : 0f;
            EditorGUILayout.LabelField("Tick Interval (sec)", tick.ToString("0.###"));

            // 残寿命合計件数
            var dict = fRemain?.GetValue(g) as System.Collections.IDictionary;
            EditorGUILayout.LabelField("Global Remaining Entries", (dict != null ? dict.Count : 0).ToString());

            // 登録ノード数
            var nodeMap = fNodeMap?.GetValue(g) as System.Collections.IDictionary;
            EditorGUILayout.LabelField("Registered Nodes", (nodeMap != null ? nodeMap.Count : 0).ToString());
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- Buckets (typeId merged) ---
        _foldBuckets = EditorGUILayout.BeginFoldoutHeaderGroup(_foldBuckets, "Type Buckets (Merged)");
        if (_foldBuckets)
        {
            foreach (var b in g.EnumerateBuckets())
            {
                string nodes = b.affectsAllNodes ? "ALL" : string.Join(",", b.targetNodes);
                EditorGUILayout.LabelField($"[{b.typeId}] {b.displayName}  v={b.valueSum}  Nodes={nodes}");
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- Raw Global Effects ---
        _foldRaw = EditorGUILayout.BeginFoldoutHeaderGroup(_foldRaw, "Global Effects (Raw)");
        if (_foldRaw)
        {
            var remain = fRemain?.GetValue(g) as System.Collections.IDictionary;
            var table = new Dictionary<int, float>();
            if (remain != null)
            {
                foreach (System.Collections.DictionaryEntry kv in remain)
                    table[(int)kv.Key] = (float)kv.Value;
            }

            foreach (var e in g.EnumerateAllGlobalEffectsRaw())
            {
                float sec = table.TryGetValue(e.Id, out var v) ? v : -1f;
                EditorGUILayout.LabelField($"Id={e.Id}  Type={e.Type}  Val={e.Value}  Dur={e.Duration}  Remain={(sec>0?FormatTime(sec):"-")}");
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- Node Map (nodeId -> NEC count) ---
        _foldNodeMap = EditorGUILayout.BeginFoldoutHeaderGroup(_foldNodeMap, "Node Map (nodeId → NEC count)");
        if (_foldNodeMap)
        {
            var nodeMap = fNodeMap?.GetValue(g) as System.Collections.IDictionary;
            if (nodeMap != null)
            {
                foreach (System.Collections.DictionaryEntry kv in nodeMap)
                {
                    int nodeId = (int)kv.Key;
                    var list = kv.Value as System.Collections.IList;
                    EditorGUILayout.LabelField($"nodeId={nodeId}", $"NEC={ (list!=null ? list.Count : 0) }");
                }
            }
            else
            {
                EditorGUILayout.LabelField("(no registered nodes)");
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- Operations ---
        _foldOps = EditorGUILayout.BeginFoldoutHeaderGroup(_foldOps, "Operations");
        if (_foldOps)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Tick Once"))
                {
                    // TickGlobalEffects(float dt) を 1回だけ呼ぶ
                    var mTick = typeof(GlobalEffectController).GetMethod("TickGlobalEffects",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (mTick != null)
                    {
                        float dt = fTickInt != null ? (float)fTickInt.GetValue(g) : 1f;
                        mTick.Invoke(g, new object[] { Mathf.Max(0.001f, dt) });
                    }
                }
                if (GUILayout.Button("Refresh Times on All Nodes"))
                {
                    // 全登録ノードのバッジ時間を再計算（軽量）
                    var nodeMap = fNodeMap?.GetValue(g) as System.Collections.IDictionary;
                    if (nodeMap != null)
                    {
                        foreach (System.Collections.DictionaryEntry kv in nodeMap)
                        {
                            var list = kv.Value as System.Collections.IList;
                            if (list == null) continue;
                            foreach (var nec in list)
                            {
                                var mRefresh = nec.GetType().GetMethod("RefreshAllBadgeTimes",
                                    BindingFlags.Instance | BindingFlags.NonPublic);
                                mRefresh?.Invoke(nec, null);
                            }
                        }
                    }
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawLoggerToggles()
    {
        if (_loggerType == null) return;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("EffectLogger", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            DrawStaticToggle("Effects", fEnableEffects);
            DrawStaticToggle("Production", fEnableProduction);
            DrawStaticToggle("Transfer", fEnableTransfer);
            DrawStaticToggle("SaveLoad", fEnableSaveLoad);
            DrawStaticToggle("Layout", fEnableLayout);
            DrawStaticToggle("Verbose", fVerbose);
        }
        EditorGUILayout.Space(6);
    }

    void DrawStaticToggle(string label, FieldInfo fi)
    {
        if (fi == null) return;
        bool v = (bool)fi.GetValue(null);
        bool nv = EditorGUILayout.Toggle(label, v);
        if (nv != v) fi.SetValue(null, nv);
    }

    static string FormatTime(float sec)
    {
        if (sec <= 0f) return "-";
        int total = Mathf.CeilToInt(sec);
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }
}
#endif