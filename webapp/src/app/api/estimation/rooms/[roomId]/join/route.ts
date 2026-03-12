import {NextRequest} from "next/server";
import {proxyEstimation} from "@/app/api/estimation/proxy";

export async function POST(req: NextRequest, {params}: {params: Promise<{roomId: string}>}) {
    const {roomId} = await params;
    const body = await req.json().catch(() => ({}));
    return proxyEstimation(req, `/estimation/rooms/${roomId}/join`, "POST", body);
}

