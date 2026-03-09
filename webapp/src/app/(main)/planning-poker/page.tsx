import {auth} from "@/auth";
import PlanningPokerLanding from "@/app/(main)/planning-poker/PlanningPokerLanding";

export default async function PlanningPokerPage() {
    const session = await auth();
    const isAuthenticated = !!session?.user;

    return <PlanningPokerLanding isAuthenticated={isAuthenticated}/>;
}
