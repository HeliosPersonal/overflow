import {NextRequest} from "next/server";
import {proxyEstimation} from "@/app/api/estimation/proxy";

export async function PUT(req: NextRequest, {params}: {params: Promise<{roomId: string}>}) {
    const {roomId} = await params;
    const body = await req.json();
    return proxyEstimation(req, `/estimation/rooms/${roomId}/title`, "PUT", body);
}

