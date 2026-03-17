'use client'

import {useState} from "react";
import {Button} from "@heroui/button";
import {Input} from "@heroui/input";
import {Divider} from "@heroui/react";
import {useSearchParams, useRouter} from "next/navigation";
import {UserIcon} from "@heroicons/react/24/outline";
import {createGuestAndSignIn} from "@/lib/auth/create-guest";
import AvatarPicker from "@/components/AvatarPicker";
import GoogleSignInButton from "@/components/auth/GoogleSignInButton";
import {AVATAR_EYES, AVATAR_MOUTH} from "@/lib/avatar";

export default function AuthGatePage() {
    const searchParams = useSearchParams();
    const router = useRouter();
    const callbackUrl = searchParams.get("callbackUrl") || '/';
    const [name, setName] = useState('');
    const [avatarJson, setAvatarJson] = useState<string | null>(() => {
        const eyes = AVATAR_EYES[Math.floor(Math.random() * AVATAR_EYES.length)];
        const mouth = AVATAR_MOUTH[Math.floor(Math.random() * AVATAR_MOUTH.length)];
        return JSON.stringify({ eyes: [eyes], mouth: [mouth] });
    });
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    async function handleContinueAsGuest() {
        if (!name.trim()) return;
        setLoading(true);
        setError(null);

        try {
            const result = await createGuestAndSignIn(name, avatarJson ?? undefined);

            if (result.ok) {
                window.location.href = callbackUrl;
            } else {
                setError(result.error);
                setLoading(false);
            }
        } catch {
            setError('An unexpected error occurred.');
            setLoading(false);
        }
    }

    return (
        <div className='min-h-full bg-content1 flex items-center justify-center'>
            <div className='w-full max-w-md space-y-6 px-4 py-8'>
                <div className='text-center'>
                    <h1 className='text-4xl font-bold mb-2'>Welcome to Overflow</h1>
                    <p className='text-foreground-500'>
                        Sign in to your account or continue as a guest.
                    </p>
                </div>

                {/* Sign in card */}
                <div className="bg-content2 border border-content3 rounded-2xl shadow-raise-sm p-5 flex flex-col gap-3">
                    <h2 className="text-lg font-semibold text-foreground-700">Have an account?</h2>
                    <p className="text-sm text-foreground-500">
                        Sign in to access all features including asking questions,
                        voting, and managing your profile.
                    </p>
                    <GoogleSignInButton callbackUrl={callbackUrl} />
                    <Button color='primary' variant="flat" className="w-full"
                        onPress={() => router.push(`/login?callbackUrl=${encodeURIComponent(callbackUrl)}`)}>
                        Sign In with Email
                    </Button>
                    <Button variant="flat" className="w-full bg-content3"
                        onPress={() => router.push(`/signup?callbackUrl=${encodeURIComponent(callbackUrl)}`)}>
                        Create Account
                    </Button>
                </div>

                <div className="flex items-center gap-3">
                    <Divider className="flex-1"/>
                    <span className="text-sm text-foreground-400">or</span>
                    <Divider className="flex-1"/>
                </div>

                {/* Guest card */}
                <div className="bg-content2 border border-content3 rounded-2xl shadow-raise-sm overflow-hidden">
                    {/* Header */}
                    <div className="px-5 pt-5 pb-3">
                        <div className="flex items-center gap-2">
                            <UserIcon className="h-5 w-5 text-foreground-500"/>
                            <h2 className="text-lg font-semibold text-foreground-700">Create your profile</h2>
                        </div>
                        <p className="text-sm text-foreground-500 mt-1">
                            Choose a name and avatar to get started instantly. You can upgrade to a full account anytime.
                        </p>
                    </div>

                    <Divider />

                    {/* Display name */}
                    <div className="px-5 py-4">
                        <label className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-2 block">
                            Display name
                        </label>
                        <Input
                            placeholder="Bob"
                            value={name}
                            onValueChange={setName}
                            autoFocus
                            size="lg"
                            onKeyDown={(e) => e.key === 'Enter' && handleContinueAsGuest()}
                        />
                    </div>

                    <Divider />

                    {/* Avatar */}
                    <div className="px-5 py-4">
                        <label className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-3 block">
                            Your avatar
                        </label>
                        <AvatarPicker seed={name.trim() || 'guest'} value={avatarJson} onChange={setAvatarJson}>
                            {({avatarSrc, onOpen}) => (
                                <div className="flex flex-col items-center gap-2">
                                    <button type="button" onClick={onOpen} className="group relative">
                                        <img
                                            src={avatarSrc}
                                            alt="Avatar"
                                            className="h-20 w-20 rounded-full ring-2 ring-foreground-200 group-hover:ring-primary transition-all"
                                        />
                                        <span className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity text-white text-xs font-medium">
                                            Edit
                                        </span>
                                    </button>
                                    <button type="button" onClick={onOpen}
                                        className="text-xs text-primary hover:underline">
                                        Customize avatar
                                    </button>
                                </div>
                            )}
                        </AvatarPicker>
                    </div>

                    <Divider />

                    {/* Actions */}
                    <div className="px-5 py-4 flex flex-col gap-3">
                        {error && <p className="text-sm text-danger">{error}</p>}
                        <Button
                            color="primary"
                            size="lg"
                            className="w-full font-semibold"
                            isDisabled={!name.trim()}
                            isLoading={loading}
                            onPress={handleContinueAsGuest}
                        >
                            Continue as {name.trim() || 'Guest'}
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
}