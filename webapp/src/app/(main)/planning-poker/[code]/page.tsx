import {auth} from "@/auth";
import RoomClient from "@/app/(main)/planning-poker/[code]/RoomClient";

export default async function RoomPage({params}: {params: Promise<{code: string}>}) {
    const {code} = await params;
    const session = await auth();
    const isAuthenticated = !!session?.user;

    return <RoomClient code={code} isAuthenticated={isAuthenticated}/>;
}
