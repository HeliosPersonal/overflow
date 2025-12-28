import {cloudinary} from "@/lib/cloudinary";
import {cloudinaryConfig} from "@/lib/config";

export async function POST(request: Request) {
    const body = (await request.json()) as {paramsToSign: Record<string, string>}
    const {paramsToSign} = body;
    
    const signature = cloudinary.v2.utils.api_sign_request(paramsToSign, 
        cloudinaryConfig.apiSecret);
    
    return Response.json({signature});
}