'use client';

import { Card, CardBody, CardHeader } from '@heroui/card';
import { Button } from '@heroui/button';
import { Input } from '@heroui/input';
import { useState } from 'react';
import Link from 'next/link';
import { Layers } from '@/components/animated-icons';

export default function ForgotPasswordPage() {
    const [email, setEmail] = useState('');
    const [isSubmitted, setIsSubmitted] = useState(false);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setIsLoading(true);
        setError(null);

        try {
            const response = await fetch('/api/auth/forgot-password', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ email }),
            });

            const result = await response.json();

            if (!response.ok) {
                setError(result.error || 'Failed to send reset email. Please try again.');
                setIsLoading(false);
                return;
            }

            setIsSubmitted(true);
        } catch (err) {
            console.error('Forgot password error:', err);
            setError('An unexpected error occurred. Please try again.');
            setIsLoading(false);
        }
    };

    if (isSubmitted) {
        return (
            <div className="flex min-h-screen items-center justify-center px-4 py-12">
                <div className="w-full max-w-md">
                    <div className="mb-8 text-center">
                        <Link href="/" className="inline-flex items-center gap-3 mb-4">
                            <Layers size={48} className="text-primary" />
                            <h1 className="uppercase">Overflow</h1>
                        </Link>
                    </div>

                    <Card className="w-full">
                        <CardBody className="px-6 py-8 text-center">
                            <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-primary-100">
                                <svg
                                    className="h-6 w-6 text-primary"
                                    fill="none"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                    strokeWidth="2"
                                    viewBox="0 0 24 24"
                                    stroke="currentColor"
                                >
                                    <path d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"></path>
                                </svg>
                            </div>
                            <h2 className="mb-2 text-xl font-bold">Check your email</h2>
                            <p className="mb-4 text-sm text-default-500">
                                If an account exists for {email}, you will receive a password reset link shortly.
                            </p>
                            <Link href="/login">
                                <Button color="primary" className="w-full">
                                    Back to login
                                </Button>
                            </Link>
                        </CardBody>
                    </Card>
                </div>
            </div>
        );
    }

    return (
        <div className="flex min-h-screen items-center justify-center px-4 py-12">
            <div className="w-full max-w-md">
                <div className="mb-8 text-center">
                    <Link href="/" className="inline-flex items-center gap-3 mb-4">
                        <Layers size={48} className="text-primary" />
                        <h1 className="uppercase">Overflow</h1>
                    </Link>
                </div>

                <Card className="w-full">
                    <CardHeader className="flex flex-col gap-1 px-6 pt-6">
                        <h2>Reset your password</h2>
                        <p className="text-sm text-default-500">
                            Enter your email address and we&apos;ll send you a link to reset your password
                        </p>
                    </CardHeader>
                    <CardBody className="px-6 pb-6">
                        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
                            {error && (
                                <div className="rounded-lg bg-danger-50 p-3 text-sm text-danger">
                                    {error}
                                </div>
                            )}

                            <Input
                                type="email"
                                label="Email"
                                placeholder="Enter your email"
                                variant="bordered"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                isDisabled={isLoading}
                                required
                            />

                            <Button
                                type="submit"
                                color="primary"
                                className="w-full"
                                isLoading={isLoading}
                            >
                                {isLoading ? 'Sending...' : 'Send reset link'}
                            </Button>

                            <div className="text-center text-sm">
                                Remember your password?{' '}
                                <Link
                                    href="/login"
                                    className="font-semibold text-primary hover:underline"
                                >
                                    Sign in
                                </Link>
                            </div>
                        </form>
                    </CardBody>
                </Card>
            </div>
        </div>
    );
}

