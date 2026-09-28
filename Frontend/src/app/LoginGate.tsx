import { authLoginUrl } from "../shared/api/client";
import { appConfig } from "../shared/config";
import { useMinScreen } from "../shared/ui/useMinScreen";

export function LoginGate() {
  const tooSmall = useMinScreen();

  if (tooSmall)
    return (
      <div className="small-screen">
        <div>
          <h2>Экран слишком маленький</h2>
          <p className="muted">
            Ваш размер экрана слишком маленький для работы с SourceCraft Repo Health Checker.
            <br />
            Требуется минимум {appConfig.minSupportedWidthRem}×{appConfig.minSupportedHeightRem} rem (примерно{" "}
            {appConfig.minSupportedWidthPx}×{appConfig.minSupportedHeightPx} px).
          </p>
        </div>
      </div>
    );

  return (
    <div className="login-gate">
      <div className="card login-gate__card">
        <img className="login-gate__logo" src="/assets/sourcecraft-icon.svg" alt="SourceCraft" />
        <h1 className="login-gate__title">Repo Health Checker</h1>
        <p className="muted login-gate__text">
          Сервис доступен только после входа. Авторизуйтесь через Я ID, затем введите PAT SourceCraft в личном кабинете,
          чтобы анализировать свои репозитории.
        </p>
        <a className="yandex-button" href={authLoginUrl}>
          <img className="yandex-button__icon" src="/assets/yandex-icon.png" alt="" />
          <span>Войти через Я ID</span>
        </a>
      </div>
    </div>
  );
}
