// Mantine использует matchMedia и ResizeObserver, которых нет в jsdom.
// Моки по официальной рекомендации: https://mantine.dev/guides/vitest/

import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
})

class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}

window.ResizeObserver = ResizeObserverMock as unknown as typeof ResizeObserver

// Общая зачистка для всех тестов: DOM и глобальные стабы fetch
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})
