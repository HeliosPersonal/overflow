import {NextRequest} from "next/server";
import {proxyEstimation} from "@/app/api/estimation/proxy";

export async function POST(req: NextRequest) {
    return proxyEstimation(req, "/estimation/refresh-profile", "POST");
}

