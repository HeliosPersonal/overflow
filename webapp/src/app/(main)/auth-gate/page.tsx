'use client'

import {useState} from "react";
import {Button} from "@heroui/button";
import {Input} from "@heroui/input";
import {Card, CardBody, Divider} from "@heroui/react";
import {useSearchParams, useRouter} from "next/navigation";
import {UserIcon} from "@heroicons/react/24/outline";
import {createGuestAndSignIn} from "@/lib/auth/create-guest";

export default function AuthGatePage() {
    const searchParams = useSearchParams();
    const router = useRouter();
    const callbackUrl = searchParams.get("callbackUrl") || '/questions';
    const [name, setName] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    async function handleContinueAsGuest() {
        if (!name.trim()) return;
        setLoading(true);
        setError(null);

        try {
            const result = await createGuestAndSignIn(name);

            if (result.ok) {
                // Hard navigate so the full layout (TopNav) re-renders with the new session
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
        <div className='h-full flex items-center justify-center'>
            <div className='w-full max-w-md space-y-6 px-4'>
                <div className='text-center'>
                    <h1 className='text-4xl font-bold mb-2'>Welcome to Overflow</h1>
                    <p className='text-foreground-500'>
                        Sign in to your account or continue as a guest.
                    </p>
                </div>

                {/* Sign in option */}
                <Card shadow="sm">
                    <CardBody className="flex flex-col gap-3 p-5">
                        <h2 className="text-lg font-semibold">Have an account?</h2>
                        <p className="text-sm text-foreground-500">
                            Sign in to access all features including asking questions,
                            voting, and managing your profile.
                        </p>
                        <Button
                            color='primary'
                            className="w-full"
                            onPress={() => router.push(`/login?callbackUrl=${encodeURIComponent(callbackUrl)}`)}
                        >
                            Sign In
                        </Button>
                        <Button
                            variant="flat"
                            className="w-full"
                            onPress={() => router.push(`/signup?callbackUrl=${encodeURIComponent(callbackUrl)}`)}
                        >
                            Create Account
                        </Button>
                    </CardBody>
                </Card>

                <div className="flex items-center gap-3">
                    <Divider className="flex-1"/>
                    <span className="text-sm text-foreground-400">or</span>
                    <Divider className="flex-1"/>
                </div>

                {/* Guest option */}
                <Card shadow="sm">
                    <CardBody className="flex flex-col gap-3 p-5">
                        <div className="flex items-center gap-2">
                            <UserIcon className="h-5 w-5 text-foreground-500"/>
                            <h2 className="text-lg font-semibold">Continue as Guest</h2>
                        </div>
                        <p className="text-sm text-foreground-500">
                            Browse and participate with a display name.
                            You can upgrade to a full account anytime from your profile.
                        </p>
                        <Input
                            label="Display name"
                            placeholder="Bob"
                            value={name}
                            onValueChange={setName}
                            autoFocus
                            onKeyDown={(e) => e.key === 'Enter' && handleContinueAsGuest()}
                        />
                        {error && (
                            <p className="text-sm text-danger">{error}</p>
                        )}
                        <Button
                            variant="bordered"
                            className="w-full"
                            isDisabled={!name.trim()}
                            isLoading={loading}
                            onPress={handleContinueAsGuest}
                        >
                            Continue as {name.trim() || 'Guest'}
                        </Button>
                    </CardBody>
                </Card>
            </div>
        </div>
    );
}