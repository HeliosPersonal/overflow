import {auth} from "@/auth";
import RoomClient from "@/app/(main)/planning-poker/[roomId]/RoomClient";

export default async function RoomPage({params}: {params: Promise<{roomId: string}>}) {
    const {roomId} = await params;
    const session = await auth();
    const isAuthenticated = !!session?.user;

    return (
        <RoomClient
            roomId={roomId}
            isAuthenticated={isAuthenticated}
        />
    );
}
