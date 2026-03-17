import Link from "next/link";
import {SparklesIcon} from "@heroicons/react/24/solid";
import ThemeToggle from "@/components/nav/ThemeToggle";
import LoginButton from "@/components/nav/LoginButton";
import {getCurrentUser} from "@/lib/actions/auth-actions";
import {fetchClient} from "@/lib/fetchClient";
import {Profile} from "@/lib/types";
import UserMenu from "@/components/nav/UserMenu";
import RegisterButton from "@/components/nav/RegisterButton";

export default async function TopNav() {
    const user = await getCurrentUser();

    // Fetch profile directly from ProfileService (source of truth for display name + avatar)
    // so it's always fresh — no dependency on the JWT session token for mutable profile data.
    let avatarUrl: string | null = null;
    let displayName: string | null = null;
    if (user) {
        const {data: profile} = await fetchClient<Profile>('/profiles/me', 'GET');
        avatarUrl = profile?.avatarUrl ?? null;
        displayName = profile?.displayName ?? null;
    }

    return (
        <header className='p-2 w-full fixed top-0 z-50 bg-content1 shadow-raise-sm'>
            <div className='flex px-10 mx-auto'>
                <div className='flex items-center gap-6'>
                    <Link href='/' className='flex items-center gap-3 max-h-16'>
                        <SparklesIcon className='size-10 text-primary' />
                        <h3 className='text-xl font-semibold uppercase'>Overflow</h3>
                    </Link>
                </div>
                
                <div className='flex-1' />
                
                <div className='flex basis-1/4 shrink-0 justify-end gap-3 items-center'>
                    <ThemeToggle />
                    {user ? (
                        <UserMenu user={user} avatarUrl={avatarUrl} displayName={displayName ?? user.displayName} />
                    ) : (
                        <>
                            <LoginButton />
                            <RegisterButton />
                        </>
                    )}
                </div>
            </div>
        </header>
    );
}