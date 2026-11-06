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

        }

        for (const [index, file] of files.entries()) {
            const item = document.createElement('li');
            item.className = 'position-relative d-flex flex-column align-items-center';
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
            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'btn btn-light border rounded-circle position-absolute top-0 end-0 p-0';
            remove.style.width = '32px';
            remove.style.height = '32px';
            remove.textContent = '×';
            remove.setAttribute('aria-label', 'إزالة ' + file.name + ' من الصور المختارة');
            remove.addEventListener('click', () => {
                const remaining = new DataTransfer();
                Array.from(galleryInput.files).forEach((selected, selectedIndex) => {
                    if (selectedIndex !== index) remaining.items.add(selected);
                });
                galleryInput.files = remaining.files;
                renderPreview();
                const buttons = galleryPreview.querySelectorAll('button');
                (buttons[Math.min(index, buttons.length - 1)] ?? galleryInput).focus();
            });
            item.append(remove);
            galleryPreview.append(item);
        }
        if (retainedCount + files.length <= 5 && uploadedBytes <= maxBytes) {
            galleryStatus.textContent = files.length ? 'هذه الصور ستُرفع عند حفظ المنتج.' : '';
        }
    }

    galleryInput.addEventListener('change', renderPreview);
    cover?.addEventListener('change', renderPreview);
    removals.forEach(input => input.addEventListener('change', renderPreview));
}
