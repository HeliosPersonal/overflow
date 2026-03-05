'use client';

import {Button} from "@heroui/button";
import {useRouter} from "next/navigation";

export default function LoginButton() {
    const router = useRouter();
    
    return (
        <Button 
            color='primary'
            variant='bordered'
            type='button'
            onPress={() => router.push('/login?callbackUrl=/questions')}
        >
            Login
        </Button>
    );
}