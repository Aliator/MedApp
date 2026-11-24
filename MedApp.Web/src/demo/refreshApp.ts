const KEEP = /^medappFullDemo/;

const deleteDatabase = (name: string) =>
  new Promise<void>((resolve) => {
    try {
      const request = indexedDB.deleteDatabase(name);
      request.onsuccess = request.onerror = request.onblocked = () => resolve();
    } catch {
      resolve();
    }
  });

export async function refreshApp(): Promise<void> {
  try {
    for (const key of Object.keys(localStorage)) if (!KEEP.test(key)) localStorage.removeItem(key);
    sessionStorage.clear();
  } catch {
    void 0;
  }
  await deleteDatabase('medapp-light-demo');
  try {
    if ('caches' in window) for (const name of await caches.keys()) await caches.delete(name);
    await fetch(window.location.href, {cache: 'reload'});
  } catch {
    void 0;
  }
  window.location.reload();
}
