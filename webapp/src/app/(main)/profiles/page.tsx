import ProfilesList from "@/app/(main)/profiles/ProfilesList";
import {getUserProfiles} from "@/lib/actions/profile-actions";
import {auth} from "@/auth";
import {redirect} from "next/navigation";

type SearchParams = Promise<{sortBy?: string}>

export default async function Page({searchParams}: {searchParams: SearchParams}) {
    const session = await auth();
    if (!session?.user?.roles?.includes('admin')) redirect('/questions');
    const {sortBy} = await searchParams;
    const {data: profiles, error} = await getUserProfiles(sortBy);

    if (error) {
        return (
            <div className="min-h-full bg-content1 flex flex-col gap-3 px-6 pt-4">
                <h1>Leaderboard</h1>
                <p className="text-sm text-foreground-400">{error.message}</p>
            </div>
        );
    }

    if (!profiles) return null;

    return (
        <div className="min-h-full bg-content1 flex flex-col gap-3 px-6 pt-4 pb-6">
            <h1>Leaderboard</h1>
            <ProfilesList profiles={profiles} />
        </div>
    );
}