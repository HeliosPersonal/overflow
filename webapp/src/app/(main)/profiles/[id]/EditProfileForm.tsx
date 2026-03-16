import {useForm} from "react-hook-form";
import {zodResolver} from "@hookform/resolvers/zod";
import {Input, Textarea} from "@heroui/input";
import {Button} from "@heroui/button";
import {Profile} from "@/lib/types";
import {useTransition} from "react";
import {useRouter} from "next/navigation";
import {editProfile} from "@/lib/actions/profile-actions";
import {handleError, successToast} from "@/lib/util";
import {editProfileSchema, EditProfileSchema} from "@/lib/schemas/editProfileSchema";

type Props = {
    profile: Profile;
    setEditMode: (value: boolean) => void;
}

export default function EditProfileForm({profile, setEditMode}: Props) {
    const [pending, startTransition] = useTransition();
    const router = useRouter();
    const {register, handleSubmit, formState: {isSubmitting, errors, isValid}} = useForm<EditProfileSchema>({
        resolver: zodResolver(editProfileSchema),
        mode: 'onTouched',
        defaultValues: {
            displayName: profile.displayName,
            description: profile.description
        }
    })

    const onSubmit = (data: EditProfileSchema) => {
        startTransition(async () => {
            const {error} = await editProfile(profile.userId, data);
            if (error) {
                handleError(error);
                return;
            }
            successToast('Profile successfully updated');
            setEditMode(false);
            router.refresh();
        })
    }

    return (
        <form onSubmit={handleSubmit(onSubmit)} className='flex flex-col gap-5'>
            <Input
                {...register('displayName')}
                label='Display name'
                size='lg'
                classNames={{ input: 'text-lg', label: 'text-base' }}
                isInvalid={!!errors.displayName}
                errorMessage={errors.displayName?.message}
            />
            <Textarea
                {...register('description')}
                label='Description'
                size='lg'
                rows={6}
                classNames={{ input: 'text-lg', label: 'text-base' }}
                isInvalid={!!errors.description}
                errorMessage={errors.description?.message}
            />
            <Button
                isLoading={isSubmitting || pending}
                isDisabled={isSubmitting || !isValid}
                color='primary'
                size='lg'
                className='w-fit'
                type='submit'
            >Submit</Button>
        </form>
    );
}