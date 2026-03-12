import {getTags} from "@/lib/actions/tag-actions";
import {auth} from "@/auth";
import {redirect} from "next/navigation";
import TagsTable from "@/app/(main)/tags/manage/TagsTable";

export default async function Page() {
    const session = await auth();
    if (!session?.user?.roles?.includes('admin')) redirect('/tags');

    const {data: tags, error} = await getTags();
    if (error) return (
        <div className='w-full px-6'>
            <p className="text-sm text-neutral-500 dark:text-neutral-400 pt-4">{error.message}</p>
        </div>
    );

    return (
        <div className='w-full px-6'>
            <div className='flex flex-col gap-3 pb-6'>
                <h1>Manage Tags</h1>
                <p className='text-default-500'>Add, edit, or remove tags. Changes take effect immediately.</p>
            </div>
            <TagsTable tags={tags ?? []} />
        </div>
    );
}

