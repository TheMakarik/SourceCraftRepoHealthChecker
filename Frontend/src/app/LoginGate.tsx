import { authLoginUrl } from "../shared/api/client";
import { appConfig } from "../shared/config";
import { useMinScreen } from "../shared/ui/useMinScreen";

export function LoginGate({ inline = false }: { inline?: boolean }) {
  const tooSmall = useMinScreen();

  if (tooSmall && !inline)
    return (
      <div className="small-screen">
        <div>
          <h2>Экран слишком маленький</h2>
          <p className="muted">
            Ваш размер экрана слишком маленький для работы с SourceCraft Repo Health Checker.
            <br />
            Требуется минимум {appConfig.minSupportedWidthRem} rem (примерно {appConfig.minSupportedWidthPx} px)
            ширины.
          </p>
        </div>
      </div>
    );

  const card = (
    <div className="card login-gate__card" style={inline ? { marginInline: "auto" } : undefined}>
      <img className="login-gate__logo" src="/assets/sourcecraft-icon.svg" alt="SourceCraft" />
      <h1 className="login-gate__title">Repo Health Checker</h1>
      <p className="muted login-gate__text">
        Войдите через Я ID, чтобы открыть свои репозитории и настроить PAT с ИИ. Публичный рейтинг и анализ открытых
        репозиториев доступны без входа.
      </p>
      <a className="yandex-button" href={authLoginUrl}>
        <img className="yandex-button__icon" src="/assets/yandex-icon.png" alt="" />
        <span>Войти через Я ID</span>
      </a>
    </div>
  );

  if (inline)
    return card;

  return <div className="login-gate">{card}</div>;
}
