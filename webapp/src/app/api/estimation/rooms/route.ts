import {NextRequest, NextResponse} from "next/server";
import {proxyEstimation} from "@/app/api/estimation/proxy";

export async function GET(req: NextRequest) {
    const res = await proxyEstimation(req, "/estimation/rooms/my", "GET");
    // Prevent browsers and CDNs from caching the room list so avatars / display
    // names are always fresh after a profile update.
    res.headers.set("Cache-Control", "no-store");
    return res;
}

export async function POST(req: NextRequest) {
    const body = await req.json();
    return proxyEstimation(req, "/estimation/rooms", "POST", body);
}

