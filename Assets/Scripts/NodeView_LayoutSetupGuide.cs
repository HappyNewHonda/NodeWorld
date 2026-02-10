// ========================================
// NodeView自動レイアウト - 確認と設定手順
// ========================================

/*
## 現在の実装状態（確認済み）

✅ NodeView.cs:
- SetupFromData()メソッド: SetupContainerAnchors()を呼び出し
- SetupContainerAnchors()メソッド: コンテナのAnchorを設定
- レイアウト強制更新: PortRoot→NodeViewの順で更新

✅ NodeView.prefab:
- NodeView本体: ContentSizeFitter追加済み
- PortRoot: VerticalLayoutGroup + ContentSizeFitter追加済み
- InputContainer: VerticalLayoutGroup + ContentSizeFitter追加済み
- OutputContainer: VerticalLayoutGroup + ContentSizeFitter追加済み

## Unityエディタで確認・設定すべき項目

### ステップ1: NodeView.prefabを開く
1. Project ウィンドウで Assets/Prefabs/NodeView.prefab を選択
2. Inspector で以下を確認

### ステップ2: NodeView（ルート）の設定
ContentSizeFitter:
- Horizontal Fit: Unconstrained
- Vertical Fit: Preferred Size ★これが最重要

RectTransform:
- Width: 200（任意）
- Anchor/Pivot: デフォルトのままでOK

### ステップ3: PortRootの設定
VerticalLayoutGroup:
- Padding:
  - Top: 40 ★タイトル分のスペース
  - Bottom: 20
  - Left: 0
  - Right: 0
- Spacing: 20 ★InputとOutputの間隔
- Child Alignment: Upper Left
- Child Control Width: ☑ ON ★重要
- Child Control Height: ☐ OFF
- Child Force Expand Width: ☑ ON ★重要
- Child Force Expand Height: ☐ OFF

ContentSizeFitter:
- Horizontal Fit: Unconstrained
- Vertical Fit: Preferred Size ★重要

RectTransform:
- Anchor: Min(0, 1), Max(1, 1) - 横幅いっぱい
- Pivot: (0, 1) - 左上
- Anchored Position: (0, 0)

### ステップ4: InputContainerの設定
VerticalLayoutGroup:
- Padding: すべて0
- Spacing: 10 ★ポート間の間隔
- Child Alignment: Upper Left ★左上から縦に
- Child Control: すべて☐ OFF
- Child Force Expand: すべて☐ OFF

ContentSizeFitter:
- Horizontal Fit: Preferred Size ★重要
- Vertical Fit: Preferred Size ★重要

RectTransform:
- Anchor: (0, 1) - 左上
- Pivot: (0, 1) - 左上

### ステップ5: OutputContainerの設定
VerticalLayoutGroup:
- Padding: すべて0
- Spacing: 10 ★ポート間の間隔
- Child Alignment: Upper Left ★左から縦に（右寄せはポートのanchorで制御）
- Child Control: すべて☐ OFF
- Child Force Expand: すべて☐ OFF

ContentSizeFitter:
- Horizontal Fit: Preferred Size ★重要
- Vertical Fit: Preferred Size ★重要

RectTransform:
- Anchor: (0, 1) - 左上（LayoutGroupが制御）
- Pivot: (0, 1) - 左上

## テスト手順

### ステップ1: NodeFromDataTest.csでテスト
```csharp
// GraphUIManager.Instance.CreateNodeFromData(1, 1, Vector2.zero);
```

### ステップ2: 確認すべき動作
1. ✅ Inputポートが左側に縦並びで追加される
2. ✅ Outputポートが右側に縦並びで追加される
3. ✅ OutputContainerがInputContainerの下に移動する ★重要
4. ✅ NodeViewの高さがポート数に応じて自動的に増える ★重要

### ステップ3: 問題が起きている場合のチェックリスト

#### OutputContainerが動かない場合:
□ PortRootのVerticalLayoutGroupのChild Control Width: ON
□ PortRootのVerticalLayoutGroupのChild Force Expand Width: ON
□ PortRootのVerticalLayoutGroupのSpacing: 20（0以外）
□ OutputContainerのAnchor: (0, 1)（左上基準）
□ OutputContainerのLayoutGroupのChild Alignment: Upper Left

#### NodeViewの高さが変わらない場合:
□ NodeViewのContentSizeFitter Vertical Fit: Preferred Size
□ PortRootのContentSizeFitter Vertical Fit: Preferred Size
□ InputContainer/OutputContainerのContentSizeFitter: 両方Preferred Size
□ BackImageなど他の要素が固定サイズになっていないか確認

#### ポートが重なる場合:
□ InputContainer/OutputContainerのSpacing: 10
□ 各ポートにLayoutElementが追加されているか（CreatePort()で自動追加）
□ LayoutElementのpreferredHeight/Widthが正しく設定されているか

## デバッグ方法

### 方法1: Hierarchy で実行時の状態を確認
1. Play モードに入る
2. NodeFromDataTest.csでノードを生成
3. Hierarchyでノードを選択
4. Inspectorで各RectTransformのサイズと位置を確認

### 方法2: Layout Debugger を使用
1. Window > Analysis > Layout Debugger を開く
2. 対象のNodeViewを選択
3. LayoutGroupの計算結果を確認

### 方法3: コンソールログで確認
SetupFromData()に以下を追加してデバッグ:
```csharp
Debug.Log($"InputContainer: {inputPortContainer.sizeDelta}");
Debug.Log($"OutputContainer: {outputPortContainer.sizeDelta}");
Debug.Log($"PortRoot: {portRoot.GetComponent<RectTransform>().sizeDelta}");
Debug.Log($"NodeView: {rt.sizeDelta}");
```

## 既知の問題と対策

### 問題1: Prefab変更が反映されない
対策: 
- シーン内の既存NodeViewインスタンスを削除
- Prefabから新しくインスタンスを配置
- または Play モードで動的に生成

### 問題2: ContentSizeFitterが機能しない
原因:
- 親要素が固定サイズになっている
- LayoutGroupと競合している

対策:
- 親要素のContentSizeFitterを確認
- LayoutElementの設定を確認

### 問題3: レイアウトが1フレーム遅れる
原因:
- LayoutRebuilderの更新タイミング

対策:
- Canvas.ForceUpdateCanvases()を先に呼ぶ
- LayoutRebuilder.ForceRebuildLayoutImmediateを子から親の順で呼ぶ

## まとめ

最も重要な設定:
1. ★ NodeView: ContentSizeFitter Vertical Fit = Preferred Size
2. ★ PortRoot: VerticalLayoutGroup Child Control Width = ON
3. ★ PortRoot: ContentSizeFitter Vertical Fit = Preferred Size
4. ★ Input/OutputContainer: ContentSizeFitter 両方 = Preferred Size

これらが正しく設定されていれば、ポート数に応じて自動的にレイアウトが調整されます。
*/
