'use client';

import {Button} from "@heroui/button";
import {useRouter} from "next/navigation";

export default function RegisterButton() {
    const router = useRouter();
    
    return (
        <Button 
            onPress={() => router.push('/signup?callbackUrl=/questions')}
            color='primary'
        >
            Register
        </Button>
    );
}