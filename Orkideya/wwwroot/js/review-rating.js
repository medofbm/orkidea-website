/* Preserve posted ratings; visual hover must never change the submitted value. */
'use strict';
function initReviewRating(document) {
    const stars = Array.from(document.querySelectorAll('.star-btn'));
    const input = document.getElementById('ratingValue');
    const selector = document.getElementById('starsSelector');
    const number = document.getElementById('ratingNumber');
    if (!input || !selector || !number || stars.length === 0) return;
    function paint(rating, commitStyle) {
        stars.forEach(star => {
            const active = Number(star.dataset.rating) <= rating;
            star.style.color = active ? '#ffc107' : '#d0d0d0';
            if (commitStyle) {
                star.classList.toggle('fas', active);
                star.classList.toggle('far', !active);
                star.setAttribute('aria-pressed', String(Number(star.dataset.rating) === rating));
            }
        });
    }
    function restore() {
        const value = Number(input.value);
        paint(Number.isInteger(value) && value >= 1 && value <= 5 ? value : 0, true);
        number.textContent = input.value;
    }
    function choose(star) {
        const value = Number(star.dataset.rating);
        if (!Number.isInteger(value) || value < 1 || value > 5) return;
        input.value = String(value);
        restore();
    }
    stars.forEach(star => {
        star.setAttribute('role', 'button');
        star.setAttribute('tabindex', '0');
        star.setAttribute('aria-label', star.dataset.rating + ' / 5');
        star.addEventListener('click', event => {event.preventDefault();choose(star);});
        star.addEventListener('keydown', event => {
            if (event.key === 'Enter' || event.key === ' ') {event.preventDefault();choose(star);}
        });
        star.addEventListener('mouseenter', () => paint(Number(star.dataset.rating), false));
    });
    selector.addEventListener('mouseleave', restore);
    restore();
}
if (typeof document !== 'undefined') document.addEventListener('DOMContentLoaded', () => initReviewRating(document));
if (typeof module !== 'undefined' && module.exports) module.exports = {initReviewRating};
