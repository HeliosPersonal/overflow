import {getTags} from "@/lib/actions/tag-actions";
import {auth} from "@/auth";
import {redirect} from "next/navigation";
import TagsTable from "@/app/(main)/tags/manage/TagsTable";

export default async function Page() {
    const session = await auth();
    if (!session?.user?.roles?.includes('admin')) redirect('/tags');

    const {data: tags, error} = await getTags();
    if (error) return (
        <div className='min-h-full bg-content1 w-full px-6 pt-4'>
            <p className="text-sm text-foreground-400">{error.message}</p>
        </div>
    );

    return (
        <div className='min-h-full bg-content1 w-full px-6 pb-6'>
            <div className='flex flex-col gap-3 pt-4 pb-6'>
                <h1>Manage Tags</h1>
                <p className='text-foreground-500'>Add, edit, or remove tags. Changes take effect immediately.</p>
            </div>
            <TagsTable tags={tags ?? []} />
        </div>
    );
}

