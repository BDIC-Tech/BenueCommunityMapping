// BSBS Community Mapping – Global JS helpers

function delRow(btn) {
    const tbody = btn.closest('tbody');
    if (tbody && tbody.rows.length > 1) {
        btn.closest('tr').remove();
        reIndexTableBody(tbody);
    }
}

function addRow(tbodyId) {
    const tbody = document.getElementById(tbodyId);
    if (!tbody) return;
    const newRow = tbody.rows[0].cloneNode(true);
    const idx    = tbody.rows.length;
    newRow.querySelectorAll('input, select, textarea').forEach(el => {
        if (el.type !== 'hidden') el.value = '';
        if (el.type === 'checkbox') el.checked = false;
        if (el.tagName === 'SELECT') el.selectedIndex = 0;
        if (el.name) el.name = el.name.replace(/\[(\d+)\]/, `[${idx}]`);
    });
    tbody.appendChild(newRow);
}

function reIndexTableBody(tbody) {
    Array.from(tbody.rows).forEach((row, rowIdx) => {
        row.querySelectorAll('[name]').forEach(el => {
            el.name = el.name.replace(/\[(\d+)\]/, `[${rowIdx}]`);
        });
    });
}

document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.alert.alert-dismissible').forEach(alert => {
        setTimeout(() => bootstrap?.Alert?.getOrCreateInstance(alert)?.close(), 6000);
    });
});
