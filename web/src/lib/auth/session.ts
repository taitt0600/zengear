'use client'

import type { QueryClient } from '@tanstack/react-query'
import {
  EmailAuthProvider,
  onAuthStateChanged,
  reauthenticateWithCredential,
  signInWithEmailAndPassword,
  signOut,
} from 'firebase/auth'
import { useEffect, useState } from 'react'
import { firebaseAuth } from './firebase'

export type AuthState = 'loading' | 'signed-in' | 'signed-out'

/** The Firebase session of this browser, followed as it changes; `loading` until the SDK has read its storage. */
export function useAuthState(): AuthState {
  const [state, setState] = useState<AuthState>('loading')
  useEffect(
    () => onAuthStateChanged(firebaseAuth(), (user) => setState(user ? 'signed-in' : 'signed-out')),
    [],
  )
  return state
}

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

/**
 * Signs the current user in again with their password, for the API's 403 recent-login-required: it changes a
 * password only within 5 minutes of a sign-in (DEC-094). Firebase errors are thrown as they come.
 */
export async function reauthenticate(password: string): Promise<void> {
  const user = firebaseAuth().currentUser
  if (!user?.email) throw new Error('No signed-in user with an email')
  // The SDK swaps in the tokens of the answer (Firebase 13, UserCredentialImpl._forOperation), so the next call
  // already carries the new auth_time.
  await reauthenticateWithCredential(user, EmailAuthProvider.credential(user.email, password))
}

/** Signs out of Firebase and drops every cached answer of the API, so the next user sees none of it. */
export async function signOutAndClear(queryClient: QueryClient): Promise<void> {
  await signOut(firebaseAuth())
  queryClient.clear()
}
