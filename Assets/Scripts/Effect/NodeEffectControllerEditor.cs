// Assets/Scripts/Editor/NodeEffectControllerEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

[CustomEditor(typeof(NodeEffectController))]
public class NodeEffectControllerEditor : Editor
{
    bool _foldSummary = true;
    bool _foldEffects = true;
    bool _foldLocalTimes = false;
    bool _foldOps = false;

    // EffectLogger の存在は Dev/Editor 時のみ
    static Type _loggerType;
    static FieldInfo fEnableEffects, fEnableProduction, fEnableTransfer, fEnableSaveLoad, fEnableLayout, fVerbose;

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
    }

    public override void OnInspectorGUI()
    {
        var nec = (NodeEffectController)target;
        var nodeProp = typeof(NodeEffectController).GetProperty("NodeId", BindingFlags.Instance | BindingFlags.Public);

        // --- EffectLogger toggles ---
        DrawLoggerToggles();

        // --- Summary ---
        _foldSummary = EditorGUILayout.BeginFoldoutHeaderGroup(_foldSummary, "Summary");
        if (_foldSummary)
        {
            int nodeId = (int)(nodeProp?.GetValue(nec) ?? -1);
            EditorGUILayout.LabelField("NodeId", nodeId.ToString());

            // NodeView 情報（入出力合計・レベルなど）
            var nodeView = nec.GetComponent<NodeView>();
            if (nodeView != null)
            {
                EditorGUILayout.LabelField("Title", nodeView.titleText != null ? nodeView.titleText.text : "(no title)");
                EditorGUILayout.LabelField("Level", nodeView.nodeLevel.ToString());
                int sumIn  = nodeView.inputPorts.Sum(p => p != null ? p.RequiredAmount : 0);
                int sumOut = nodeView.outputPorts.Sum(p => p != null ? p.ProduceAmount : 0);
                EditorGUILayout.LabelField("Inputs Σ Required", sumIn.ToString());
                EditorGUILayout.LabelField("Outputs Σ Produce", sumOut.ToString());
            }

            // _outputsToRemove（private）を読み取り
            var fRemove = typeof(NodeEffectController).GetField("_outputsToRemove",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (fRemove != null)
            {
                int left = (int)fRemove.GetValue(nec);
                EditorGUILayout.LabelField("RemoveByOutputCount Left", left.ToString());
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- Effects (Buckets merged) ---
        _foldEffects = EditorGUILayout.BeginFoldoutHeaderGroup(_foldEffects, "Active Effects (Merged Buckets)");
        if (_foldEffects)
        {
            // BuildCurrentBucketsForDebug()：現在このノードに効くタイプの合成バケット
            var m = typeof(NodeEffectController).GetMethod("BuildCurrentBucketsForDebug",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var list = (IEnumerable<object>)m.Invoke(nec, null);
            if (list != null)
            {
                // 残寿命参照
                var mRemain = typeof(NodeEffectController).GetMethod("GetRemainingSecondsForType_ForDebug",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var b in list)
                {
                    var tBucket = b.GetType();
                    int typeId = (int)tBucket.GetField("typeId").GetValue(b);
                    string name = (string)tBucket.GetField("displayName").GetValue(b);
                    int valueSum = (int)tBucket.GetField("valueSum").GetValue(b);

                    float sec = (float)mRemain.Invoke(nec, new object[] { typeId });
                    string secStr = sec > 0 ? FormatTime(sec) : "-";

                    // 出典：Global/Local/GL 判定の簡易化（HasLocalTypeId + Global.EnumerateBuckets）
                    bool local = (bool)typeof(NodeEffectController).GetMethod("HasLocalTypeId",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Invoke(nec, new object[] { typeId });
                    bool global = false;
                    foreach (var gb in Effects.GlobalEffectController.Instance.EnumerateBuckets())
                        if (gb.typeId == typeId && gb.IsTargetNode(nec.NodeId)) { global = true; break; }

                    string src = global && local ? "GL" : (global ? "G" : (local ? "L" : "-"));
                    EditorGUILayout.LabelField($"[{typeId}] {name}  v={valueSum}  remain={secStr}  src={src}");
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- Local Remaining Times (effectId -> sec) ---
        _foldLocalTimes = EditorGUILayout.BeginFoldoutHeaderGroup(_foldLocalTimes, "Local Remaining (effectId → sec)");
        if (_foldLocalTimes)
        {
            // private readonly Dictionary<int,float> localEffectRemainingSec
            var f = typeof(NodeEffectController).GetField("localEffectRemainingSec",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = f?.GetValue(nec) as System.Collections.IDictionary;
            if (dict != null)
            {
                foreach (System.Collections.DictionaryEntry kv in dict)
                    EditorGUILayout.LabelField($"effectId={kv.Key}", $"{FormatTime((float)kv.Value)}");
            }
            else
            {
                EditorGUILayout.LabelField("(no local duration entries)");
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- Operations ---
        _foldOps = EditorGUILayout.BeginFoldoutHeaderGroup(_foldOps, "Operations");
        if (_foldOps)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("ApplyAll()"))
                {
                    var mApply = typeof(NodeEffectController).GetMethod("ApplyAll",
                        BindingFlags.Instance | BindingFlags.Public);
                    mApply?.Invoke(nec, null);
                }
                if (GUILayout.Button("Refresh Badge Times"))
                {
                    var mRefresh = typeof(NodeEffectController).GetMethod("RefreshAllBadgeTimes",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    mRefresh?.Invoke(nec, null);
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