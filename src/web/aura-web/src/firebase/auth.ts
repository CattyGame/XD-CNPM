import {
  inMemoryPersistence,
  onAuthStateChanged,
  setPersistence,
  signInWithEmailAndPassword,
  signOut,
  type User,
} from 'firebase/auth'
import { getFirebaseAuth } from './config'

export async function loginForNotifications(
  email: string,
  password: string,
): Promise<User> {
  const auth = getFirebaseAuth()

  if (!auth) {
    throw new Error('Firebase chưa được cấu hình.')
  }

  if (!email.trim() || !password) {
    throw new Error('Cần nhập email và mật khẩu.')
  }

  await setPersistence(auth, inMemoryPersistence)

  const credential = await signInWithEmailAndPassword(
    auth,
    email.trim(),
    password,
  )

  return credential.user
}

export function observeNotificationUser(
  callback: (user: User | null) => void,
): () => void {
  const auth = getFirebaseAuth()

  if (!auth) {
    callback(null)
    return () => {}
  }

  return onAuthStateChanged(auth, callback)
}

export async function getNotificationIdToken(): Promise<string> {
  const auth = getFirebaseAuth()
  const user = auth?.currentUser

  if (!user) {
    throw new Error('Cần đăng nhập trước khi đăng ký thông báo.')
  }

  return user.getIdToken()
}

export async function logoutFirebaseNotificationSession(): Promise<void> {
  const auth = getFirebaseAuth()

  if (auth) {
    await signOut(auth)
  }
}