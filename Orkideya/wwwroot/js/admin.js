/**
 * ============================================
 * ORKIDEYA ADMIN PANEL - JAVASCRIPT
 * Modern Admin Dashboard Functionality
 * ============================================
 */

'use strict';

// Admin utilities
const AdminUtils = {
    // Show toast notification
    showToast(message, type = 'success') {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                toast: true,
                position: 'top-end',
                icon: type,
                title: message,
                showConfirmButton: false,
                timer: 3000,
                timerProgressBar: true,
            });
        }
    },
    
    // Confirm dialog
    async confirm(message, title = 'هل أنت متأكد؟') {
        if (typeof Swal !== 'undefined') {
            const result = await Swal.fire({
                title: title,
                text: message,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#6a1b9a',
                cancelButtonColor: '#dc3545',
                confirmButtonText: 'نعم، تأكيد',
                cancelButtonText: 'إلغاء'
            });
            return result.isConfirmed;
        }
        return confirm(message);
    },
    
    // Format currency
    formatCurrency(amount) {
        return new Intl.NumberFormat('ar-LY', {
            style: 'decimal',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(amount) + ' د.ل';
    },
    
    // Format date
    formatDate(date) {
        return new Intl.DateTimeFormat('ar-LY', {
            year: 'numeric',
            month: 'long',
            day: 'numeric'
        }).format(new Date(date));
    }
};

// Initialize admin dashboard
document.addEventListener('DOMContentLoaded', () => {
    initDeleteConfirmations();
    initFormValidations();
    initTableFilters();
});

/**
 * Initialize delete confirmations
 */
function initDeleteConfirmations() {
    const deleteForms = document.querySelectorAll('form[action*="Delete"]');
    
    deleteForms.forEach(form => {
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            
            const confirmed = await AdminUtils.confirm(
                'لن تتمكن من التراجع عن هذا الإجراء!',
                'هل تريد حذف هذا العنصر؟'
            );
            
            if (confirmed) {
                form.submit();
            }
        });
    });
}

/**
 * Initialize form validations
 */
function initFormValidations() {
    const forms = document.querySelectorAll('.needs-validation');
    
    Array.from(forms).forEach(form => {
        form.addEventListener('submit', (event) => {
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
            }
            
            form.classList.add('was-validated');
        }, false);
    });
}

/**
 * Initialize table filters
 */
function initTableFilters() {
    const searchInputs = document.querySelectorAll('[data-table-search]');
    
    searchInputs.forEach(input => {
        const tableId = input.dataset.tableSearch;
        const table = document.querySelector(tableId);
        
        if (!table) return;
        
        input.addEventListener('input', (e) => {
            const searchTerm = e.target.value.toLowerCase();
            const rows = table.querySelectorAll('tbody tr');
            
            rows.forEach(row => {
                const text = row.textContent.toLowerCase();
                row.style.display = text.includes(searchTerm) ? '' : 'none';
            });
        });
    });
}

/**
 * Export table to Excel
 */
function exportTableToExcel(tableId, filename = 'export') {
    const table = document.querySelector(tableId);
    if (!table) return;
    
    // This is a placeholder - actual implementation would use a library or server-side export
    AdminUtils.showToast('جاري التصدير...', 'info');
}

// Export utilities globally
window.AdminUtils = AdminUtils;
window.exportTableToExcel = exportTableToExcel;
