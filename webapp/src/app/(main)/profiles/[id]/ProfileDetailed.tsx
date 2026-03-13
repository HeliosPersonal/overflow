"use client";

import { Profile } from "@/lib/types";
import { Button } from "@heroui/button";
import { useState } from "react";
import EditProfileForm from "@/app/(main)/profiles/[id]/EditProfileForm";
import { Avatar } from "@heroui/avatar";
import { Divider } from "@heroui/divider";
import { Snippet } from "@heroui/snippet";
import { Session } from "next-auth";
import UpgradeAccountForm from "@/app/(main)/profiles/[id]/UpgradeAccountForm";

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
  const isAnonymous = session?.user?.isAnonymous;

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
            <Avatar className="h-16 w-16" color="primary" />
            <span className="text-3xl font-semibold">{profile.displayName}</span>
          </div>
          {currentUserProfile && (
            <Button onPress={() => setEditMode((prev) => !prev)} variant="bordered">
              {editMode ? "Cancel" : "Edit profile"}
            </Button>
          )}
        </div>
        <Divider />
        <div className="px-5 py-4">
          {editMode ? (
            <EditProfileForm profile={profile} setEditMode={setEditMode} />
          ) : (
            <p className="text-foreground-500">
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