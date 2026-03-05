import Link from "next/link";
import {AcademicCapIcon} from "@heroicons/react/24/outline";
import ThemeToggle from "@/components/nav/ThemeToggle";
import SearchInput from "@/components/nav/SearchInput";
import LoginButton from "@/components/nav/LoginButton";
import {getCurrentUser} from "@/lib/actions/auth-actions";
import UserMenu from "@/components/nav/UserMenu";
import RegisterButton from "@/components/nav/RegisterButton";

export default async function TopNav() {
    const user = await getCurrentUser();
    return (
        <header className='p-2 w-full fixed top-0 z-50 border-b border-neutral-200 dark:border-neutral-800 bg-white dark:bg-black'>
            <div className='flex px-10 mx-auto'>
                <div className='flex items-center gap-6'>
                    <Link href='/' className='flex items-center gap-3 max-h-16'>
                        <AcademicCapIcon className='size-10 text-primary' />
                        <h3 className='text-xl font-semibold uppercase'>Overflow</h3>
                    </Link>
                </div>
                
               <SearchInput />
                
                <div className='flex basis-1/4 shrink-0 justify-end gap-3 items-center'>
                    <ThemeToggle />
                    {user ? (
                        <UserMenu user={user} />
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