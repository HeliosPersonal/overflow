import { handlers } from "@/auth"

// Log all auth requests
console.log('[NextAuth Route] Module loaded at:', new Date().toISOString());

// Export handlers directly - NextAuth handles the typing internally
export const { GET, POST } = handlers;

// Note: To see request/response logs, check the NextAuth debug mode in auth.ts
// Set debug: true in the NextAuth config for detailed logs
