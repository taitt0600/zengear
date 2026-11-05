import * as React from "react"
import { cn } from "cn"
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  MoreHorizontalIcon,
} from "lucide-react"
import { useTranslations } from "next-intl"
import { Slot } from "radix-ui"

import { buttonVariants, type Button } from "@/components/ui/button"

function Pagination({ className, ...props }: React.ComponentProps<"nav">) {
  const t = useTranslations("ui")
  return (
    <nav
      role="navigation"
      aria-label={t("pagination.label")}
      data-slot="pagination"
      className={cn("mx-auto flex w-full justify-center", className)}
      {...props}
    />
  )
}

function PaginationContent({
  className,
  ...props
}: React.ComponentProps<"ul">) {
  return (
    <ul
      data-slot="pagination-content"
      className={cn("flex flex-row items-center gap-1", className)}
      {...props}
    />
  )
}

function PaginationItem({ ...props }: React.ComponentProps<"li">) {
  return <li data-slot="pagination-item" {...props} />
}

// asChild renders the link as its child, such as a next/link Link, for navigation inside the app.
type PaginationLinkProps = {
  isActive?: boolean
  asChild?: boolean
} & Pick<React.ComponentProps<typeof Button>, "size"> &
  React.ComponentProps<"a">

function PaginationLink({
  className,
  isActive,
  size = "icon",
  asChild = false,
  ...props
}: PaginationLinkProps) {
  const Comp = asChild ? Slot.Root : "a"
  return (
    <Comp
      aria-current={isActive ? "page" : undefined}
      data-slot="pagination-link"
      data-active={isActive}
      className={cn(
        buttonVariants({
          variant: isActive ? "outline" : "ghost",
          size,
        }),
        className
      )}
      {...props}
    />
  )
}

// With asChild, the child (a Link) becomes the link and the chevron and the text go inside it (Radix's Slottable).
function PaginationPrevious({
  className,
  asChild,
  children,
  ...props
}: React.ComponentProps<typeof PaginationLink>) {
  const t = useTranslations("ui")
  return (
    <PaginationLink
      aria-label={t("pagination.previous")}
      size="default"
      className={cn("gap-1 px-2.5 sm:pl-2.5", className)}
      asChild={asChild}
      {...props}
    >
      <ChevronLeftIcon />
      {asChild && <Slot.Slottable>{children}</Slot.Slottable>}
      <span className="hidden sm:block">{t("pagination.previous")}</span>
    </PaginationLink>
  )
}

function PaginationNext({
  className,
  asChild,
  children,
  ...props
}: React.ComponentProps<typeof PaginationLink>) {
  const t = useTranslations("ui")
  return (
    <PaginationLink
      aria-label={t("pagination.next")}
      size="default"
      className={cn("gap-1 px-2.5 sm:pr-2.5", className)}
      asChild={asChild}
      {...props}
    >
      {asChild && <Slot.Slottable>{children}</Slot.Slottable>}
      <span className="hidden sm:block">{t("pagination.next")}</span>
      <ChevronRightIcon />
    </PaginationLink>
  )
}

function PaginationEllipsis({
  className,
  ...props
}: React.ComponentProps<"span">) {
  const t = useTranslations("ui")
  return (
    <span
      aria-hidden
      data-slot="pagination-ellipsis"
      className={cn("flex size-9 items-center justify-center", className)}
      {...props}
    >
      <MoreHorizontalIcon className="size-4" />
      <span className="sr-only">{t("pagination.more")}</span>
    </span>
  )
}

export {
  Pagination,
  PaginationContent,
  PaginationLink,
  PaginationItem,
  PaginationPrevious,
  PaginationNext,
  PaginationEllipsis,
}
