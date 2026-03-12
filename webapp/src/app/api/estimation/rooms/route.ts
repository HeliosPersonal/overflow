import {NextRequest} from "next/server";
import {proxyEstimation} from "@/app/api/estimation/proxy";

export async function GET(req: NextRequest) {
    return proxyEstimation(req, "/estimation/rooms/my", "GET");
}

export async function POST(req: NextRequest) {
    const body = await req.json();
    return proxyEstimation(req, "/estimation/rooms", "POST", body);
}

