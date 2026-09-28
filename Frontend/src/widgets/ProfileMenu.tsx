import { useState } from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Popover,
  PopoverSurface,
  PopoverTrigger
} from "@fluentui/react-components";
import { authLoginUrl } from "../shared/api/client";
import { useLogout, useMe } from "../shared/api/hooks";

export function ProfileMenu() {
  const me = useMe();
  const logout = useLogout();
  const [popoverOpen, setPopoverOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  if (!me.isSuccess)
    return (
      <Button
        as="a"
        href={authLoginUrl}
        appearance="primary"
        size="small"
        title="Войти через Я ID"
        style={{ fontSize: "0.72rem", minWidth: 0, padding: "0.35rem 0.45rem" }}
      >
        Войти
      </Button>
    );

  const displayName = me.data.displayName || me.data.login || "Пользователь";
  const initials = displayName.trim().charAt(0).toUpperCase();

  return (
    <>
      <Popover
        open={popoverOpen}
        onOpenChange={(_event, data) => setPopoverOpen(data.open)}
        positioning="above-end"
        withArrow
        trapFocus
      >
        <PopoverTrigger disableButtonEnhancement>
          <button className="app__nav-link app__profile" title="Профиль">
            {me.data.avatarUrl ? (
              <img className="user-avatar" src={me.data.avatarUrl} alt="" />
            ) : (
              <span className="user-avatar">{initials}</span>
            )}
          </button>
        </PopoverTrigger>
        <PopoverSurface className="profile-card">
          <div className="profile-card__head">
            {me.data.avatarUrl ? (
              <img className="user-avatar user-avatar--lg" src={me.data.avatarUrl} alt="" />
            ) : (
              <span className="user-avatar user-avatar--lg">{initials}</span>
            )}
            <div>
              <div style={{ fontWeight: 600 }}>{displayName}</div>
              <div className="muted">Я ID: {me.data.login ?? "—"}</div>
              <div className="muted">{me.data.email || "—"}</div>
            </div>
          </div>
          <Button
            className="btn-danger"
            appearance="primary"
            onClick={() => {
              setPopoverOpen(false);
              setConfirmOpen(true);
            }}
          >
            Выйти
          </Button>
        </PopoverSurface>
      </Popover>

      <Dialog open={confirmOpen} onOpenChange={(_event, data) => setConfirmOpen(data.open)} modalType="modal">
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Выйти из аккаунта?</DialogTitle>
            <DialogContent>
              Вы действительно хотите выйти? Для работы с личными разделами потребуется снова войти через Я ID.
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setConfirmOpen(false)}>
                Отмена
              </Button>
              <Button
                className="btn-danger"
                appearance="primary"
                disabled={logout.isPending}
                onClick={() => logout.mutate()}
              >
                Выйти
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
}
