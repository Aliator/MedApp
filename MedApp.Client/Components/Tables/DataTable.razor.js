export function measureAndObserve(wrapperEl, dotNetRef) {
    function getRowHeight() {
        const row = wrapperEl.querySelector('tbody tr:not(.skeleton-row)');
        return row ? row.getBoundingClientRect().height : 0;
    }

    function calculate() {
        const thead = wrapperEl.querySelector('thead');
        const tfoot = wrapperEl.querySelector('tfoot');
        const rowHeight = getRowHeight();
        if (!thead || !tfoot || rowHeight === 0) return;

        const wrapperTop = Math.max(0, wrapperEl.getBoundingClientRect().top);
        const availableHeight = window.innerHeight - wrapperTop;
        const theadHeight = thead.getBoundingClientRect().height;
        const tfootHeight = tfoot.getBoundingClientRect().height;
        const pageSize = Math.max(1, Math.floor((availableHeight - theadHeight - tfootHeight) / rowHeight));
        dotNetRef.invokeMethodAsync('UpdatePageSize', pageSize);
    }

    wrapperEl.addEventListener('mousedown', e => {
        if (e.target.closest('.pagination-btn')) e.preventDefault();
    });

    wrapperEl.addEventListener('click', e => {
        if (!e.target.closest('.pagination-btn')) return;
        wrapperEl.classList.add('pagination-suppress-hover');
        const restore = () => {
            wrapperEl.classList.remove('pagination-suppress-hover');
            window.removeEventListener('mousemove', restore);
        };
        window.addEventListener('mousemove', restore);
    });

    const ro = new ResizeObserver(calculate);
    ro.observe(wrapperEl);
    ro.observe(document.documentElement);

    return {
        recalculate: calculate,
        dispose: () => ro.disconnect()
    };
}
