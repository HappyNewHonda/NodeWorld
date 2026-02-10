using System.Collections.Generic;

public interface IObjectiveEvaluator
{
	// フレーム更新で呼ぶ or Coroutineなどで監視
	void Tick(float deltaTime);

	// 達成したら true
	bool IsSatisfied(object context); // context: 参照したいシミュレーションデータ
}

public class ObjectiveRegistry
{
	// "StableHouses60s", "NetProfit>=1.0_For60s" など ID→Evaluator
	Dictionary<string, IObjectiveEvaluator> evaluators = new();

	public IObjectiveEvaluator Get(string id) => evaluators.TryGetValue(id, out var e) ? e : null;
}
