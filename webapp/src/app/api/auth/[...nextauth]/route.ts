import { handlers } from "@/auth"
import logger from "@/lib/logger";

// Log all auth requests
logger.info({ loadedAt: new Date().toISOString() }, 'NextAuth route module loaded');

// Export handlers directly - NextAuth handles the typing internally
export const { GET, POST } = handlers;

// Note: To see request/response logs, check the NextAuth debug mode in auth.ts
// Set debug: true in the NextAuth config for detailed logs
