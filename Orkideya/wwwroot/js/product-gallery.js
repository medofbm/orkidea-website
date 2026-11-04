'use strict';

const productGalleryImage = document.getElementById('productGalleryImage');
if (productGalleryImage) {
    const photos = document.querySelectorAll('[data-product-photo]');
    photos.forEach((photo, index) => {
        if (index === 0) photo.setAttribute('aria-current', 'true');
        photo.addEventListener('click', event => {
            if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            productGalleryImage.src = photo.href;
            photos.forEach(item => item.removeAttribute('aria-current'));
            photo.setAttribute('aria-current', 'true');
        });
    });
}
