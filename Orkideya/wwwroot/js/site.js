/**
 * ============================================
 * ORKIDEA - MAIN JAVASCRIPT FILE
 * Modern ES6+ | No jQuery Required
 * ============================================
 */

'use strict';

// Utility functions
const Utils = {
    // Debounce function for performance optimization
    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    },

    // Safe query selector
    qs(selector, parent = document) {
        return parent.querySelector(selector);
    },

    // Safe query selector all
    qsa(selector, parent = document) {
        return Array.from(parent.querySelectorAll(selector));
    }
};

// Main application initialization
document.addEventListener('DOMContentLoaded', () => {
    initSwiperSlider();
    initVariantSelection();
    initQuantitySelectors();
    initAddToCart();
    initAccessibility();
});

/**
 * Initialize Featured Products Swiper
 */
function initSwiperSlider() {
    const swiperElement = Utils.qs('.featured-swiper');
    if (!swiperElement) return;

    try {
        const swiper = new Swiper('.featured-swiper', {
            loop: false,
            slidesPerView: 1,
            spaceBetween: 24,
            watchOverflow: true,
            pagination: {
                el: '.featured-swiper .swiper-pagination',
                clickable: true,
                dynamicBullets: true,
            },
            navigation: {
                nextEl: '.featured-swiper .swiper-button-next',
                prevEl: '.featured-swiper .swiper-button-prev',
            },
            breakpoints: {
                640: { slidesPerView: 2, spaceBetween: 24 },
                992: { slidesPerView: 3, spaceBetween: 28 },
            },
            autoplay: {
                delay: 5000,
                disableOnInteraction: false,
                pauseOnMouseEnter: true,
            },
            speed: 600,
        });
    } catch (error) {
        console.error('Error initializing featured Swiper:', error);
    }
}

/**
 * Initialize Variant Selection
 */
function initVariantSelection() {
    // Support both new class (.product-card-wrapper) and old class (.product-card)
    const productCards = Utils.qsa('.product-card-wrapper, .product-card');

    productCards.forEach(card => {
        // Support both new class (.variant-size-btn) and old class (.variant-btn)
        const variantButtons = Utils.qsa('.variant-size-btn, .variant-btn', card);
        const priceDisplay = Utils.qs('[class*="price-display-"]', card);
        const hiddenVariantInput = Utils.qs('input[name="variantId"]', card);

        variantButtons.forEach(button => {
            button.addEventListener('click', function (e) {
                e.preventDefault();

                // Remove active class from all buttons in this card
                variantButtons.forEach(btn => {
                    btn.classList.remove('active');
                    btn.setAttribute('aria-pressed', 'false');
                });

                // Add active class to clicked button
                this.classList.add('active');
                this.setAttribute('aria-pressed', 'true');

                // Get variant data
                const { productId, price, variantId } = this.dataset;

                // Update price display with animation
                if (priceDisplay) {
                    priceDisplay.style.opacity = '0';
                    setTimeout(() => {
                        // Keep currency label inside
                        priceDisplay.innerHTML = `${price} <small class="price-currency">د.ل</small>`;
                        priceDisplay.style.opacity = '1';
                    }, 150);
                }

                // Update hidden input
                if (hiddenVariantInput) {
                    hiddenVariantInput.value = variantId;
                }

                // Update add to cart buttons
                const addToCartButtons = Utils.qsa('.add-to-cart-ajax-btn, .add-to-cart-btn', card);
                addToCartButtons.forEach(btn => {
                    btn.dataset.variantId = variantId;
                });
            });
        });

        // Set initial active variant with proper ARIA
        const firstVariantButton = variantButtons[0];
        const activeButton = Utils.qs('.variant-size-btn.active, .variant-btn.active', card);

        if (firstVariantButton && !activeButton) {
            firstVariantButton.classList.add('active');
            firstVariantButton.setAttribute('aria-pressed', 'true');
        } else if (activeButton) {
            activeButton.setAttribute('aria-pressed', 'true');
        }
    });
}

/**
 * Initialize Quantity Selectors
 */
function initQuantitySelectors() {
    const productCards = Utils.qsa('.product-card-wrapper, .product-card');

    productCards.forEach(card => {
        const quantityInput = Utils.qs('.quantity-input', card);
        const quantityMinusBtn = Utils.qs('.quantity-minus', card);
        const quantityPlusBtn = Utils.qs('.quantity-plus', card);

        if (!quantityInput) return;

        // Decrease quantity
        if (quantityMinusBtn) {
            quantityMinusBtn.addEventListener('click', (e) => {
                e.preventDefault();
                const currentQuantity = parseInt(quantityInput.value, 10) || 1;
                if (currentQuantity > 1) {
                    quantityInput.value = currentQuantity - 1;
                    quantityInput.dispatchEvent(new Event('change'));
                }
            });
        }

        // Increase quantity
        if (quantityPlusBtn) {
            quantityPlusBtn.addEventListener('click', (e) => {
                e.preventDefault();
                const currentQuantity = parseInt(quantityInput.value, 10) || 1;
                const maxQuantity = parseInt(quantityInput.max, 10) || 999;
                if (currentQuantity < maxQuantity) {
                    quantityInput.value = currentQuantity + 1;
                    quantityInput.dispatchEvent(new Event('change'));
                }
            });
        }

        // Validate input on change
        quantityInput.addEventListener('change', () => {
            let value = parseInt(quantityInput.value, 10);
            const min = parseInt(quantityInput.min, 10) || 1;
            const max = parseInt(quantityInput.max, 10) || 999;

            if (isNaN(value) || value < min) {
                value = min;
            } else if (value > max) {
                value = max;
            }

            quantityInput.value = value;
        });
    });
}


