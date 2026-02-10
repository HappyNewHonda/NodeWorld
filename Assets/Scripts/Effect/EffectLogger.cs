// Assets/Scripts/Debug/EffectLogger.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public static class EffectLogger
{
    public enum Category
    {
        Effects,       // 集約・再適用・寿命
        Production,    // 生産・在庫
        Transfer,      // 資源移送トークン
        SaveLoad,      // セーブ/ロード
        Layout,        // バッジ/レイアウト
    }

    // カテゴリ別の有効フラグ（必要に応じて保存/ロードしてもOK）
    public static bool EnableEffects   = true;
    public static bool EnableProduction= false;
    public static bool EnableTransfer  = false;
    public static bool EnableSaveLoad  = true;
    public static bool EnableLayout    = false;

    public static bool Verbose = false; // 冗長ログ

    public static void Log(Category cat, string msg)
    {
        if (!IsEnabled(cat)) return;
        Debug.Log($"[NW:{cat}] {msg}");
    }

    public static void LogVerbose(Category cat, string msg)
    {
        if (!IsEnabled(cat) || !Verbose) return;
        Debug.Log($"[NW:{cat}:V] {msg}");
    }

    public static void Warn(Category cat, string msg)
    {
        if (!IsEnabled(cat)) return;
        Debug.LogWarning($"[NW:{cat}] {msg}");
    }

    public static void Error(Category cat, string msg)
    {
        Debug.LogError($"[NW:{cat}] {msg}");
    }

    static bool IsEnabled(Category cat) => cat switch
    {
        Category.Effects    => EnableEffects,
        Category.Production => EnableProduction,
        Category.Transfer   => EnableTransfer,
        Category.SaveLoad   => EnableSaveLoad,
        Category.Layout     => EnableLayout,
        _ => true
    };
}
#endif