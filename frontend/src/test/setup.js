import '@testing-library/jest-dom/vitest'

// jsdom's window.localStorage is undefined under this Node version (it defers to Node's own
// experimental Storage API, which needs a --localstorage-file flag we don't set) — a minimal
// in-memory Storage polyfill is enough for tests that only need getItem/setItem/removeItem/clear.
function createMemoryStorage() {
  const store = new Map()
  return {
    getItem: (key) => (store.has(key) ? store.get(key) : null),
    setItem: (key, value) => store.set(key, String(value)),
    removeItem: (key) => store.delete(key),
    clear: () => store.clear(),
    key: (index) => Array.from(store.keys())[index] ?? null,
    get length() {
      return store.size
    },
  }
}

const memoryStorage = createMemoryStorage()

for (const target of [globalThis, window]) {
  Object.defineProperty(target, 'localStorage', {
    value: memoryStorage,
    configurable: true,
    writable: true,
  })
}
