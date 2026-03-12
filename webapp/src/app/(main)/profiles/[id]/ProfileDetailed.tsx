'use client';

import {Card, CardBody, CardFooter, CardHeader} from "@heroui/card";
import {Profile} from "@/lib/types";
import {Button} from "@heroui/button";
import {useState} from "react";
import EditProfileForm from "@/app/(main)/profiles/[id]/EditProfileForm";
import {Avatar} from "@heroui/avatar";
import {Divider} from "@heroui/divider";
import {Snippet} from "@heroui/snippet";
import {Session} from "next-auth";
import ErrorButtons from "@/app/(main)/profiles/[id]/ErrorButtons";
import AuthTestButton from "@/app/(main)/profiles/[id]/AuthTestButton";
import UpgradeAccountForm from "@/app/(main)/profiles/[id]/UpgradeAccountForm";

type Props = {
    profile: Profile
    currentUserProfile: boolean
    session?: Session | null
}

export default function ProfileDetailed({profile, currentUserProfile, session}: Props) {
    const [editMode, setEditMode] = useState(false);
    const isAnonymous = session?.user?.isAnonymous;

    return (
        <div className='flex flex-col gap-4 pt-4'>
            {/* Upgrade banner for anonymous users */}
            {currentUserProfile && isAnonymous && (
                <UpgradeAccountForm userId={profile.userId} />
            )}

            <Card>
                <CardHeader className='text-3xl font-semibold flex justify-between'>
                    <div className='flex items-center gap-3'>
                        <Avatar className='h-30 w-30' color='primary' />
                        <span>{profile.displayName}</span>
                    </div>
                    {currentUserProfile &&
                        <Button
                            onPress={() => setEditMode(prev => !prev)}
                            variant='bordered'
                        >
                            {editMode ? 'Cancel' : 'Edit profile'}
                        </Button>}
                </CardHeader>
                <Divider />
                <CardBody>
                    {editMode ? (
                        <EditProfileForm profile={profile} setEditMode={setEditMode} />
                    ) : (
                        <p>{profile?.description || 'No profile description added yet'}</p>
                    )}
                </CardBody>
                <CardFooter className='text-xl font-semibold'>
                    Reputation score: {profile.reputation}
                </CardFooter>
            </Card>

            {currentUserProfile && session && (
                <Card>
                    <CardHeader className='text-xl font-semibold'>
                        Session
                    </CardHeader>
                    <Divider />
                    <CardBody className='flex flex-col gap-4'>
                        <Snippet
                            symbol=''
                            classNames={{
                                base: 'w-full',
                                pre: 'text-wrap whitespace-pre-wrap break-all'
                            }}
                        >
                            {JSON.stringify(session, null, 2)}
                        </Snippet>
                        <div className='flex items-center gap-3 flex-wrap'>
                            <ErrorButtons />
                            <AuthTestButton />
                        </div>
                    </CardBody>
                </Card>
            )}
        </div>
    );
}