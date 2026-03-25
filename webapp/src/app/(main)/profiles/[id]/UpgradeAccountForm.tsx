'use client';

import {useState} from "react";
import {Input} from "@heroui/input";
import {Button} from "@heroui/button";
import {Divider} from "@heroui/divider";
import {addToast} from "@heroui/react";
import {signOut} from "next-auth/react";
import GoogleSignInButton from "@/components/auth/GoogleSignInButton";

type Props = {
    userId: string;
}

export default function UpgradeAccountForm({userId}: Props) {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [verificationSent, setVerificationSent] = useState(false);

    async function handleUpgrade() {
        setError(null);

        if (!email.trim() || !password) {
            setError('Email and password are required');
            return;
        }
        if (password !== confirmPassword) {
            setError('Passwords do not match');
            return;
        }
        if (password.length < 8) {
            setError('Password must be at least 8 characters');
            return;
        }

        setLoading(true);
        try {
            const res = await fetch('/api/auth/upgrade', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({
                    email: email.trim(),
                    password,
                    firstName: firstName.trim() || undefined,
                    lastName: lastName.trim() || undefined,
                }),
            });

            if (!res.ok) {
                const err = await res.json().catch(() => null);
                setError(err?.error || 'Failed to upgrade account');
                setLoading(false);
                return;
            }

            // Show verification message
            setVerificationSent(true);

            addToast({
                title: 'Check your email!',
                description: `We've sent a verification link to ${email.trim()}.`,
                color: 'success',
            });

            // Sign out the guest session — user must verify email then sign in with new creds
            setTimeout(async () => {
                await signOut({redirectTo: '/login'});
            }, 3000);
        } catch {
            setError('An unexpected error occurred');
            setLoading(false);
        }
    }

    if (verificationSent) {
        return (
            <div className="bg-content2 border border-content3 shadow-raise-sm rounded-2xl p-5 flex flex-col gap-4 text-center">
                <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-success-100">
                    <svg className="h-7 w-7 text-success" fill="none" strokeLinecap="round" strokeLinejoin="round"
                         strokeWidth="2" viewBox="0 0 24 24" stroke="currentColor">
                        <path d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                    </svg>
                </div>
                <h2 className="text-xl font-semibold">Check your email</h2>
                <p className="text-sm text-foreground-500">
                    We&apos;ve sent a verification link to <strong>{email}</strong>.
                    <br />
                    Click the link to verify your email, then sign in with your new credentials.
                </p>
                <p className="text-xs text-foreground-400">
                    Redirecting to login…
                </p>
            </div>
        );
    }

    return (
        <div className="bg-content2 border border-content3 shadow-raise-sm rounded-2xl p-5 flex flex-col gap-4">
            <h2 className="text-xl font-semibold flex items-center gap-2">
                🎉 Complete Your Registration
            </h2>
            <Divider/>
            <p className="text-sm text-foreground-500">
                You&apos;re using a guest account. Add an email and password to keep your
                account permanently, or sign in with Google.
            </p>
            <p className="text-xs text-warning-600 bg-warning-50 rounded-lg px-3 py-2">
                ⚠️ Guest accounts are automatically deleted after 30 days of inactivity.
                Complete registration to keep your data.
            </p>
            <GoogleSignInButton/>
            <div className="flex items-center gap-3">
                <Divider className="flex-1"/>
                <span className="text-sm text-foreground-400">or</span>
                <Divider className="flex-1"/>
            </div>
            <div className="flex gap-3">
                <Input label="First name" placeholder="John" value={firstName} onValueChange={setFirstName}/>
                <Input label="Last name" placeholder="Doe" value={lastName} onValueChange={setLastName}/>
            </div>
            <Input label="Email" placeholder="you@example.com" type="email" value={email} onValueChange={setEmail} isRequired/>
            <Input label="Password" placeholder="At least 8 characters" type="password" value={password} onValueChange={setPassword} isRequired/>
            <Input label="Confirm password" type="password" value={confirmPassword} onValueChange={setConfirmPassword} isRequired/>
            {error && <p className="text-sm text-danger">{error}</p>}
            <Button color="primary" className="w-full" isLoading={loading}
                isDisabled={!email.trim() || !password} onPress={handleUpgrade}>
                Complete Registration
            </Button>
        </div>
    );
}
