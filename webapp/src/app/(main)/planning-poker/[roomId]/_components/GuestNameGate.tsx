'use client';

import {Button, Divider, Input} from "@heroui/react";
import AvatarPicker from "@/components/AvatarPicker";
import GoogleSignInButton from "@/components/auth/GoogleSignInButton";
import {FULL_PAGE_CENTER, SECTION_LABEL} from "./room-constants";

export default function GuestNameGate({roomId, guestName, onGuestNameChange, avatarJson, onAvatarChange, joinLoading, onJoin, onNavigateToLogin}: {
    roomId: string;
    guestName: string;
    onGuestNameChange: (name: string) => void;
    avatarJson: string | null;
    onAvatarChange: (json: string | null) => void;
    joinLoading: boolean;
    onJoin: () => void;
    onNavigateToLogin: () => void;
}) {
    return (
        <div className={FULL_PAGE_CENTER}>
            <div className="max-w-md w-full px-4 py-16 flex flex-col gap-4">
                <div className="bg-content2 border border-content3 shadow-raise-sm rounded-2xl overflow-hidden">
                    <div className="px-6 pt-6 pb-4">
                        <h2 className="text-xl font-semibold">Set up your profile</h2>
                        <p className="text-sm text-foreground-500 mt-1">Choose a name and avatar to join this room.</p>
                    </div>
                    <Divider/>

                    <div className="px-6 py-5">
                        <label className={`${SECTION_LABEL} mb-2 block`}>Display name</label>
                        <Input placeholder="Jane" value={guestName} onValueChange={onGuestNameChange} autoFocus
                               size="lg" onKeyDown={(e) => e.key === 'Enter' && onJoin()}/>
                    </div>
                    <Divider/>

                    <div className="px-6 py-5">
                        <label className={`${SECTION_LABEL} mb-3 block`}>Your avatar</label>
                        <AvatarPicker seed={guestName.trim() || 'guest'} value={avatarJson} onChange={onAvatarChange}>
                            {({avatarSrc, onOpen}) => (
                                <div className="flex flex-col items-center gap-2">
                                    <button type="button" onClick={onOpen} className="group relative">
                                        <img src={avatarSrc} alt="Avatar"
                                             className="h-20 w-20 rounded-full border-2 border-foreground-200 group-hover:border-primary transition-all"/>
                                        <span className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity text-white text-xs font-medium">
                                            Edit
                                        </span>
                                    </button>
                                    <button type="button" onClick={onOpen}
                                            className="text-xs text-primary hover:underline">Customize avatar</button>
                                </div>
                            )}
                        </AvatarPicker>
                    </div>
                    <Divider/>

                    <div className="px-6 py-5 flex flex-col gap-3">
                        <Button color="primary" size="lg" isLoading={joinLoading} onPress={onJoin}
                                isDisabled={!guestName.trim()} className="w-full font-semibold">Join Room</Button>
                        <div className="flex items-center gap-3 my-1">
                            <Divider className="flex-1"/>
                            <span className="text-xs text-foreground-400">or sign in</span>
                            <Divider className="flex-1"/>
                        </div>
                        <GoogleSignInButton callbackUrl={`/planning-poker/${roomId}`}
                                            label="Continue with Google"/>
                        <Button variant="flat" className="w-full bg-content3" onPress={onNavigateToLogin}>
                            Sign In with Email
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
}

