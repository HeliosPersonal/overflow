"use client";

import { User } from "next-auth";
import {
  Dropdown,
  DropdownItem,
  DropdownMenu,
  DropdownSection,
  DropdownTrigger,
} from "@heroui/dropdown";
import DiceBearAvatar from "@/components/DiceBearAvatar";
import { Chip } from "@heroui/react";
import { signOut } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useCookieConsentStore } from "@/lib/hooks/useCookieConsentStore";
import { getActiveRoom } from "@/lib/hooks/useActiveRoom";

type Props = {
  user: User;
  /** Avatar URL fetched directly from ProfileService (source of truth). */
  avatarUrl?: string | null;
  /** Display name fetched directly from ProfileService (source of truth). */
  displayName: string;
};

type MenuItem = { key: string; label: string; href?: string; className?: string; onPress?: () => void };

export default function UserMenu({ user, avatarUrl, displayName }: Props) {
  const router = useRouter();
  const openPreferences = useCookieConsentStore((s) => s.openPreferences);
  const isAnonymous = user.isAnonymous;

  const profileItems: MenuItem[] = isAnonymous
    ? [{ key: "register", label: "Complete Registration", onPress: () => router.push(`/profiles/${user.id}`) }]
    : [
        { key: "profile", label: "My Profile", href: `/profiles/${user.id}` },
        ...(user.roles?.includes("admin")
          ? [{ key: "admin", label: "Admin Panel", href: "/admin", className: "text-primary" }]
          : []),
      ];

  async function handleSignOut() {
    // If the user is in a planning-poker room, leave it *before* the session
    // is destroyed — otherwise the proxy can't attach the Bearer token and the
    // backend resolves the wrong participant identity.
    const activeRoom = getActiveRoom();
    if (activeRoom) {
      await fetch(`/api/estimation/rooms/${activeRoom}/leave`, { method: "POST" }).catch(() => {});
    }
    await signOut({ redirectTo: "/" });
  }

  return (
    <Dropdown>
      <DropdownTrigger>
        <div className="flex items-center gap-2 cursor-pointer">
          <DiceBearAvatar
            userId={user.id}
            avatarJson={avatarUrl}
            borderClass={isAnonymous ? "border-2 border-default" : "border-2 border-primary"}
            size="md"
            name={displayName?.charAt(0)}
          />
          <span className="hidden sm:inline">{displayName}</span>
          {isAnonymous && (
            <Chip
              size="sm"
              variant="flat"
              color="warning"
              className="text-xs hidden sm:inline-flex"
            >
              Guest
            </Chip>
          )}
        </div>
      </DropdownTrigger>
      <DropdownMenu>
        <DropdownSection showDivider items={profileItems}>
          {(item) => (
            <DropdownItem
              key={item.key}
              href={item.href}
              className={item.className}
              onPress={item.onPress}
              {...(item.key === "register" ? { description: "Guest accounts expire after 30 days" } : {})}
            >
              {item.label}
            </DropdownItem>
          )}
        </DropdownSection>
        <DropdownSection>
          <DropdownItem
            key="cookie-settings"
            onPress={openPreferences}
          >
            Cookie Settings
          </DropdownItem>
          <DropdownItem
            key="logout"
            className="text-danger"
            color="danger"
            onPress={handleSignOut}
          >
            Sign out
          </DropdownItem>
        </DropdownSection>
      </DropdownMenu>
    </Dropdown>
  );
}