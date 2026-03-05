'use client';

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@heroui/button';
import { Input } from '@heroui/input';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { signupSchema, type SignupFormData } from '@/lib/validators/auth';
import Link from 'next/link';
import { AcademicCapIcon } from '@heroicons/react/24/outline';
import GoogleSignInButton from '@/components/auth/GoogleSignInButton';

export default function SignupPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const callbackUrl = searchParams.get('callbackUrl') || '/';
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState(false);

    const {
        register,
        handleSubmit,
        formState: { errors },
    } = useForm<SignupFormData>({
        resolver: zodResolver(signupSchema),
    });

    const onSubmit = async (data: SignupFormData) => {
        setIsLoading(true);
        setError(null);

        try {
            const response = await fetch('/api/auth/signup', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(data),
            });

            const result = await response.json();

            if (!response.ok) {
                console.error('Signup failed:', result.error);
                setError(result.error || 'Signup failed. Please try again.');
                setIsLoading(false);
                return;
            }

            setSuccess(true);
            
            setTimeout(() => {
                router.push(`/login?callbackUrl=${encodeURIComponent(callbackUrl)}`);
            }, 2000);

        } catch (err) {
            console.error('Signup error:', err);
            setError('An unexpected error occurred. Please try again.');
            setIsLoading(false);
        }
    };

    if (success) {
        return (
            <div className="flex min-h-screen items-center justify-center px-4 py-12">
                <div className="w-full max-w-md">
                    {/* Logo Header */}
                    <div className="mb-8 text-center">
                        <Link href="/" className="inline-flex items-center gap-3 mb-4">
                            <AcademicCapIcon className="size-12 text-primary" />
                            <h1 className="uppercase">Overflow</h1>
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
                        <h2 className="mb-2 text-xl font-bold">Account created successfully!</h2>
                        <p className="text-sm text-default-500">
                            Redirecting to login page...
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
                {/* Logo Header */}
                <div className="mb-8 text-center">
                    <Link href="/" className="inline-flex items-center gap-3 mb-4">
                        <AcademicCapIcon className="size-12 text-primary" />
                        <h1 className="uppercase">Overflow</h1>
                    </Link>
                </div>

                <Card className="w-full">
                    <CardHeader className="flex flex-col gap-1 px-6 pt-6">
                        <h2>Create an account</h2>
                        <p className="text-sm text-default-500">
                            Join our community and start asking questions
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
                            {...register('email')}
                            type="email"
                            label="Email"
                            placeholder="Enter your email"
                            variant="bordered"
                            isInvalid={!!errors.email}
                            errorMessage={errors.email?.message}
                            autoComplete="email"
                            isDisabled={isLoading}
                        />

                        <div className="grid grid-cols-2 gap-4">
                            <Input
                                {...register('firstName')}
                                type="text"
                                label="First Name"
                                placeholder="First name"
                                variant="bordered"
                                isInvalid={!!errors.firstName}
                                errorMessage={errors.firstName?.message}
                                autoComplete="given-name"
                                isDisabled={isLoading}
                            />

                            <Input
                                {...register('lastName')}
                                type="text"
                                label="Last Name"
                                placeholder="Last name"
                                variant="bordered"
                                isInvalid={!!errors.lastName}
                                errorMessage={errors.lastName?.message}
                                autoComplete="family-name"
                                isDisabled={isLoading}
                            />
                        </div>

                        <Input
                            {...register('password')}
                            type="password"
                            label="Password"
                            placeholder="Create a password"
                            variant="bordered"
                            isInvalid={!!errors.password}
                            errorMessage={errors.password?.message}
                            autoComplete="new-password"
                            isDisabled={isLoading}
                        />

                        <Input
                            {...register('confirmPassword')}
                            type="password"
                            label="Confirm Password"
                            placeholder="Confirm your password"
                            variant="bordered"
                            isInvalid={!!errors.confirmPassword}
                            errorMessage={errors.confirmPassword?.message}
                            autoComplete="new-password"
                            isDisabled={isLoading}
                        />

                        <Button
                            type="submit"
                            color="primary"
                            isLoading={isLoading}
                            className="w-full"
                        >
                            {isLoading ? 'Creating account...' : 'Create account'}
                        </Button>

                        <div className="relative my-2">
                            <div className="absolute inset-0 flex items-center">
                                <span className="w-full border-t border-neutral-200 dark:border-neutral-800" />
                            </div>
                            <div className="relative flex justify-center text-xs uppercase">
                                <span className="bg-background px-2 text-muted-foreground">
                                    Or
                                </span>
                            </div>
                        </div>

                        <GoogleSignInButton
                            callbackUrl={callbackUrl}
                            label="Sign up with Google"
                        />

                        <div className="text-center text-sm">
                            Already have an account?{' '}
                            <Link
                                href={`/login?callbackUrl=${encodeURIComponent(callbackUrl)}`}
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

