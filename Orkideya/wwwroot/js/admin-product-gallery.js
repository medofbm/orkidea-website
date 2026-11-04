'use strict';

const galleryInput = document.getElementById('galleryFiles');
const galleryPreview = document.getElementById('galleryPreview');
const galleryStatus = document.getElementById('galleryPreviewStatus');

if (galleryInput && galleryPreview && galleryStatus) {
    let previewUrls = [];

    const cover = galleryInput.form.elements.namedItem('imageFile');
    const removals = galleryInput.form.querySelectorAll('[name=removeGallery]');

    function renderPreview() {
        galleryPreview.replaceChildren();
        previewUrls.forEach(url => URL.revokeObjectURL(url));
        previewUrls = [];

        const files = Array.from(galleryInput.files);
        const maxBytes = 5 * 1024 * 1024;
        const supportedTypes = ['image/png', 'image/jpeg'];
        const retainedCount = Array.from(removals).filter(input => !input.checked).length;
        const uploadedBytes = files.reduce((sum, file) => sum + file.size, cover?.files?.[0]?.size ?? 0);
        if (retainedCount + files.length > 5 || uploadedBytes > maxBytes) {
            galleryStatus.textContent = 'اختاري حتى خمس صور إضافية وبحجم إجمالي لا يتجاوز 5 ميجابايت مع الغلاف.';
            return;
        }

        for (const file of files) {
            const item = document.createElement('li');
            item.className = 'd-flex flex-column align-items-center';
            if (supportedTypes.includes(file.type)) {
                const image = document.createElement('img');
                image.alt = '';
                image.width = 72;
                image.height = 72;
                image.className = 'object-fit-contain rounded border';
                image.src = URL.createObjectURL(file);
                previewUrls.push(image.src);
                item.append(image);
            }

            const caption = document.createElement('span');
            caption.className = 'small text-break';
            caption.textContent = file.name;
            item.append(caption);
            galleryPreview.append(item);
        }
        galleryStatus.textContent = files.length ? 'هذه الصور ستُرفع عند حفظ المنتج.' : '';
    }

    galleryInput.addEventListener('change', renderPreview);
    cover?.addEventListener('change', renderPreview);
    removals.forEach(input => input.addEventListener('change', renderPreview));
}
