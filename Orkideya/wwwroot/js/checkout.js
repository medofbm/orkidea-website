/* Progressive enhancement; the server independently verifies all amounts. */
'use strict';
function initCheckoutSummary(document) {
    const summary = document.getElementById('checkoutSummary');
    const cities = document.getElementById('shippingRateIdInput');
    const shipping = document.getElementById('shippingTotal');
    const total = document.getElementById('orderTotal');
    if (!summary || !cities || !shipping || !total) return;
    function update() {
        const option = cities.selectedOptions[0];
        const subtotal = Number(summary.dataset.subtotal);
        const price = option?.dataset.price;
        const cost = Number(price);
        if (!cities.value || price == null || price.trim() === '' || !Number.isFinite(cost) || cost < 0 || !Number.isFinite(subtotal) || subtotal < 0) {
            shipping.textContent = 'اختر المدينة';
            total.textContent = 'اختر المدينة';
            return;
        }
        shipping.textContent = cost.toFixed(2) + ' د.ل';
        total.textContent = (subtotal + cost).toFixed(2) + ' د.ل';
    }
    cities.addEventListener('change', update);
    update();
}
if (typeof document !== 'undefined') document.addEventListener('DOMContentLoaded', () => initCheckoutSummary(document));
if (typeof module !== 'undefined' && module.exports) module.exports = { initCheckoutSummary };
