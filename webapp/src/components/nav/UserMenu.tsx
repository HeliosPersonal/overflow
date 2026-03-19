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

export default function UserMenu({ user, avatarUrl, displayName }: Props) {
  const router = useRouter();
  const openPreferences = useCookieConsentStore((s) => s.openPreferences);
  const isAnonymous = user.isAnonymous;

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
            color={isAnonymous ? "default" : "primary"}
            size="md"
            name={displayName?.charAt(0)}
          />
          {displayName}
          {isAnonymous && (
            <Chip
              size="sm"
              variant="flat"
              color="warning"
              className="text-xs"
            >
              Guest
            </Chip>
          )}
        </div>
      </DropdownTrigger>
      <DropdownMenu>
        {isAnonymous ? (
          <DropdownSection showDivider>
            <DropdownItem
              key="register"
              description="Add email & password to keep your account"
              onPress={() => router.push(`/profiles/${user.id}`)}
            >
              Complete Registration
            </DropdownItem>
          </DropdownSection>
        ) : (
          <DropdownSection showDivider>
            <DropdownItem href={`/profiles/${user.id}`} key="profile">
              My Profile
            </DropdownItem>
          </DropdownSection>
        )}
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