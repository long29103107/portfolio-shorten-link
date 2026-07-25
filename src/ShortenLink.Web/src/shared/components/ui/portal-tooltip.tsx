import type { ReactNode } from "react";
import { createPortal } from "react-dom";
import { cn } from "../../lib/utils";

type PortalTooltipProps = {
  children: ReactNode;
  className?: string;
  position: {
    x: number;
    y: number;
  } | null;
};

export function PortalTooltip({
  children,
  className,
  position
}: PortalTooltipProps) {
  if (!position || typeof document === "undefined") {
    return null;
  }

  return createPortal(
    <div
      className={cn("portal-tooltip", className)}
      role="status"
      aria-live="polite"
      style={{
        left: position.x,
        top: position.y
      }}
    >
      {children}
    </div>,
    document.body
  );
}
