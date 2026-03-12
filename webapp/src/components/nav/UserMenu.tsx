"use client";

import { User } from "next-auth";
import {
  Dropdown,
  DropdownItem,
  DropdownMenu,
  DropdownSection,
  DropdownTrigger,
} from "@heroui/dropdown";
import { Avatar } from "@heroui/avatar";
import { Chip } from "@heroui/react";
import { signOut } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useCookieConsentStore } from "@/lib/hooks/useCookieConsentStore";

type Props = {
  user: User;
};

export default function UserMenu({ user }: Props) {
  const router = useRouter();
  const openPreferences = useCookieConsentStore((s) => s.openPreferences);
  const isAnonymous = user.isAnonymous;

  return (
    <Dropdown>
      <DropdownTrigger>
        <div className="flex items-center gap-2 cursor-pointer">
          <Avatar
            suppressHydrationWarning
            color={isAnonymous ? "default" : "primary"}
            size="sm"
            name={user.displayName?.charAt(0)}
          />
          {user.displayName}
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
            onPress={() => signOut({ redirectTo: "/" })}
          >
            Sign out
          </DropdownItem>
        </DropdownSection>
      </DropdownMenu>
    </Dropdown>
  );
}