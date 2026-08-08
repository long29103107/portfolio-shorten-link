export function isCurrentRequestGeneration(
  requestVersion: number,
  currentVersion: number,
  signal: AbortSignal
): boolean {
  return requestVersion === currentVersion && !signal.aborted;
}
