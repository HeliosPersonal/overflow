'use server';

import {auth} from "@/auth";
import {User} from "next-auth";


export async function getCurrentUser(): Promise<User | null> {
    const session = await auth();
    return session?.user ?? null;
}