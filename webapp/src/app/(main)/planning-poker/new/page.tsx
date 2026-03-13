import {auth} from "@/auth";
import CreateRoomForm from "@/app/(main)/planning-poker/new/CreateRoomForm";

export default async function NewPlanningPokerRoomPage() {
    const session = await auth();
    const isAuthenticated = !!session?.user;

    return <CreateRoomForm isAuthenticated={isAuthenticated}/>;
}

