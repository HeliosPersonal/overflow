import {getProfileById} from "@/lib/actions/profile-actions";
import {notFound} from "next/navigation";
import {handleError} from "@/lib/util";
import ProfileDetailed from "@/app/(main)/profiles/[id]/ProfileDetailed";
import {getCurrentUser} from "@/lib/actions/auth-actions";
import {auth} from "@/auth";

type Params = Promise<{id: string}>
export default async function Page({params}: {params: Params}) {
    const [currentUser, session] = await Promise.all([getCurrentUser(), auth()]);
    const {id} = await params;
    const {data: profile, error} = await getProfileById(id);
    const currentUserProfile = currentUser?.id === id;

    if (error) handleError(error);
    if (!profile) return notFound()

    return (
        <div className="px-6 flex flex-col gap-3 pt-4">
            <h1>Profile details</h1>
            <ProfileDetailed
                profile={profile}
                currentUserProfile={currentUserProfile}
                session={currentUserProfile ? session : null}
            />
        </div>
    );
}