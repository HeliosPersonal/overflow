'use client';

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@heroui/button';
import { Input } from '@heroui/input';
import { Card, CardBody, CardHeader } from '@heroui/card';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { loginSchema, type LoginFormData } from '@/lib/validators/auth';
import { signIn } from 'next-auth/react';
import Link from 'next/link';
import { Layers } from '@/components/animated-icons';
import GoogleSignInButton from '@/components/auth/GoogleSignInButton';
import { createClientLogger } from '@/lib/client-logger';

const log = createClientLogger('LoginPage');

export default function LoginPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const callbackUrl = searchParams.get('callbackUrl') || '/';
    const urlError = searchParams.get('error');
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(
        urlError === 'EMAIL_NOT_VERIFIED'
            ? 'Please verify your email before signing in. Check your inbox for the verification link.'
            : null
    );

    const {
        register,
        handleSubmit,
        formState: { errors },
    } = useForm<LoginFormData>({
        resolver: zodResolver(loginSchema),
    });

    const onSubmit = async (data: LoginFormData) => {
        log.debug('Login submit start', { email: data.email, callbackUrl });
        
        setIsLoading(true);
        setError(null);

        try {
            // eslint-disable-next-line react-hooks/purity
            const startTime = Date.now();
            
            const result = await signIn('credentials', {
                email: data.email,
                password: data.password,
                redirect: false,
            });
            
            // eslint-disable-next-line react-hooks/purity
            const duration = Date.now() - startTime;
            log.debug('signIn completed', { duration, ok: result?.ok, error: result?.error, status: result?.status });

            if (result?.error) {
                log.warn('Login failed', { error: result.error });
                setError('Invalid email or password.');
                setIsLoading(false);
                return;
            }

            if (result?.ok) {
                log.info('Login successful, redirecting', { callbackUrl });
                router.push(callbackUrl);
            } else {
                log.error('Login failed without specific error', { result });
                setError('Login failed. Please try again.');
                setIsLoading(false);
            }

        } catch (err) {
            log.error('Login exception', err instanceof Error ? err : String(err));
            setError('An unexpected error occurred.');
            setIsLoading(false);
        }
    };

    return (
        <div className="flex min-h-full items-center justify-center px-4 py-8">
            <div className="w-full max-w-md">
                {/* Logo Header */}
                <div className="mb-8 text-center">
                    <Link href="/" className="inline-flex items-center gap-3 mb-4">
                        <Layers size={48} className="text-primary" />
                        <h1 className="uppercase">Overflow</h1>
                    </Link>
                </div>

                <Card className="w-full">
                    <CardHeader className="flex flex-col gap-1 px-6 pt-6">
                        <h2>Welcome back</h2>
                        <p className="text-sm text-default-500">
                            Sign in to your account to continue
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

                        <Input
                            {...register('password')}
                            type="password"
                            label="Password"
                            placeholder="Enter your password"
                            variant="bordered"
                            isInvalid={!!errors.password}
                            errorMessage={errors.password?.message}
                            autoComplete="current-password"
                            isDisabled={isLoading}
                        />

                        <div className="flex items-center justify-between text-sm">
                            <Link
                                href="/forgot-password"
                                className="text-primary hover:underline"
                            >
                                Forgot password?
                            </Link>
                        </div>

                        <Button
                            type="submit"
                            color="primary"
                            isLoading={isLoading}
                            className="w-full"
                        >
                            {isLoading ? 'Signing in...' : 'Sign in'}
                        </Button>

                        <div className="relative my-2">
                            <div className="absolute inset-0 flex items-center">
                                <span className="w-full border-t border-divider" />
                            </div>
                            <div className="relative flex justify-center text-xs uppercase">
                                <span className="bg-background px-2 text-muted-foreground">
                                    Or
                                </span>
                            </div>
                        </div>

                        <GoogleSignInButton callbackUrl={callbackUrl} />

                        <div className="text-center text-sm">
                            Don&apos;t have an account?{' '}
                            <Link
                                href={`/signup?callbackUrl=${encodeURIComponent(callbackUrl)}`}
                                className="font-semibold text-primary hover:underline"
                            >
                                Sign up
                            </Link>
                        </div>
                    </form>
                </CardBody>
            </Card>
            </div>
        </div>
    );
}


