"use client";

import type { ButtonHTMLAttributes, ReactNode } from "react";
import { cn } from "@/shared/lib/cn";
import { Icon, type IconName } from "./icons";

type Variant = "primary" | "outline" | "ghost" | "danger";
type Size = "sm" | "md";

const variants: Record<Variant, string> = {
  primary:
    "border border-accent bg-accent text-accent-foreground hover:brightness-110 shadow-[0_0_18px_-8px_var(--accent)]",
  outline: "border border-border-strong bg-transparent text-foreground hover:border-jade hover:text-jade",
  ghost: "border border-transparent bg-transparent text-muted hover:bg-foreground/5 hover:text-foreground",
  danger: "border border-ember/60 bg-ember/10 text-ember hover:bg-ember/20",
};

const sizes: Record<Size, string> = {
  sm: "h-8 px-2.5 text-xs",
  md: "h-10 px-4 text-sm",
};

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  icon?: IconName;
  trailingIcon?: IconName;
  children?: ReactNode;
}

export function Button({
  variant = "outline",
  size = "md",
  icon,
  trailingIcon,
  children,
  className,
  type = "button",
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      className={cn(
        "inline-flex shrink-0 items-center justify-center gap-2 rounded-lg font-medium transition-all",
        "focus-visible:ring-2 focus-visible:ring-jade/60 focus-visible:outline-none",
        "disabled:cursor-not-allowed disabled:opacity-45 disabled:hover:brightness-100",
        variants[variant],
        sizes[size],
        className,
      )}
      {...rest}
    >
      {icon && <Icon name={icon} className="size-4" />}
      {children}
      {trailingIcon && <Icon name={trailingIcon} className="size-4" />}
    </button>
  );
}

interface IconButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "size"> {
  icon: IconName;
  label: string;
  variant?: Variant;
  size?: Size;
}

export function IconButton({
  icon,
  label,
  variant = "ghost",
  size = "sm",
  className,
  ...rest
}: IconButtonProps) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      className={cn(
        "inline-flex shrink-0 items-center justify-center rounded-lg transition-all",
        "focus-visible:ring-2 focus-visible:ring-jade/60 focus-visible:outline-none",
        "disabled:cursor-not-allowed disabled:opacity-45",
        variants[variant],
        size === "sm" ? "size-8" : "size-10",
        className,
      )}
      {...rest}
    >
      <Icon name={icon} className="size-4" />
    </button>
  );
}
