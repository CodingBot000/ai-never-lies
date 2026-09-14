import '@testing-library/jest-dom/vitest'
import { afterEach, beforeEach } from 'vitest'
import { cleanup } from '@testing-library/react'

beforeEach(() => {
  window.localStorage.clear()
  document.documentElement.dataset.textSize = '100'
  delete document.documentElement.dataset.reducedMotion
})

afterEach(() => {
  cleanup()
})
