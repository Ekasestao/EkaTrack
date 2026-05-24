export function initScrollLoader(sentinelId, dotnetRef) {
    const sentinel = document.getElementById(sentinelId);
    if (!sentinel) return null;

    const observer = new IntersectionObserver((entries) => {
        if (entries[0].isIntersecting) {
            dotnetRef.invokeMethodAsync('LoadNextPage');
        }
    }, { rootMargin: '400px' });

    observer.observe(sentinel);
    return { observer };
}

export function destroyScrollLoader(instance) {
    if (instance && instance.observer) {
        instance.observer.disconnect();
    }
}
