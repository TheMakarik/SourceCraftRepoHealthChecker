import { Card } from "@fluentui/react-components";
import { categoryLabels, categoryOrder } from "../shared/api/labels";

const weights: Record<string, string> = {
  Security: "20%",
  CodeHealth: "20%",
  Activity: "15%",
  Documentation: "15%",
  CiCd: "15%",
  Issues: "15%"
};

export function MethodologyPage() {
  return (
    <div className="stack">
      <Card className="card stack">
        <h2 style={{ margin: 0 }}>Методика Repo Health Score</h2>
        <p className="muted" style={{ margin: 0 }}>
          Итоговый балл 0–100 — взвешенное среднее оценок шести категорий. Категория — взвешенное среднее её метрик,
          поэтому объяснение сходится с баллом. Нормализация метрики — линейная между худшим и лучшим значением.
        </p>
      </Card>

      <Card className="card stack">
        <div style={{ fontWeight: 600 }}>Категории и веса</div>
        {categoryOrder.map((category) => (
          <div className="metric-row" key={category}>
            <span>{categoryLabels[category]}</span>
            <strong>{weights[category]}</strong>
          </div>
        ))}
      </Card>

      <Card className="card stack">
        <div style={{ fontWeight: 600 }}>Правило «Нет данных»</div>
        <p className="muted" style={{ margin: 0 }}>
          Отсутствие данных — отдельный статус, а не плохой результат. Категории со статусом «Нет данных» исключаются
          из расчёта, а веса доступных нормируются. Если недоступен AppSec, Security не штрафуется и помечается
          подсказкой подключить AppSec SourceCraft.
        </p>
      </Card>

      <Card className="card stack">
        <div style={{ fontWeight: 600 }}>Устойчивость</div>
        <p className="muted" style={{ margin: 0 }}>
          Здоровье важнее популярности: лайки не подменяют качество, а второстепенные показатели не доминируют.
          Простые способы искусственно поднять Score не дают существенного эффекта.
        </p>
      </Card>
    </div>
  );
}
