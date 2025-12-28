import cloudinary from "cloudinary";
import {cloudinaryConfig} from "@/lib/config";

console.log('🌥️  [Cloudinary] Initializing Cloudinary SDK...', {
    hasCloudName: !!process.env.NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME,
    hasApiKey: !!process.env.CLOUDINARY_API_KEY,
    hasApiSecret: !!process.env.CLOUDINARY_API_SECRET,
});

cloudinary.v2.config({
    cloud_name: cloudinaryConfig.cloudName,
    api_key: cloudinaryConfig.apiKey,
    api_secret: cloudinaryConfig.apiSecret,
});

console.log('✅ [Cloudinary] SDK configured');

export { cloudinary };