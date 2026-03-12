'use client';

import {useState} from "react";
import {Card, CardBody, CardHeader} from "@heroui/card";
import {Input} from "@heroui/input";
import {Button} from "@heroui/button";
import {Divider} from "@heroui/divider";
import {addToast} from "@heroui/react";
import {signIn, signOut} from "next-auth/react";
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

            // Sign in with the new credentials so the session refreshes
            // with the real email and isAnonymous becomes false.
            const signInResult = await signIn('credentials', {
                email: email.trim(),
                password,
                redirect: false,
            });

            if (signInResult?.ok) {
                addToast({
                    title: 'Account registered!',
                    description: 'Your account has been upgraded successfully.',
                    color: 'success',
                });
                // Hard reload so the full layout re-renders with the updated session
                window.location.reload();
            } else {
                // Fallback: sign out and redirect to login if auto-sign-in fails
                addToast({
                    title: 'Account registered!',
                    description: 'Please sign in with your new email and password.',
                    color: 'success',
                });
                await signOut({redirectTo: `/login?callbackUrl=/profiles/${userId}`});
            }
        } catch {
            setError('An unexpected error occurred');
            setLoading(false);
        }
    }

    return (
        <Card className="border-2 border-warning/30">
            <CardHeader className="text-xl font-semibold flex items-center gap-2 px-5 pt-5">
                🎉 Complete Your Registration
            </CardHeader>
            <Divider/>
            <CardBody className="flex flex-col gap-4 px-5 pb-5">
                <p className="text-sm text-foreground-500">
                    You&apos;re using a guest account. Add an email and password to keep your
                    account permanently, or sign in with Google.
                </p>

                {/* Google sign-in option */}
                <GoogleSignInButton/>

                <div className="flex items-center gap-3">
                    <Divider className="flex-1"/>
                    <span className="text-sm text-foreground-400">or</span>
                    <Divider className="flex-1"/>
                </div>

                {/* Email + password form */}
                <div className="flex gap-3">
                    <Input
                        label="First name"
                        placeholder="John"
                        value={firstName}
                        onValueChange={setFirstName}
                    />
                    <Input
                        label="Last name"
                        placeholder="Doe"
                        value={lastName}
                        onValueChange={setLastName}
                    />
                </div>
                <Input
                    label="Email"
                    placeholder="you@example.com"
                    type="email"
                    value={email}
                    onValueChange={setEmail}
                    isRequired
                />
                <Input
                    label="Password"
                    placeholder="At least 8 characters"
                    type="password"
                    value={password}
                    onValueChange={setPassword}
                    isRequired
                />
                <Input
                    label="Confirm password"
                    type="password"
                    value={confirmPassword}
                    onValueChange={setConfirmPassword}
                    isRequired
                />
                {error && <p className="text-sm text-danger">{error}</p>}
                <Button
                    color="primary"
                    className="w-full"
                    isLoading={loading}
                    isDisabled={!email.trim() || !password}
                    onPress={handleUpgrade}
                >
                    Complete Registration
                </Button>
            </CardBody>
        </Card>
    );
}

