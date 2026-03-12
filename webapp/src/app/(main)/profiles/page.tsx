import ProfilesList from "@/app/(main)/profiles/ProfilesList";
import {getUserProfiles} from "@/lib/actions/profile-actions";

type SearchParams = Promise<{sortBy?: string}>

export default async function Page({searchParams}: {searchParams: SearchParams}) {
    const {sortBy} = await searchParams;
    const {data: profiles, error} = await getUserProfiles(sortBy);

    if (error) {
        return (
            <div className="flex flex-col gap-3 px-6 pt-4">
                <h1>User table</h1>
                <p className="text-sm text-neutral-500 dark:text-neutral-400">{error.message}</p>
            </div>
        );
    }

    if (!profiles) return null;

    return (
        <div className="flex flex-col gap-3 px-6 pt-4">
            <h1>User table</h1>
            <ProfilesList profiles={profiles} />
        </div>
    );
}