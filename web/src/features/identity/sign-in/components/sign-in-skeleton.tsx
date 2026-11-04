import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { Field, FieldGroup } from '@/components/ui/field'
import { Skeleton } from '@/components/ui/skeleton'

/**
 * The loading state of the sign-in card while its search parameters are read: as high as the form, each bar the
 * height of the line it stands for (the labels and the texts of Help on screen are 20 px lines).
 */
export function SignInSkeleton() {
  return (
    <Card>
      <CardHeader>
        <Skeleton className="mx-auto h-7 w-48" />
        <Skeleton className="mx-auto h-5 w-64 max-w-full" />
      </CardHeader>
      <CardContent>
        <FieldGroup>
          <Field>
            <Skeleton className="h-5 w-24" />
            <Skeleton className="h-9 w-full" />
            <Skeleton className="h-5 w-56 max-w-full" />
          </Field>
          <Field>
            <Skeleton className="h-5 w-24" />
            <Skeleton className="h-9 w-full" />
          </Field>
          <Field>
            <Skeleton className="h-9 w-full" />
            <Skeleton className="mx-auto h-5 w-52 max-w-full" />
          </Field>
        </FieldGroup>
      </CardContent>
    </Card>
  )
}
