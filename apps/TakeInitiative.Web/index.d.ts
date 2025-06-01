declare module '#app' {
    interface PageMeta {
        requiresAuth: boolean
        pageType?: 'scrollable' | 'fixed'
    }
}

// It is always important to ensure you import/export something when augmenting a type
export { }
