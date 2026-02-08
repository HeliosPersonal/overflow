'use client';

import { Card, CardBody, CardHeader } from '@heroui/card';
import { Button } from '@heroui/button';
import { Input } from '@heroui/input';
import { useState } from 'react';
import Link from 'next/link';
import { AcademicCapIcon } from '@heroicons/react/24/solid';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

const resetPasswordSchema = z.object({
    password: z.string()
        .min(8, 'Password must be at least 8 characters')
        .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
        .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
        .regex(/[0-9]/, 'Password must contain at least one number'),
    confirmPassword: z.string(),
}).refine((data) => data.password === data.confirmPassword, {
    message: "Passwords don't match",
    path: ['confirmPassword'],
});

type ResetPasswordFormData = z.infer<typeof resetPasswordSchema>;

export default function ResetPasswordPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState(false);

    const token = searchParams.get('token');
    const email = searchParams.get('email');
    const tokenValid = !!(token && email);

    const {
        register,
        handleSubmit,
        formState: { errors },
    } = useForm<ResetPasswordFormData>({
        resolver: zodResolver(resetPasswordSchema),
    });


    const onSubmit = async (data: ResetPasswordFormData) => {
        if (!token || !email) {
            setError('Invalid reset link');
            return;
        }

        setIsLoading(true);
        setError(null);

        try {
            const response = await fetch('/api/auth/reset-password', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    token,
                    email,
                    password: data.password,
                }),
            });

            const result = await response.json();

            if (!response.ok) {
                setError(result.error || 'Failed to reset password. Please try again.');
                setIsLoading(false);
                return;
            }

            setSuccess(true);
            
            setTimeout(() => {
                router.push('/login');
            }, 3000);

        } catch (err) {
            console.error('Reset password error:', err);
            setError('An unexpected error occurred. Please try again.');
            setIsLoading(false);
        }
    };

    if (!tokenValid) {
        return (
            <div className="flex min-h-screen items-center justify-center px-4 py-12">
                <div className="w-full max-w-md">
                    <div className="mb-8 text-center">
                        <Link href="/" className="inline-flex items-center gap-3 mb-4">
                            <AcademicCapIcon className="size-12 text-secondary" />
                            <h1 className="text-3xl font-bold uppercase">Overflow</h1>
                        </Link>
                    </div>

                    <Card className="w-full">
                        <CardBody className="px-6 py-8 text-center">
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
                                    <path d="M6 18L18 6M6 6l12 12"></path>
                                </svg>
                            </div>
                            <h2 className="mb-2 text-xl font-bold">Invalid Reset Link</h2>
                            <p className="mb-4 text-sm text-default-500">
                                This password reset link is invalid or has expired.
                            </p>
                            <Link href="/forgot-password">
                                <Button color="primary" className="w-full">
                                    Request new reset link
                                </Button>
                            </Link>
                        </CardBody>
                    </Card>
                </div>
            </div>
        );
    }

    if (success) {
        return (
            <div className="flex min-h-screen items-center justify-center px-4 py-12">
                <div className="w-full max-w-md">
                    <div className="mb-8 text-center">
                        <Link href="/" className="inline-flex items-center gap-3 mb-4">
                            <AcademicCapIcon className="size-12 text-secondary" />
                            <h1 className="text-3xl font-bold uppercase">Overflow</h1>
                        </Link>
                    </div>

                    <Card className="w-full">
                        <CardBody className="px-6 py-8 text-center">
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
                                    <path d="M5 13l4 4L19 7"></path>
                                </svg>
                            </div>
                            <h2 className="mb-2 text-xl font-bold">Password Reset Successful</h2>
                            <p className="mb-4 text-sm text-default-500">
                                Your password has been reset successfully. Redirecting to login...
                            </p>
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
                        <AcademicCapIcon className="size-12 text-secondary" />
                        <h1 className="text-3xl font-bold uppercase">Overflow</h1>
                    </Link>
                </div>

                <Card className="w-full">
                    <CardHeader className="flex flex-col gap-1 px-6 pt-6">
                        <h2 className="text-2xl font-bold">Set new password</h2>
                        <p className="text-sm text-default-500">
                            Enter your new password below
                        </p>
                    </CardHeader>
                    <CardBody className="px-6 pb-6">
                        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
                            {error && (
                                <div className="rounded-lg bg-danger-50 p-3 text-sm text-danger">
                                    {error}
                                </div>
                            )}

                            <Input
                                {...register('password')}
                                type="password"
                                label="New Password"
                                placeholder="Enter new password"
                                variant="bordered"
                                isInvalid={!!errors.password}
                                errorMessage={errors.password?.message}
                                isDisabled={isLoading}
                                autoComplete="new-password"
                            />

                            <Input
                                {...register('confirmPassword')}
                                type="password"
                                label="Confirm Password"
                                placeholder="Confirm new password"
                                variant="bordered"
                                isInvalid={!!errors.confirmPassword}
                                errorMessage={errors.confirmPassword?.message}
                                isDisabled={isLoading}
                                autoComplete="new-password"
                            />

                            <Button
                                type="submit"
                                color="primary"
                                className="w-full"
                                isLoading={isLoading}
                            >
                                {isLoading ? 'Resetting password...' : 'Reset password'}
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

