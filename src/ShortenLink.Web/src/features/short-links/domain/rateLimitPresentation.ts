import type { RateLimitActivity, RateLimitPolicyActivity } from "../types";

export type RateLimitPolicyView = {
  label: string;
  permitLimit: string;
  window: string;
  queueLimit: string;
  rejectedCount: number;
};

export function buildRateLimitPolicyView(
  label: string,
  policy: RateLimitPolicyActivity
): RateLimitPolicyView {
  return {
    label,
    permitLimit: String(policy.permitLimit),
    window: `${policy.windowSeconds}s`,
    queueLimit: String(policy.queueLimit),
    rejectedCount: policy.rejectedCount
  };
}

export function buildRateLimitPolicyViews(activity: RateLimitActivity): RateLimitPolicyView[] {
  return [
    buildRateLimitPolicyView("Create", activity.create),
    buildRateLimitPolicyView("Redirect", activity.redirect)
  ];
}
