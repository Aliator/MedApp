export function measureAndObserve(wrapperEl, dotNetRef) {
    let debounceTimer = null;

    function calculate() {
        const thead = wrapperEl.querySelector('thead');
        const tfoot = wrapperEl.querySelector('tfoot');
        if (!thead || !tfoot) return;

        const rowHeightStr = getComputedStyle(wrapperEl).getPropertyValue('--dt-row-height').trim();
        const rowHeight = parseFloat(rowHeightStr);
        if (!rowHeight || rowHeight === 0) return;

        const wrapperTop = Math.max(0, wrapperEl.getBoundingClientRect().top);
        const availableHeight = window.innerHeight - wrapperTop;
        const theadHeight = thead.getBoundingClientRect().height;
        const tfootHeight = tfoot.getBoundingClientRect().height;

        const remPx = parseFloat(getComputedStyle(document.documentElement).fontSize);
        const pageSize = Math.max(1, Math.floor((availableHeight - theadHeight - tfootHeight - remPx) / rowHeight));
        dotNetRef.invokeMethodAsync('UpdatePageSize', pageSize);
    }

    function scheduleCalculate() {
        if (debounceTimer) clearTimeout(debounceTimer);
        debounceTimer = setTimeout(calculate, 50);
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

    const ro = new ResizeObserver(scheduleCalculate);
    ro.observe(wrapperEl);
    ro.observe(document.documentElement);

    return {
        recalculate: scheduleCalculate,
        dispose: () => {
            if (debounceTimer) clearTimeout(debounceTimer);
            ro.disconnect();
        }
    };
}