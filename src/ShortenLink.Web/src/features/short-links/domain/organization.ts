export function parseTagInput(value: string): string[] {
  return [...new Set(
    value
      .split(",")
      .map((tag) => tag.trim().toLowerCase())
      .filter(Boolean)
  )];
}

export function formatTagInput(tags: string[] | null | undefined): string {
  return (tags ?? []).join(", ");
}
