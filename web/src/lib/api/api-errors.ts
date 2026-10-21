// The failed queries and mutations of the app, reported by the query cache to the admin layout, which applies the
// session and permission rules of docs/SCREENS.md (Conventions) wherever a call fails.
type Listener = (error: unknown) => void

const listeners = new Set<Listener>()

export function reportApiError(error: unknown): void {
  for (const listener of listeners) listener(error)
}

/** Listens to every failed call; returns the function that stops listening. */
export function onApiError(listener: Listener): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}
