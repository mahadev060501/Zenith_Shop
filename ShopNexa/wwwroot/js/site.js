// Cart count update (uses Cart/Count endpoint; always scoped to current logged-in user)
document.addEventListener('DOMContentLoaded', function () {
    updateCartCount();
    wireAddToCartForms();
});

function updateCartCount() {
    const badge = document.getElementById('cartCount');
    if (!badge) {
        return;
    }

    // Always fetch from server so cart never leaks across users or sessions
    fetch('/Cart/Count', { method: 'GET', headers: { 'Accept': 'application/json' } })
        .then(function (response) {
            if (!response.ok) {
                throw new Error('Failed to fetch cart count');
            }
            return response.json();
        })
        .then(function (data) {
            const count = (data && typeof data.count === 'number') ? data.count : 0;
            if (count > 0) {
                badge.textContent = count.toString();
                badge.style.display = 'inline-block';
            } else {
                badge.textContent = '0';
                badge.style.display = 'none';
            }
        })
        .catch(function () {
            // On error, just hide badge to avoid confusing stale values
            badge.style.display = 'none';
        });
}

// Wire up "Add to cart" forms so they use AJAX and stay on the same page
function wireAddToCartForms() {
    const forms = document.querySelectorAll('form[data-add-to-cart="true"]');
    if (!forms || forms.length === 0) {
        return;
    }

    forms.forEach(function (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();

            const action = form.getAttribute('action') || '/Cart/Add';
            const formData = new FormData(form);

            fetch(action, {
                method: 'POST',
                body: formData,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            })
                .then(function (response) { return response.json(); })
                .then(function (data) {
                    if (!data) {
                        alert('Could not add to cart. Please try again.');
                        return;
                    }

                    if (data.requiresLogin) {
                        // Redirect to login, preserving current page as returnUrl
                        var returnUrl = window.location.pathname + window.location.search;
                        window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(returnUrl);
                        return;
                    }

                    if (data.success) {
                        showCartToast('Added to cart');
                        updateCartCount();
                    } else {
                        alert(data.message || 'Could not add to cart. Please try again.');
                    }
                })
                .catch(function () {
                    alert('Could not add to cart. Please try again.');
                });
        });
    });
}

// Small toast message for cart actions
function showCartToast(message) {
    var existing = document.getElementById('cart-toast');
    if (!existing) {
        existing = document.createElement('div');
        existing.id = 'cart-toast';
        existing.style.position = 'fixed';
        existing.style.right = '1.5rem';
        existing.style.bottom = '1.5rem';
        existing.style.zIndex = '1080';
        existing.style.padding = '0.75rem 1.25rem';
        existing.style.borderRadius = '999px';
        existing.style.backgroundColor = '#198754';
        existing.style.color = '#fff';
        existing.style.fontWeight = '600';
        existing.style.boxShadow = '0 0.5rem 1rem rgba(0,0,0,0.15)';
        existing.style.display = 'none';
        document.body.appendChild(existing);
    }

    existing.textContent = message || 'Added to cart';
    existing.style.display = 'block';

    setTimeout(function () {
        existing.style.display = 'none';
    }, 2000);
}

// Auto-dismiss alerts after 5 seconds
document.addEventListener('DOMContentLoaded', function() {
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(function(alert) {
        setTimeout(function() {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 5000);
    });
});

// Form validation enhancement
(function() {
    'use strict';
    window.addEventListener('load', function() {
        var forms = document.getElementsByTagName('form');
        var validation = Array.prototype.filter.call(forms, function(form) {
            form.addEventListener('submit', function(event) {
                if (form.checkValidity() === false) {
                    event.preventDefault();
                    event.stopPropagation();
                }
                form.classList.add('was-validated');
            }, false);
        });
    }, false);
})();
