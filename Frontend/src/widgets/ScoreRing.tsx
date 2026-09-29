import { scoreGrade, scoreTone, toneColor } from "../shared/api/labels";

export function ScoreRing({
  score,
  size = 160,
  thickness = 14,
  grade
}: {
  score: number | null;
  size?: number;
  thickness?: number;
  grade?: string;
}) {
  const radius = (size - thickness) / 2;
  const circumference = 2 * Math.PI * radius;
  const clamped = score === null ? 0 : Math.max(0, Math.min(100, score));
  const dash = (clamped / 100) * circumference;
  const color = score === null ? "var(--srhc-muted)" : toneColor(scoreTone(score));
  const caption = grade ?? (score === null ? "Нет данных" : scoreGrade(score));

  return (
    <svg className="score-ring" width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="img" aria-label={score === null ? "Score: нет данных" : `Score ${score}`}>
      <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke="var(--srhc-border)" strokeWidth={thickness} />
      <circle
        cx={size / 2}
        cy={size / 2}
        r={radius}
        fill="none"
        stroke={color}
        strokeWidth={thickness}
        strokeLinecap="round"
        strokeDasharray={`${dash} ${circumference - dash}`}
        transform={`rotate(-90 ${size / 2} ${size / 2})`}
      />
      <text x="50%" y="47%" textAnchor="middle" dominantBaseline="middle" fontSize={size * 0.26} fontWeight={700} fill={color}>
        {score === null ? "—" : score}
      </text>
      <text x="50%" y="68%" textAnchor="middle" dominantBaseline="middle" fontSize={size * 0.09} fill="var(--srhc-muted)">
        {caption}
      </text>
    </svg>
  );
}
