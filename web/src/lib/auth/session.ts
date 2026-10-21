'use client'

import type { QueryClient } from '@tanstack/react-query'
import { signInWithEmailAndPassword, signOut } from 'firebase/auth'
import { firebaseAuth } from './firebase'

/** Whether this browser holds a Firebase session, once the SDK has read its storage. */
export async function hasSession(): Promise<boolean> {
  const auth = firebaseAuth()
  await auth.authStateReady()
  return auth.currentUser !== null
}

/** The ID token the API client sends, refreshed by the SDK when it expires; null when nobody is signed in. */
export async function currentIdToken(): Promise<string | null> {
  const auth = firebaseAuth()
  await auth.authStateReady()
  return auth.currentUser ? auth.currentUser.getIdToken() : null
}

export async function signInWithPassword(email: string, password: string): Promise<void> {
  await signInWithEmailAndPassword(firebaseAuth(), email, password)
}

/** Signs out of Firebase and drops every cached answer of the API, so the next user sees none of it. */
export async function signOutAndClear(queryClient: QueryClient): Promise<void> {
  await signOut(firebaseAuth())
  queryClient.clear()
}
