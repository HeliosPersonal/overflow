"use client";

import { Profile } from "@/lib/types";
import { Button } from "@heroui/button";
import { useState, useTransition } from "react";
import EditProfileForm from "@/app/(main)/profiles/[id]/EditProfileForm";
import { Divider } from "@heroui/divider";
import { Snippet } from "@heroui/snippet";
import { Session } from "next-auth";
import UpgradeAccountForm from "@/app/(main)/profiles/[id]/UpgradeAccountForm";
import DiceBearAvatar from "@/components/DiceBearAvatar";
import AvatarPicker from "@/components/AvatarPicker";
import { editProfile } from "@/lib/actions/profile-actions";
import { handleError, successToast } from "@/lib/util";
import { useRouter } from "next/navigation";

type Props = {
  profile: Profile;
  currentUserProfile: boolean;
  session?: Session | null;
};

export default function ProfileDetailed({
  profile,
  currentUserProfile,
  session,
}: Props) {
  const [editMode, setEditMode] = useState(false);
  const [avatarSaving, startAvatarSave] = useTransition();
  const router = useRouter();
  const isAnonymous = session?.user?.isAnonymous;

  function handleAvatarChange(json: string) {
    startAvatarSave(async () => {
      const { error } = await editProfile(profile.userId, {
        displayName: profile.displayName,
        description: profile.description ?? "",
        avatarUrl: json,
      });
      if (error) {
        handleError(error);
        return;
      }
      successToast("Avatar updated!");
      // editProfile calls revalidatePath('/', 'layout') which busts the Next.js cache.
      // router.refresh() re-renders TopNav which fetches fresh data from ProfileService.
      router.refresh();
    });
  }

  return (
    <div className="flex flex-col gap-4 pt-4">
      {/* Upgrade banner for anonymous users */}
      {currentUserProfile && isAnonymous && (
        <UpgradeAccountForm userId={profile.userId} />
      )}

      {/* Profile card */}
      <div className="bg-content2 border border-content3 shadow-raise-sm rounded-2xl">
        <div className="flex justify-between items-center px-5 pt-5 pb-4">
          <div className="flex items-center gap-3">
            {currentUserProfile ? (
              <AvatarPicker
                seed={profile.userId}
                value={profile.avatarUrl}
                onChange={handleAvatarChange}
              >
                {({ avatarSrc, onOpen }) => (
                  <button
                    type="button"
                    onClick={onOpen}
                    disabled={avatarSaving}
                    className="group relative shrink-0"
                  >
                    <img
                      src={avatarSrc}
                      alt="Avatar"
                      className="h-16 w-16 rounded-full ring-2 ring-primary/40 group-hover:ring-primary transition-all"
                    />
                    <span className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity text-white text-xs font-medium">
                      {avatarSaving ? "…" : "Edit"}
                    </span>
                  </button>
                )}
              </AvatarPicker>
            ) : (
              <DiceBearAvatar
                className="h-16 w-16"
                color="primary"
                userId={profile.userId}
                avatarJson={profile.avatarUrl}
                name={profile.displayName?.charAt(0)}
              />
            )}
            <span className="text-4xl font-bold">{profile.displayName}</span>
          </div>
          {currentUserProfile && (
            <Button onPress={() => setEditMode((prev) => !prev)} variant="bordered">
              {editMode ? "Cancel" : "Edit profile"}
            </Button>
          )}
        </div>
        <Divider />
        <div className="px-5 py-5">
          {editMode ? (
            <EditProfileForm profile={profile} setEditMode={setEditMode} />
          ) : (
            <p className="text-lg text-foreground-500">
              {profile?.description || "No profile description added yet"}
            </p>
          )}
        </div>
        <Divider />
        <div className="px-5 py-4 text-foreground-500 font-semibold">
          Reputation score:{" "}
          <span className="text-foreground-800">{profile.reputation}</span>
        </div>
      </div>

      {/* Session card */}
      {currentUserProfile && session && (
        <div className="bg-content2 border border-content3 shadow-raise-sm rounded-2xl">
          <div className="px-5 pt-5 pb-4 text-xl font-semibold">Session</div>
          <Divider />
          <div className="px-5 py-4">
            <Snippet
              symbol=""
              classNames={{
                base: "w-full",
                pre: "text-wrap whitespace-pre-wrap break-all",
              }}
            >
              {JSON.stringify(session, null, 2)}
            </Snippet>
          </div>
        </div>
      )}
    </div>
  );
}