/**
 * Initialize Add to Cart AJAX functionality
 */
function initAddToCart() {
    const addToCartButtons = Utils.qsa('.add-to-cart-btn, .add-to-cart-ajax-btn');

    addToCartButtons.forEach(button => {
        button.addEventListener('click', async function (event) {
            event.preventDefault();

            // Disable button during request
            this.disabled = true;
            const originalHTML = this.innerHTML;
            this.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> جاري الإضافة...';

            try {
                // Read variantId directly from the button (works on both product cards AND product detail page)
                const variantId = this.dataset.variantId;

                // Try to get quantity from a nearby input, or fall back to data-quantity attr
                const card = this.closest('.product-card-wrapper, .product-card');
                const quantityInput = card ? Utils.qs('.quantity-input', card) : null;
                const quantity = quantityInput
                    ? parseInt(quantityInput.value, 10)
                    : parseInt(this.dataset.quantity, 10) || 1;

                if (!variantId) {
                    showNotification('الرجاء اختيار الحجم أولاً', 'error');
                    return;
                }

                // Get CSRF token
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ||
                    document.querySelector('[name="__RequestVerificationToken"]')?.value;

                // Make AJAX request
                const url = `/Cart/AddToCartAjax?variantId=${variantId}&quantity=${quantity}`;
                const headers = {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest',
                };

                // Add CSRF token if available
                if (token) {
                    headers['RequestVerificationToken'] = token;
                }

                const response = await fetch(url, {
                    method: 'POST',
                    headers: headers,
                    credentials: 'same-origin'
                });

                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                const data = await response.json();

                if (data.success) {
                    // Update cart badge with animation
                    updateCartBadge(data.count);

                    // Show success feedback
                    this.innerHTML = '<i class="fas fa-check me-1"></i> تمت الإضافة ✓';
                    showNotification('تمت الإضافة للسلة بنجاح! 🛒', 'success');
                    setTimeout(() => {
                        this.innerHTML = originalHTML;
                    }, 2000);
                } else {
                    showNotification(data.message || 'فشل في إضافة المنتج', 'error');
                    this.innerHTML = originalHTML;
                }
            } catch (error) {
                console.error('Error adding to cart:', error);
                showNotification('حدث خطأ، يرجى المحاولة مرة أخرى', 'error');
                this.innerHTML = originalHTML;
            } finally {
                // Re-enable button
                this.disabled = false;
            }
        });
    });
}


/**
 * Update cart badge with animation - updates the header badge
 */
function updateCartBadge(count) {
    const cartBadge = Utils.qs('#cartBadge');
    if (cartBadge) {
        cartBadge.textContent = count;
        if (count > 0) {
            cartBadge.classList.remove('cart-badge--hidden');
        } else {
            cartBadge.classList.add('cart-badge--hidden');
        }
        cartBadge.classList.remove('pulse-animation');
        void cartBadge.offsetWidth;
        cartBadge.classList.add('pulse-animation');
        setTimeout(() => cartBadge.classList.remove('pulse-animation'), 600);
    }
}


/**
 * Show notification (can be enhanced with a toast library)
 */
function showNotification(message, type = 'info') {
    // Simple console notification for now
    // Can be replaced with SweetAlert2 or custom toast
    console.log(`[${type.toUpperCase()}]: ${message}`);

    // If SweetAlert2 is available, use it
    if (typeof Swal !== 'undefined') {
        Swal.fire({
            text: message,
            icon: type === 'error' ? 'error' : 'success',
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 3000,
            timerProgressBar: true,
        });
    }
}

/**
 * Initialize accessibility improvements
 */
function initAccessibility() {
    // Add skip link functionality
    const skipLink = Utils.qs('.skip-to-main');
    if (skipLink) {
        skipLink.addEventListener('click', (e) => {
            e.preventDefault();
            const mainContent = Utils.qs('#main-content');
            if (mainContent) {
                mainContent.focus();
                mainContent.scrollIntoView({ behavior: 'smooth' });
            }
        });
    }

    // Improve dropdown accessibility
    const dropdowns = Utils.qsa('[data-bs-toggle="dropdown"]');
    dropdowns.forEach(dropdown => {
        dropdown.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                dropdown.click();
            }
        });
    });
}

// Export utilities for potential reuse
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { Utils, updateCartBadge, showNotification };
}