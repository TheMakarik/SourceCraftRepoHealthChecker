import type { ReactElement } from "react";
import { Button, Menu, MenuItemLink, MenuList, MenuPopover, MenuTrigger } from "@fluentui/react-components";
import { MoreHorizontal24Regular, Open20Regular, Person20Regular } from "@fluentui/react-icons";

interface RepoMenuTarget {
  url: string;
  fullName: string;
}

function ownerUrl(fullName: string): string {
  const owner = fullName.split("/")[0]?.trim();
  return owner ? `https://sourcecraft.dev/${owner}` : "https://sourcecraft.dev";
}

function RepoMenuList({ url, fullName }: RepoMenuTarget) {
  return (
    <MenuList>
      <MenuItemLink href={url} target="_blank" rel="noreferrer" icon={<Open20Regular />}>
        Открыть в SourceCraft
      </MenuItemLink>
      <MenuItemLink href={ownerUrl(fullName)} target="_blank" rel="noreferrer" icon={<Person20Regular />}>
        Аккаунт владельца
      </MenuItemLink>
    </MenuList>
  );
}

export function RepoContextMenu({ url, fullName, children }: RepoMenuTarget & { children: ReactElement }) {
  return (
    <Menu openOnContext>
      <MenuTrigger disableButtonEnhancement>{children}</MenuTrigger>
      <MenuPopover>
        <RepoMenuList url={url} fullName={fullName} />
      </MenuPopover>
    </Menu>
  );
}

export function RepoActionsButton({ url, fullName }: RepoMenuTarget) {
  return (
    <Menu>
      <MenuTrigger disableButtonEnhancement>
        <Button appearance="subtle" icon={<MoreHorizontal24Regular />} aria-label="Действия с репозиторием" />
      </MenuTrigger>
      <MenuPopover>
        <RepoMenuList url={url} fullName={fullName} />
      </MenuPopover>
    </Menu>
  );
}
