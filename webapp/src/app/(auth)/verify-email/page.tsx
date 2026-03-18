'use client';

import { Card, CardBody } from '@heroui/card';
import { Button } from '@heroui/button';
import { Spinner } from '@heroui/spinner';
import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Sparkles } from '@/components/animated-icons';
import { useSearchParams } from 'next/navigation';

export default function VerifyEmailPage() {
    const searchParams = useSearchParams();
    const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading');
    const [error, setError] = useState<string | null>(null);

    const token = searchParams.get('token');
    const email = searchParams.get('email');

    useEffect(() => {
        if (!token || !email) {
            setStatus('error');
            setError('Invalid verification link.');
            return;
        }

        fetch('/api/auth/verify-email', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ token, email }),
        })
            .then(async (res) => {
                if (res.ok) {
                    setStatus('success');
                } else {
                    const body = await res.json().catch(() => null);
                    setError(body?.error || 'Verification failed. The link may have expired.');
                    setStatus('error');
                }
            })
            .catch(() => {
                setError('An unexpected error occurred.');
                setStatus('error');
            });
    }, [token, email]);

    return (
        <div className="flex min-h-screen items-center justify-center px-4 py-12">
            <div className="w-full max-w-md">
                <div className="mb-8 text-center">
                    <Link href="/" className="inline-flex items-center gap-3 mb-4">
                        <Sparkles size={48} className="text-primary" />
                        <h1 className="uppercase">Overflow</h1>
                    </Link>
                </div>

                <Card className="w-full">
                    <CardBody className="px-6 py-8 text-center">
                        {status === 'loading' && (
                            <>
                                <Spinner className="mx-auto mb-4" size="lg" />
                                <h2 className="mb-2">Verifying your email…</h2>
                                <p className="text-sm text-default-500">
                                    Please wait while we confirm your address.
                                </p>
                            </>
                        )}

                        {status === 'success' && (
                            <>
                                <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-success-100">
                                    <svg
                                        className="h-6 w-6 text-success"
                                        fill="none"
                                        strokeLinecap="round"
                                        strokeLinejoin="round"
                                        strokeWidth="2"
                                        viewBox="0 0 24 24"
                                        stroke="currentColor"
                                    >
                                        <path d="M5 13l4 4L19 7" />
                                    </svg>
                                </div>
                                <h2 className="mb-2">Email Verified!</h2>
                                <p className="mb-4 text-sm text-default-500">
                                    Your email has been verified. You can now sign in with your new credentials.
                                </p>
                                <Link href="/login">
                                    <Button color="primary" className="w-full">
                                        Sign in
                                    </Button>
                                </Link>
                            </>
                        )}

                        {status === 'error' && (
                            <>
                                <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-danger-100">
                                    <svg
                                        className="h-6 w-6 text-danger"
                                        fill="none"
                                        strokeLinecap="round"
                                        strokeLinejoin="round"
                                        strokeWidth="2"
                                        viewBox="0 0 24 24"
                                        stroke="currentColor"
                                    >
                                        <path d="M6 18L18 6M6 6l12 12" />
                                    </svg>
                                </div>
                                <h2 className="mb-2">Verification Failed</h2>
                                <p className="mb-4 text-sm text-default-500">
                                    {error}
                                </p>
                                <Link href="/login">
                                    <Button color="primary" variant="flat" className="w-full">
                                        Go to login
                                    </Button>
                                </Link>
                            </>
                        )}
                    </CardBody>
                </Card>
            </div>
        </div>
    );
}

