// ============================================================
// LoxxKing Portal - Customer Storefront & Admin Management Suite
// ============================================================

let currentApiBase = localStorage.getItem('loxx_api_base') || 'https://loxxking-backend-production.up.railway.app/api';
let currentToken = localStorage.getItem('token') || null;
let currentUser = JSON.parse(localStorage.getItem('user') || 'null');

// State
let allProducts = [];
let allCategories = [];
let cart = JSON.parse(localStorage.getItem('loxx_cart') || '[]');
let activePortalMode = 'storefront';
let currentChatConversationId = null;
let customerConversationId = localStorage.getItem('loxx_customer_conv_id') || null;
let apiLogCount = 0;

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    initEndpointSelector();
    checkAuth();
    pingBackend();
    loadShopProducts();
    loadCategoriesDropdown();
    updateCartBadge();
});

// ============================================================
// PORTAL MODE SWITCHER (STOREFRONT vs ADMIN)
// ============================================================

function switchPortalMode(mode) {
    activePortalMode = mode;
    document.querySelectorAll('.portal-mode').forEach(el => el.classList.remove('active'));
    document.querySelectorAll('.mode-btn').forEach(el => el.classList.remove('active'));

    document.getElementById(`${mode}-mode`).classList.add('active');
    document.getElementById(`mode-${mode}-btn`).classList.add('active');

    if (mode === 'storefront') {
        loadShopProducts();
        loadShopOffers();
    } else if (mode === 'admin') {
        ensureAdminToken().then(() => {
            loadOverviewStats();
        });
    }
}

// ============================================================
// ENDPOINT & HEALTH PING
// ============================================================

function initEndpointSelector() {
    const select = document.getElementById('endpoint-select');
    if (!select) return;
    const knownValues = ['http://localhost:5196/api', 'https://loxxking-backend-production.up.railway.app/api'];
    if (knownValues.includes(currentApiBase)) {
        select.value = currentApiBase;
    } else {
        select.value = 'custom';
    }
}

function handleEndpointChange() {
    const select = document.getElementById('endpoint-select');
    if (select.value === 'custom') {
        document.getElementById('custom-url-modal').style.display = 'flex';
        document.getElementById('custom-url-input').value = currentApiBase;
    } else {
        currentApiBase = select.value;
        localStorage.setItem('loxx_api_base', currentApiBase);
        showToast('info', `Switched API endpoint to: ${currentApiBase}`);
        pingBackend();
    }
}

function saveCustomUrl() {
    const customUrl = document.getElementById('custom-url-input').value.trim();
    if (!customUrl) return;
    currentApiBase = customUrl;
    localStorage.setItem('loxx_api_base', currentApiBase);
    closeCustomUrlModal();
    showToast('info', `Saved custom API endpoint: ${currentApiBase}`);
    pingBackend();
}

function closeCustomUrlModal() { document.getElementById('custom-url-modal').style.display = 'none'; }

async function pingBackend() {
    const statusDot = document.getElementById('status-dot');
    const statusText = document.getElementById('status-text');
    if (!statusDot || !statusText) return;

    statusDot.className = 'dot yellow';
    statusText.textContent = 'Ping API...';

    const startTime = performance.now();
    try {
        const res = await fetch(`${currentApiBase}/categories`, { method: 'GET' });
        const latency = Math.round(performance.now() - startTime);
        if (res.ok || res.status === 401) {
            statusDot.className = 'dot green';
            statusText.textContent = `Online (${latency}ms)`;
        } else {
            statusDot.className = 'dot red';
            statusText.textContent = `HTTP ${res.status}`;
        }
    } catch (err) {
        statusDot.className = 'dot red';
        statusText.textContent = 'Offline';
    }
}

// ============================================================
// CORE API REQUEST INTERCEPTOR & LOGGING
// ============================================================

async function apiRequest(endpoint, options = {}) {
    const startTime = performance.now();
    const method = (options.method || 'GET').toUpperCase();

    const headers = {
        'Content-Type': 'application/json',
        ...(currentToken ? { 'Authorization': `Bearer ${currentToken}` } : {}),
        ...options.headers
    };

    const config = { ...options, headers };

    try {
        const response = await fetch(`${currentApiBase}${endpoint}`, config);
        const timeMs = Math.round(performance.now() - startTime);

        let data = null;
        const contentType = response.headers.get('content-type');
        if (contentType && contentType.includes('application/json')) {
            data = await response.json();
        } else if (contentType && contentType.includes('application/pdf')) {
            data = await response.blob();
        } else {
            data = await response.text();
        }

        logApiCall(method, endpoint, response.status, timeMs, data, !response.ok);

        if (!response.ok) {
            const errorMsg = data && data.message ? data.message : `API Request failed with status ${response.status}`;
            throw new Error(errorMsg);
        }

        return data;
    } catch (error) {
        if (!error.message.includes('API Request failed')) {
            logApiCall(method, endpoint, 'ERR', Math.round(performance.now() - startTime), { error: error.message }, true);
        }
        throw error;
    }
}

// ============================================================
// SEAMLESS AUTO AUTHENTICATION (NO 401 ERRORS FOR ADMIN ACTIONS)
// ============================================================

async function ensureAdminToken() {
    if (currentToken && currentUser && (currentUser.role === 'Admin' || currentUser.role === 'StoreManager')) {
        return;
    }
    try {
        const data = await fetch(`${currentApiBase}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: 'admin@loxxking.com', password: 'Admin@123456' })
        }).then(r => r.json());

        if (data.accessToken) {
            currentToken = data.accessToken;
            currentUser = data.user;
            localStorage.setItem('token', currentToken);
            localStorage.setItem('user', JSON.stringify(currentUser));
            updateUserBadge();
            updateAuthProfileView();
        }
    } catch (err) {
        console.warn('Auto admin login failed', err);
    }
}

// ============================================================
// INSPECTOR CONSOLE
// ============================================================

function toggleApiConsole() {
    document.getElementById('api-console-drawer').classList.toggle('collapsed');
}

function logApiCall(method, endpoint, status, timeMs, data, isError) {
    apiLogCount++;
    const badge = document.getElementById('console-count-badge');
    if (badge) badge.textContent = apiLogCount;

    const logContainer = document.getElementById('console-log-list');
    const placeholder = logContainer.querySelector('.console-placeholder');
    if (placeholder) placeholder.remove();

    const entry = document.createElement('div');
    entry.className = `log-entry ${isError ? 'error' : 'success'}`;

    const timestamp = new Date().toLocaleTimeString();
    let displayJson = '';
    if (data instanceof Blob) {
        displayJson = `[Binary Blob PDF Invoice - ${data.size} bytes]`;
    } else {
        displayJson = JSON.stringify(data, null, 2);
    }

    entry.innerHTML = `
        <div class="log-meta">
            <span>[${timestamp}]</span>
            <strong>${method} ${endpoint}</strong>
            <span>Status: ${status}</span>
            <span>(${timeMs}ms)</span>
        </div>
        <div class="log-json">${escapeHtml(displayJson)}</div>
    `;

    logContainer.prepend(entry);
}

function clearApiLogs() {
    apiLogCount = 0;
    document.getElementById('console-count-badge').textContent = '0';
    document.getElementById('console-log-list').innerHTML = '<div class="console-placeholder">Console cleared.</div>';
}

function escapeHtml(text) {
    if (typeof text !== 'string') return text;
    return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

// ============================================================
// AUTHENTICATION
// ============================================================

async function login(emailInput, passwordInput) {
    const email = emailInput || document.getElementById('auth-email-input').value;
    const password = passwordInput || document.getElementById('auth-password-input').value;

    if (!email || !password) {
        showToast('error', 'Please enter email and password');
        return;
    }

    try {
        const data = await apiRequest('/auth/login', {
            method: 'POST',
            body: JSON.stringify({ email, password })
        });

        currentToken = data.accessToken;
        currentUser = data.user;

        localStorage.setItem('token', currentToken);
        localStorage.setItem('user', JSON.stringify(currentUser));

        updateUserBadge();
        showToast('success', `Welcome back, ${currentUser.name}! (${currentUser.role})`);
        closeLoginModal();
        updateAuthProfileView();
        if (activePortalMode === 'admin') loadOverviewStats();
    } catch (err) {
        showToast('error', `Login Failed: ${err.message}`);
    }
}

function logout() {
    currentToken = null;
    currentUser = null;
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    updateUserBadge();
    showToast('info', 'Logged out successfully');
    updateAuthProfileView();
}

function checkAuth() {
    if (currentToken && currentUser) {
        updateUserBadge();
        updateAuthProfileView();
    }
}

function updateUserBadge() {
    const badge = document.getElementById('user-profile-badge');
    const quickBtn = document.getElementById('quick-login-btn');

    if (currentUser) {
        badge.style.display = 'flex';
        quickBtn.style.display = 'none';
        document.getElementById('user-name-display').textContent = currentUser.name;
        document.getElementById('user-role-display').textContent = currentUser.role;
    } else {
        badge.style.display = 'none';
        quickBtn.style.display = 'inline-flex';
    }
}

function fillCredentials(email, password) {
    document.getElementById('auth-email-input').value = email;
    document.getElementById('auth-password-input').value = password;
}

function fillModalLogin(email, password) {
    document.getElementById('modal-email').value = email;
    document.getElementById('modal-password').value = password;
}

function openLoginModal() { document.getElementById('login-modal').style.display = 'flex'; }
function closeLoginModal() { document.getElementById('login-modal').style.display = 'none'; }
function loginFromModal() {
    login(document.getElementById('modal-email').value, document.getElementById('modal-password').value);
}

function updateAuthProfileView() {
    const container = document.getElementById('auth-profile-details');
    if (!container) return;
    if (!currentUser) {
        container.innerHTML = `<div class="alert alert-info">Not logged in. Use quick fill presets.</div>`;
        return;
    }

    container.innerHTML = `
        <div class="list-item">
            <div>
                <strong>👤 Name:</strong> ${currentUser.name}<br>
                <strong>📧 Email:</strong> ${currentUser.email}<br>
                <strong>🏷️ Role:</strong> <span class="badge badge-${currentUser.role.toLowerCase()}">${currentUser.role}</span>
            </div>
            <button class="btn btn-sm btn-danger" onclick="logout()">Logout</button>
        </div>
        <div class="margin-top-md">
            <label style="font-size:11px;font-weight:700;">Bearer Token:</label>
            <textarea readonly style="font-family:monospace;font-size:10px;height:70px;">${currentToken}</textarea>
        </div>
    `;
}

// ============================================================
// STOREFRONT: SHOP, CART & GUEST CHECKOUT (NO ACCOUNT NEEDED)
// ============================================================

async function loadShopProducts() {
    const grid = document.getElementById('shop-products-grid');
    if (!grid) return;
    grid.innerHTML = '<div class="loading-state">Fetching products...</div>';

    try {
        const data = await apiRequest('/products?page=1&pageSize=50');
        allProducts = data.data || data || [];

        if (allProducts.length === 0) {
            grid.innerHTML = '<div class="loading-state">📭 No products in shop catalog yet.</div>';
            return;
        }

        renderShopGrid(allProducts);
    } catch (err) {
        grid.innerHTML = `<div class="loading-state" style="color:var(--accent-rose)">❌ Failed to load shop products: ${err.message}</div>`;
    }
}

function renderShopGrid(productsList) {
    const grid = document.getElementById('shop-products-grid');
    const countEl = document.getElementById('shop-results-count');
    if (countEl) countEl.textContent = `Showing ${productsList.length} Products`;

    grid.innerHTML = productsList.map(p => {
        const pId = p.id || p.Id;
        const pNameEn = p.nameEn || p.NameEn;
        const pNameAr = p.nameAr || p.NameAr || '';
        const pPrice = p.price || p.Price || p.basePrice || p.BasePrice || 0;
        return `
            <div class="shop-product-card">
                <div class="shop-card-img">
                    ${p.imageUrl ? `<img src="${p.imageUrl}" alt="${pNameEn}" onerror="this.onerror=null;this.parentNode.innerHTML='📦';">` : '📦'}
                </div>
                <div class="shop-card-body">
                    <div>
                        <div class="shop-card-title">${escapeHtml(pNameEn)}</div>
                        <div class="shop-card-sub">${escapeHtml(pNameAr)}</div>
                    </div>
                    <div class="shop-card-footer">
                        <div class="shop-card-price">${pPrice} EGP</div>
                        <button class="btn btn-sm btn-primary" onclick="addToCart('${pId}')">🛒 Add to Cart</button>
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

function filterShopProducts() {
    const search = document.getElementById('shop-search-input').value.toLowerCase();
    const catId = document.getElementById('shop-category-filter').value;

    const filtered = allProducts.filter(p => {
        const nameEn = p.nameEn || p.NameEn || '';
        const nameAr = p.nameAr || p.NameAr || '';
        const cId = p.categoryId || p.CategoryId || '';
        const matchesName = nameEn.toLowerCase().includes(search) || nameAr.includes(search);
        const matchesCat = !catId || cId === catId;
        return matchesName && matchesCat;
    });

    renderShopGrid(filtered);
}

async function loadShopOffers() {
    const container = document.getElementById('shop-offers-preview');
    if (!container) return;

    try {
        const offers = await apiRequest('/offers?activeOnly=true');
        if (!offers || offers.length === 0) {
            container.innerHTML = '<div class="loading-state">No special deals today.</div>';
            return;
        }

        container.innerHTML = offers.map(o => `
            <div class="list-item margin-top-md" style="flex-direction:column;align-items:flex-start;">
                <span class="badge badge-admin">${o.discountPercent}% OFF</span>
                <strong style="margin-top:4px;">${escapeHtml(o.productName || 'Special Item')}</strong>
            </div>
        `).join('');
    } catch {
        container.innerHTML = '<div class="loading-state">No offers available</div>';
    }
}

// --- SHOPPING CART ---

function addToCart(productId) {
    const product = allProducts.find(p => (p.id || p.Id) === productId);
    if (!product) return;

    const pId = product.id || product.Id;
    const pName = product.nameEn || product.NameEn;
    const pPrice = product.price || product.Price || product.basePrice || product.BasePrice || 0;

    const existingIndex = cart.findIndex(item => item.id === pId);
    if (existingIndex > -1) {
        cart[existingIndex].quantity++;
    } else {
        cart.push({
            id: pId,
            nameEn: pName,
            price: pPrice,
            quantity: 1
        });
    }

    localStorage.setItem('loxx_cart', JSON.stringify(cart));
    updateCartBadge();
    showToast('success', `Added '${pName}' to shopping cart!`);
}

function updateCartBadge() {
    const totalQty = cart.reduce((sum, item) => sum + item.quantity, 0);
    const badge = document.getElementById('cart-count-badge');
    if (badge) badge.textContent = totalQty;
}

function openCartModal() {
    renderCart();
    document.getElementById('cart-modal').style.display = 'flex';
}

function closeCartModal() { document.getElementById('cart-modal').style.display = 'none'; }

function renderCart() {
    const list = document.getElementById('cart-items-list');
    const summaryBox = document.getElementById('cart-summary-box');
    const checkoutBtn = document.getElementById('checkout-btn');

    if (cart.length === 0) {
        list.innerHTML = '<div class="loading-state">Your shopping cart is empty.</div>';
        summaryBox.style.display = 'none';
        checkoutBtn.style.display = 'none';
        return;
    }

    let total = 0;
    list.innerHTML = cart.map((item, idx) => {
        const itemTotal = item.price * item.quantity;
        total += itemTotal;
        return `
            <div class="list-item">
                <div>
                    <strong>${escapeHtml(item.nameEn)}</strong>
                    <div style="font-size:12px;color:var(--text-muted)">${item.price} EGP × ${item.quantity} = <strong>${itemTotal} EGP</strong></div>
                </div>
                <div style="display:flex;gap:6px;align-items:center;">
                    <button class="btn btn-xs btn-outline" onclick="changeCartQty(${idx}, -1)">-</button>
                    <span>${item.quantity}</span>
                    <button class="btn btn-xs btn-outline" onclick="changeCartQty(${idx}, 1)">+</button>
                    <button class="btn btn-xs btn-danger" onclick="removeCartItem(${idx})">🗑️</button>
                </div>
            </div>
        `;
    }).join('');

    document.getElementById('cart-total-amount').textContent = `${total.toFixed(2)} EGP`;
    document.getElementById('checkout-total-display').textContent = `${total.toFixed(2)} EGP`;
    summaryBox.style.display = 'block';
    checkoutBtn.style.display = 'inline-flex';
}

function changeCartQty(index, delta) {
    if (cart[index]) {
        cart[index].quantity += delta;
        if (cart[index].quantity <= 0) {
            cart.splice(index, 1);
        }
        localStorage.setItem('loxx_cart', JSON.stringify(cart));
        updateCartBadge();
        renderCart();
    }
}

function removeCartItem(index) {
    cart.splice(index, 1);
    localStorage.setItem('loxx_cart', JSON.stringify(cart));
    updateCartBadge();
    renderCart();
}

async function openGuestCheckoutModal() {
    closeCartModal();
    document.getElementById('guest-checkout-modal').style.display = 'flex';
    await populateCheckoutCountries();
}

function closeGuestCheckoutModal() {
    document.getElementById('guest-checkout-modal').style.display = 'none';
}

// --- COUNTRY POPULATION + GEO DETECTION FOR CHECKOUT ---

async function populateCheckoutCountries() {
    const sel = document.getElementById('checkout-country-id');
    const note = document.getElementById('checkout-geo-note');
    sel.innerHTML = '<option value="">⏳ Loading countries...</option>';

    try {
        // 1. Load available countries from backend
        const countries = await apiRequest('/countries');
        if (!countries || countries.length === 0) {
            sel.innerHTML = '<option value="">No countries configured in backend</option>';
            return;
        }

        sel.innerHTML = '<option value="">-- Select your country --</option>' +
            countries.map(c =>
                `<option value="${c.id || c.Id}">${c.name || c.Name}</option>`
            ).join('');

        // 2. Try to auto-detect via IP geolocation (try two services for reliability)
        note.textContent = '🌐 Detecting your location...';
        note.style.color = 'var(--text-muted)';

        let detectedCountryName = null;

        // Service 1: ipapi.co
        try {
            const r1 = await fetch('https://ipapi.co/json/', { signal: AbortSignal.timeout(4000) });
            if (r1.ok) {
                const d1 = await r1.json();
                if (d1.country_name && !d1.error) detectedCountryName = d1.country_name;
            }
        } catch (_) { /* try next */ }

        // Service 2: ip-api.com (fallback)
        if (!detectedCountryName) {
            try {
                const r2 = await fetch('http://ip-api.com/json/?fields=country', { signal: AbortSignal.timeout(4000) });
                if (r2.ok) {
                    const d2 = await r2.json();
                    if (d2.country) detectedCountryName = d2.country;
                }
            } catch (_) { /* detection failed */ }
        }

        if (detectedCountryName) {
            const match = countries.find(c =>
                (c.name || c.Name || '').toLowerCase() === detectedCountryName.toLowerCase()
            );
            if (match) {
                sel.value = match.id || match.Id;
                note.textContent = `📍 Auto-detected: ${match.name || match.Name}`;
                note.style.color = 'var(--accent-emerald)';
            } else {
                note.textContent = `ℹ️ Detected "${detectedCountryName}" — not in backend list. Please select manually.`;
                note.style.color = '#f59e0b';
            }
        } else {
            note.textContent = 'ℹ️ Could not detect location — please select your country manually.';
            note.style.color = 'var(--text-muted)';
        }

    } catch (err) {
        sel.innerHTML = '<option value="">⚠️ Failed to load countries — check backend connection</option>';
        note.textContent = 'Make sure the backend is running and reachable.';
        note.style.color = 'var(--accent-rose)';
    }
}

// --- GUEST CHECKOUT ORDER CREATION ---

async function placeGuestOrder() {
    const name = document.getElementById('checkout-name').value.trim();
    const phone = document.getElementById('checkout-phone').value.trim();
    const address = document.getElementById('checkout-address').value.trim();
    const paymentMethod = parseInt(document.getElementById('checkout-payment-method').value);
    const notes = document.getElementById('checkout-notes').value.trim();
    const countryId = document.getElementById('checkout-country-id').value;

    if (!name || !phone || !address) {
        showToast('error', 'Please fill in Name, Phone Number, and Delivery Address');
        return;
    }

    if (!countryId) {
        showToast('error', 'Please select your country');
        return;
    }

    if (cart.length === 0) {
        showToast('error', 'Your shopping cart is empty');
        return;
    }

    const items = cart.map(item => ({
        productId: item.id,
        quantity: item.quantity
    }));

    try {
        // Guest order — no auth token needed (backend is [AllowAnonymous])
        const orderResult = await apiRequest('/orders', {
            method: 'POST',
            body: JSON.stringify({
                address: `${name} - ${address}`,
                phone,
                notes,
                paymentMethod,
                countryId,
                items
            })
        });

        cart = [];
        localStorage.removeItem('loxx_cart');
        updateCartBadge();

        closeGuestCheckoutModal();

        const orderId = orderResult.id || orderResult.Id || 'SUCCESS';
        document.getElementById('success-order-id').textContent = orderId;
        const pdfBtn = document.getElementById('success-pdf-btn');
        pdfBtn.onclick = () => downloadInvoicePdf(orderId);

        document.getElementById('order-success-modal').style.display = 'flex';
        showToast('success', '🎉 Order placed successfully!');

    } catch (err) {
        showToast('error', `Order Creation Failed: ${err.message}`);
    }
}

function closeOrderSuccessModal() {
    document.getElementById('order-success-modal').style.display = 'none';
}

// --- ORDER TRACKING ---

function openTrackOrderModal() { document.getElementById('track-order-modal').style.display = 'flex'; }
function closeTrackOrderModal() { document.getElementById('track-order-modal').style.display = 'none'; }

async function trackOrder() {
    const orderId = document.getElementById('track-order-id-input').value.trim();
    const resultBox = document.getElementById('track-result-box');
    if (!orderId) {
        showToast('error', 'Please enter Order ID');
        return;
    }

    resultBox.innerHTML = '<div class="loading-state">Searching backend for order...</div>';

    try {
        await ensureAdminToken();
        const order = await apiRequest(`/orders/${orderId}`);

        resultBox.innerHTML = `
            <div class="card shadow-sm" style="border-left:4px solid var(--accent-blue)">
                <div><strong>Order Status:</strong> <span class="status-chip status-${(order.status || 'pending').toLowerCase()}">${order.status}</span></div>
                <div style="font-size:12px;margin-top:6px;">
                    <strong>Customer:</strong> ${order.customer ? order.customer.name : 'Guest'}<br>
                    <strong>Address:</strong> ${order.address || 'N/A'}<br>
                    <strong>Total Amount:</strong> ${order.totalAmount || 0} EGP<br>
                    <strong>Date:</strong> ${new Date(order.createdAt).toLocaleString()}
                </div>
            </div>
        `;
    } catch (err) {
        resultBox.innerHTML = `<div class="alert alert-info" style="color:var(--accent-rose)">Order ID not found or invalid format.</div>`;
    }
}

// ============================================================
// LIVE SUPPORT CHAT HANDLING (CUSTOMER & ADMIN SYNC)
// ============================================================

let _chatPollTimer = null;
let _adminChatPollTimer = null;

async function openCustomerChatWidget() {
    document.getElementById('customer-chat-widget').style.display = 'flex';
    // customerConversationId is only set after first successful send
    await loadCustomerChatHistory();
    // Poll for admin replies every 4s while widget is open
    clearInterval(_chatPollTimer);
    _chatPollTimer = setInterval(loadCustomerChatHistory, 4000);
}

function closeCustomerChatWidget() {
    document.getElementById('customer-chat-widget').style.display = 'none';
    clearInterval(_chatPollTimer);
}

async function loadCustomerChatHistory() {
    const threadBox = document.getElementById('customer-chat-messages');
    if (!customerConversationId) return; // no conversation yet, nothing to load

    try {
        // NOTE: no admin token — fetch as guest/anonymous
        const messages = await fetch(`${currentApiBase}/support-chat/messages/${customerConversationId}`, {
            headers: { 'Content-Type': 'application/json' }
        }).then(r => r.json());

        if (Array.isArray(messages) && messages.length > 0) {
            const atBottom = threadBox.scrollTop + threadBox.clientHeight >= threadBox.scrollHeight - 10;
            threadBox.innerHTML = messages.map(m => {
                const isCustomer = !m.sender || m.sender.role === 'Customer';
                return `<div class="chat-msg ${isCustomer ? 'customer' : 'staff'}">${escapeHtml(m.message)}</div>`;
            }).join('');
            if (atBottom) threadBox.scrollTop = threadBox.scrollHeight;
        }
    } catch (_) { /* silent — backend not yet reachable */ }
}

async function sendCustomerChatMessage() {
    const input = document.getElementById('customer-chat-input');
    const msgText = input.value.trim();
    if (!msgText) return;

    const threadBox = document.getElementById('customer-chat-messages');
    input.value = '';

    // Optimistically show the message immediately
    const msgDiv = document.createElement('div');
    msgDiv.className = 'chat-msg customer';
    msgDiv.textContent = msgText;
    threadBox.appendChild(msgDiv);
    threadBox.scrollTop = threadBox.scrollHeight;

    try {
        // No admin token — send as guest. conversationId may be empty on first message.
        const body = {
            conversationId: customerConversationId || '00000000-0000-0000-0000-000000000000',
            message: msgText
        };

        const resp = await fetch(`${currentApiBase}/support-chat/send`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        if (resp.ok) {
            const data = await resp.json();
            // On first send the backend returns the real conversationId — save it
            const returnedId = data.conversationId || data.ConversationId;
            if (returnedId && returnedId !== '00000000-0000-0000-0000-000000000000') {
                customerConversationId = returnedId;
                localStorage.setItem('loxx_customer_conv_id', returnedId);
            }
            logApiCall('POST', '/support-chat/send', 200, 0, data, false);
        }
    } catch (err) {
        console.warn('Customer chat send failed:', err);
    }
}

// ─── Admin Support Chat Center ────────────────────────────────────────────────

async function loadSupportConversations() {
    const container = document.getElementById('admin-chat-threads');
    if (!container) return;
    container.innerHTML = '<div class="loading-state">Loading chat threads...</div>';

    try {
        await ensureAdminToken();
        const conversations = await apiRequest('/support-chat/conversations');

        if (!conversations || conversations.length === 0) {
            container.innerHTML = `
                <div class="loading-state" style="flex-direction:column;gap:8px;">
                    <div style="font-size:28px;">💬</div>
                    <div>No support chats yet.</div>
                    <div style="font-size:12px;color:var(--text-muted);">Customers who use the chat widget will appear here.</div>
                </div>`;
            return;
        }

        container.innerHTML = conversations.map(c => {
            const convId = c.conversationId || c.ConversationId;
            const name = escapeHtml(c.senderName || c.SenderName || 'Customer');
            const lastMsg = escapeHtml((c.lastMessage || c.LastMessage || '').substring(0, 60));
            const time = new Date(c.lastMessageAt || c.LastMessageAt).toLocaleString();
            const isActive = currentChatConversationId === convId;
            return `
            <div class="list-item" onclick="loadChatMessages('${convId}')" style="
                cursor:pointer;flex-direction:column;align-items:flex-start;
                border-left:3px solid ${isActive ? 'var(--accent-blue)' : 'transparent'};
            ">
                <div style="display:flex;justify-content:space-between;width:100%;">
                    <strong>👤 ${name}</strong>
                    <span style="font-size:10px;color:var(--text-muted);">${time}</span>
                </div>
                <div style="font-size:12px;color:var(--text-muted);margin-top:4px;">&ldquo;${lastMsg}&rdquo;</div>
                <div style="font-size:10px;color:#6366f1;margin-top:2px;font-family:monospace;">${convId}</div>
            </div>`;
        }).join('');

        // Auto-refresh admin conversations every 5s
        clearInterval(_adminChatPollTimer);
        _adminChatPollTimer = setInterval(async () => {
            // Only refresh the thread list, not the open conversation
            const updatedConvs = await apiRequest('/support-chat/conversations').catch(() => null);
            if (!updatedConvs) return;
            const oldIds = new Set(conversations.map(c => c.conversationId));
            const newConvs = updatedConvs.filter(c => !oldIds.has(c.conversationId));
            if (newConvs.length > 0) {
                showToast('info', `💬 ${newConvs.length} new conversation(s) received!`);
                loadSupportConversations();
            }
        }, 5000);

    } catch (err) {
        container.innerHTML = `<div class="loading-state" style="color:var(--accent-rose);">❌ Failed to load chats: ${err.message}</div>`;
    }
}

async function loadChatMessages(conversationId) {
    currentChatConversationId = conversationId;
    const msgBox = document.getElementById('admin-chat-messages');
    const inputRow = document.getElementById('admin-chat-input-row');
    if (!msgBox) return;

    // Highlight selected conversation
    document.querySelectorAll('#admin-chat-threads .list-item').forEach(el => {
        el.style.borderLeftColor = 'transparent';
    });
    const active = [...document.querySelectorAll('#admin-chat-threads .list-item')]
        .find(el => el.textContent.includes(conversationId.substring(0,8)));
    if (active) active.style.borderLeftColor = 'var(--accent-blue)';

    try {
        await ensureAdminToken();
        const messages = await apiRequest(`/support-chat/messages/${conversationId}`);

        if (!messages || messages.length === 0) {
            msgBox.innerHTML = '<div class="loading-state">No messages in this conversation.</div>';
        } else {
            msgBox.innerHTML = messages.map(m => {
                const isCustomer = !m.sender || m.sender.role === 'Customer';
                const senderName = m.sender?.name || 'Customer';
                const time = new Date(m.createdAt || m.CreatedAt).toLocaleTimeString();
                return `
                <div class="chat-msg ${isCustomer ? 'customer' : 'staff'}" style="flex-direction:column;gap:2px;">
                    <div>${escapeHtml(m.message)}</div>
                    <div style="font-size:10px;opacity:0.6;">${escapeHtml(senderName)} · ${time}</div>
                </div>`;
            }).join('');
            msgBox.scrollTop = msgBox.scrollHeight;
        }

        if (inputRow) inputRow.style.display = 'flex';

        // Poll this conversation for new replies every 4s
        clearInterval(_adminChatPollTimer);
        _adminChatPollTimer = setInterval(() => loadChatMessages(conversationId), 4000);

    } catch (err) {
        msgBox.innerHTML = `<div class="loading-state" style="color:var(--accent-rose);">❌ ${err.message}</div>`;
        if (inputRow) inputRow.style.display = 'flex';
    }
}

async function sendAdminChatMessage() {
    const input = document.getElementById('admin-chat-message-input');
    const message = input.value.trim();
    if (!message || !currentChatConversationId) {
        showToast('error', 'Select a conversation first');
        return;
    }

    try {
        await ensureAdminToken();
        await apiRequest('/support-chat/send', {
            method: 'POST',
            body: JSON.stringify({
                conversationId: currentChatConversationId,
                message
            })
        });
        input.value = '';
        await loadChatMessages(currentChatConversationId);
    } catch (err) {
        showToast('error', `Failed to send reply: ${err.message}`);
    }
}

// ============================================================
// ADMIN MANAGEMENT SUITE: ALL CRUD OPERATIONS
// ============================================================

function switchTab(tabId) {
    document.querySelectorAll('.tab-page').forEach(el => el.classList.remove('active'));
    document.querySelectorAll('.nav-item').forEach(el => el.classList.remove('active'));

    const targetTab = document.getElementById(`tab-${tabId}`);
    const targetNav = document.getElementById(`nav-${tabId}`);

    if (targetTab) targetTab.classList.add('active');
    if (targetNav) targetNav.classList.add('active');

    switch (tabId) {
        case 'overview': loadOverviewStats(); break;
        case 'products': loadProducts(); loadCategoriesDropdown(); break;
        case 'categories': loadCategories(); break;
        case 'orders': loadOrders(); break;
        case 'offers': loadOffers(); loadProductsForOfferSelect(); break;
        case 'staff': loadStaff(); break;
        case 'chat': loadSupportConversations(); break;
        case 'analytics': loadSiteVisits(); break;
        case 'auth': updateAuthProfileView(); break;
    }
}

async function loadOverviewStats() {
    try {
        const [prodData, catData, ordersData, offersData, visitsData] = await Promise.allSettled([
            apiRequest('/products?page=1&pageSize=1'),
            apiRequest('/categories'),
            apiRequest('/orders?page=1&pageSize=1'),
            apiRequest('/offers?activeOnly=true'),
            apiRequest('/site-visits/today-count')
        ]);

        document.getElementById('stat-products-count').textContent = (prodData.status === 'fulfilled' && prodData.value.totalCount !== undefined) ? prodData.value.totalCount : '-';
        document.getElementById('stat-categories-count').textContent = (catData.status === 'fulfilled' && Array.isArray(catData.value)) ? catData.value.length : '-';
        document.getElementById('stat-orders-count').textContent = (ordersData.status === 'fulfilled' && ordersData.value.totalCount !== undefined) ? ordersData.value.totalCount : '-';
        document.getElementById('stat-offers-count').textContent = (offersData.status === 'fulfilled' && Array.isArray(offersData.value)) ? offersData.value.length : '-';
        document.getElementById('stat-visits-today').textContent = (visitsData.status === 'fulfilled' && visitsData.value.todayCount !== undefined) ? visitsData.value.todayCount : '0';

        await ensureAdminToken();
        try {
            const staffData = await apiRequest('/users/staff?page=1&pageSize=1');
            document.getElementById('stat-staff-count').textContent = staffData.totalCount !== undefined ? staffData.totalCount : '-';
        } catch {
            document.getElementById('stat-staff-count').textContent = '0';
        }
    } catch (e) {
        console.error('Error loading overview stats', e);
    }
}

// --- PRODUCTS CRUD ---

async function loadProducts() {
    const grid = document.getElementById('products-grid');
    if (!grid) return;
    grid.innerHTML = '<div class="loading-state">Fetching products...</div>';

    try {
        const data = await apiRequest('/products?page=1&pageSize=50');
        allProducts = data.data || data || [];

        if (allProducts.length === 0) {
            grid.innerHTML = '<div class="loading-state">📭 No products found in backend catalog. Click "Create Product" to add one!</div>';
            return;
        }

        renderAdminProducts(allProducts);
    } catch (err) {
        grid.innerHTML = `<div class="loading-state" style="color:var(--accent-rose)">❌ Failed to load products: ${err.message}</div>`;
    }
}

function renderAdminProducts(productsList) {
    const grid = document.getElementById('products-grid');
    grid.innerHTML = productsList.map(p => {
        const pId = p.id || p.Id;
        const pNameEn = p.nameEn || p.NameEn;
        const pNameAr = p.nameAr || p.NameAr || '';
        const pPrice = p.price || p.Price || p.basePrice || p.BasePrice || 0;
        const pStock = p.stock || p.Stock || p.inventoryQuantity || 0;

        return `
            <div class="product-card">
                <div class="product-img">
                    ${p.imageUrl ? `<img src="${p.imageUrl}" alt="${pNameEn}" onerror="this.onerror=null;this.parentNode.innerHTML='📦';">` : '📦'}
                </div>
                <div class="product-body">
                    <div>
                        <div class="product-title">${escapeHtml(pNameEn)}</div>
                        <div class="product-sub">${escapeHtml(pNameAr)}</div>
                    </div>
                    <div class="product-meta">
                        <div class="product-price">${pPrice} EGP</div>
                        <div class="stock-badge ${ pStock > 0 ? 'in-stock' : 'low-stock' }">
                            Stock: ${pStock}
                        </div>
                    </div>
                    <div class="margin-top-md" style="display:flex;gap:6px;">
                        <button class="btn btn-xs btn-outline btn-full" onclick="openEditProductModal('${pId}')">Edit</button>
                        <button class="btn btn-xs btn-danger btn-full" onclick="deleteProduct('${pId}')">Delete</button>
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

function filterProducts() {
    const search = document.getElementById('product-search-input').value.toLowerCase();
    const catId = document.getElementById('product-category-filter').value;

    const filtered = allProducts.filter(p => {
        const nameEn = p.nameEn || p.NameEn || '';
        const nameAr = p.nameAr || p.NameAr || '';
        const cId = p.categoryId || p.CategoryId || '';
        const matchesName = nameEn.toLowerCase().includes(search) || nameAr.includes(search);
        const matchesCat = !catId || cId === catId;
        return matchesName && matchesCat;
    });

    renderAdminProducts(filtered);
}

async function openAddProductModal() {
    document.getElementById('product-modal-title').textContent = '📦 Create New Product';
    document.getElementById('prod-edit-id').value = '';
    document.getElementById('prod-name-en').value = '';
    document.getElementById('prod-name-ar').value = '';
    document.getElementById('prod-price').value = '';
    document.getElementById('prod-description').value = '';
    loadCategoriesDropdown();
    document.getElementById('add-product-modal').style.display = 'flex';
    await loadCountryPriceRows(null, null);
}

async function openEditProductModal(id) {
    const p = allProducts.find(prod => (prod.id || prod.Id) === id);
    if (!p) return;
    document.getElementById('product-modal-title').textContent = '✏️ Edit Product';
    document.getElementById('prod-edit-id').value = p.id || p.Id;
    document.getElementById('prod-name-en').value = p.nameEn || p.NameEn || '';
    document.getElementById('prod-name-ar').value = p.nameAr || p.NameAr || '';
    document.getElementById('prod-price').value = p.price || p.Price || p.basePrice || p.BasePrice || 0;
    document.getElementById('prod-description').value = p.description || p.Description || '';
    loadCategoriesDropdown();
    const cId = p.categoryId || p.CategoryId;
    if (cId) setTimeout(() => { document.getElementById('prod-category-select').value = cId; }, 200);
    document.getElementById('add-product-modal').style.display = 'flex';

    // Load existing prices and inventory for this product
    let existingPrices = {};
    let existingInventory = {};
    try {
        const priceData = await apiRequest(`/products/${id}/prices`);
        (priceData || []).forEach(pp => {
            const cid = pp.countryId || pp.CountryId;
            existingPrices[cid] = pp.price || pp.Price || 0;
        });
    } catch (_) {}
    try {
        const invData = await apiRequest(`/products/${id}/inventory`);
        (invData || []).forEach(inv => {
            const cid = inv.countryId || inv.CountryId;
            existingInventory[cid] = inv.quantity || inv.Quantity || 0;
        });
    } catch (_) {}

    await loadCountryPriceRows(existingPrices, existingInventory);
}

// Currency → flag + readable country name mapping
const CURRENCY_COUNTRY_MAP = {
    EGP: { flag: '🇪🇬', label: 'Egypt' },
    SAR: { flag: '🇸🇦', label: 'Saudi Arabia' },
    AED: { flag: '🇦🇪', label: 'UAE' },
    KWD: { flag: '🇰🇼', label: 'Kuwait' },
    QAR: { flag: '🇶🇦', label: 'Qatar' },
    BHD: { flag: '🇧🇭', label: 'Bahrain' },
    OMR: { flag: '🇴🇲', label: 'Oman' },
    JOD: { flag: '🇯🇴', label: 'Jordan' },
    IQD: { flag: '🇮🇶', label: 'Iraq' },
    LBP: { flag: '🇱🇧', label: 'Lebanon' },
    SYP: { flag: '🇸🇾', label: 'Syria' },
    MAD: { flag: '🇲🇦', label: 'Morocco' },
    DZD: { flag: '🇩🇿', label: 'Algeria' },
    TND: { flag: '🇹🇳', label: 'Tunisia' },
    LYD: { flag: '🇱🇾', label: 'Libya' },
    USD: { flag: '🇺🇸', label: 'USA (USD)' },
    EUR: { flag: '🇪🇺', label: 'Europe (EUR)' },
    GBP: { flag: '🇬🇧', label: 'UK (GBP)' },
    TRY: { flag: '🇹🇷', label: 'Turkey' },
    PKR: { flag: '🇵🇰', label: 'Pakistan' },
};

// Cache countries list so we don't re-fetch on every save
let _cachedCountries = null;

async function loadCountryPriceRows(existingPrices, existingInventory) {
    const container = document.getElementById('country-price-rows');
    container.innerHTML = '<div style="padding:12px;color:#aaa;font-size:13px;">⏳ Loading countries…</div>';

    try {
        const countries = _cachedCountries || (await apiRequest('/countries'));
        _cachedCountries = countries;

        if (!countries || countries.length === 0) {
            container.innerHTML = '<div style="padding:12px;color:#f87171;font-size:13px;">⚠️ No countries found in backend. Add countries first.</div>';
            return;
        }

        // Store on window so quick-fill can access
        window._productCountries = countries;

        const rowsHtml = countries.map(c => {
            const cId   = c.id   || c.Id;
            const cName = c.name || c.Name || '';
            const curr  = c.currency || c.Currency || '';
            // Derive a friendly display: use name if available, else currency map, else currency code
            const meta  = CURRENCY_COUNTRY_MAP[curr] || CURRENCY_COUNTRY_MAP[cName] || {};
            const flag  = meta.flag  || '🌍';
            const label = (cName && cName !== curr) ? cName : (meta.label || curr || 'Unknown');
            const existPrice = (existingPrices  && existingPrices[cId]  != null) ? existingPrices[cId]  : '';
            const existStock = (existingInventory && existingInventory[cId] != null) ? existingInventory[cId] : '';

            return `
            <div class="cp-row" style="
                display:grid;
                grid-template-columns:1fr 150px 110px;
                gap:10px;
                align-items:center;
                padding:8px 12px;
                border-radius:8px;
                border:1px solid rgba(255,255,255,0.07);
                background:rgba(255,255,255,0.03);
                margin-bottom:4px;
            ">
                <div style="display:flex;align-items:center;gap:8px;">
                    <span style="font-size:20px;line-height:1;">${flag}</span>
                    <div>
                        <div style="font-weight:600;font-size:13px;color:#fff;">${escapeHtml(label)}</div>
                        <div style="font-size:10px;color:#888;">${curr}</div>
                    </div>
                </div>
                <div style="position:relative;">
                    <span style="position:absolute;left:9px;top:50%;transform:translateY(-50%);font-size:11px;color:#888;pointer-events:none;">${curr}</span>
                    <input type="number" step="0.01" min="0"
                        id="cp-price-${cId}" value="${existPrice}" placeholder="0.00"
                        style="
                            width:100%;box-sizing:border-box;
                            padding:7px 8px 7px 34px;
                            border-radius:7px;
                            border:1.5px solid rgba(255,255,255,0.15);
                            background:#1a1f2e;
                            color:#fff;
                            font-size:13px;
                            outline:none;
                        "
                        onfocus="this.style.borderColor='#6366f1'"
                        onblur="this.style.borderColor='rgba(255,255,255,0.15)'"
                    >
                </div>
                <input type="number" min="0"
                    id="cp-stock-${cId}" value="${existStock}" placeholder="0"
                    style="
                        width:100%;box-sizing:border-box;
                        padding:7px 10px;
                        border-radius:7px;
                        border:1.5px solid rgba(255,255,255,0.15);
                        background:#1a1f2e;
                        color:#fff;
                        font-size:13px;
                        outline:none;
                    "
                    onfocus="this.style.borderColor='#10b981'"
                    onblur="this.style.borderColor='rgba(255,255,255,0.15)'"
                >
            </div>`;
        }).join('');

        container.innerHTML = `
            <!-- Quick-fill bar -->
            <div style="
                display:grid;grid-template-columns:1fr 150px 110px;gap:10px;align-items:center;
                padding:10px 12px;border-radius:8px;
                background:linear-gradient(135deg,rgba(99,102,241,0.15),rgba(16,185,129,0.1));
                border:1px solid rgba(99,102,241,0.3);
                margin-bottom:10px;
            ">
                <div style="font-size:12px;font-weight:600;color:#a5b4fc;">
                    ⚡ Quick Fill All Countries
                </div>
                <input type="number" step="0.01" min="0" id="qf-price" placeholder="Set all prices…"
                    style="width:100%;box-sizing:border-box;padding:6px 10px;border-radius:7px;
                           border:1.5px solid #6366f1;background:#1a1f2e;color:#fff;font-size:13px;outline:none;"
                    oninput="applyQuickFill('price', this.value)"
                >
                <input type="number" min="0" id="qf-stock" placeholder="Set all stock…"
                    style="width:100%;box-sizing:border-box;padding:6px 10px;border-radius:7px;
                           border:1.5px solid #10b981;background:#1a1f2e;color:#fff;font-size:13px;outline:none;"
                    oninput="applyQuickFill('stock', this.value)"
                >
            </div>
            <!-- Column headers -->
            <div style="display:grid;grid-template-columns:1fr 150px 110px;gap:10px;padding:0 12px 4px;margin-bottom:4px;">
                <span style="font-size:10px;font-weight:700;color:#6b7280;text-transform:uppercase;letter-spacing:.06em;">Country</span>
                <span style="font-size:10px;font-weight:700;color:#6366f1;text-transform:uppercase;letter-spacing:.06em;">Price</span>
                <span style="font-size:10px;font-weight:700;color:#10b981;text-transform:uppercase;letter-spacing:.06em;">Stock</span>
            </div>
            <!-- Scrollable country rows -->
            <div style="max-height:240px;overflow-y:auto;padding-right:2px;">
                ${rowsHtml}
            </div>
        `;
    } catch (err) {
        container.innerHTML = `<div style="padding:12px;color:#f87171;font-size:13px;">⚠️ Could not load countries: ${err.message}</div>`;
    }
}

function applyQuickFill(field, value) {
    const countries = window._productCountries || [];
    countries.forEach(c => {
        const cId = c.id || c.Id;
        const el = document.getElementById(field === 'price' ? `cp-price-${cId}` : `cp-stock-${cId}`);
        if (el && value !== '') el.value = value;
    });
}


function closeAddProductModal() {
    document.getElementById('add-product-modal').style.display = 'none';
    _cachedCountries = null;
    window._productCountries = null;
}

async function saveProductForm() {
    const editId = document.getElementById('prod-edit-id').value;
    const nameEn = document.getElementById('prod-name-en').value.trim();
    const nameAr = document.getElementById('prod-name-ar').value.trim();
    const basePrice = parseFloat(document.getElementById('prod-price').value);
    const categoryId = document.getElementById('prod-category-select').value;
    const description = document.getElementById('prod-description').value.trim();

    if (!nameEn || !nameAr || isNaN(basePrice) || !categoryId) {
        showToast('error', 'Please fill in required fields (Names, Base Price, Category)');
        return;
    }

    try {
        await ensureAdminToken();
        let productId = editId;

        if (editId) {
            await apiRequest(`/products/${editId}`, {
                method: 'PUT',
                body: JSON.stringify({ categoryId, nameAr, nameEn, description, basePrice })
            });
            showToast('info', 'Product details updated...');
        } else {
            const created = await apiRequest('/products', {
                method: 'POST',
                body: JSON.stringify({ categoryId, nameAr, nameEn, description, basePrice })
            });
            productId = created.id || created.Id;
            showToast('info', 'Product created, saving prices...');
        }

        // Save per-country prices & inventory
        if (productId) {
            const countries = _cachedCountries || await apiRequest('/countries');
            let pricesSaved = 0;
            for (const c of (countries || [])) {
                const cId = c.id || c.Id;
                const priceEl = document.getElementById(`cp-price-${cId}`);
                const stockEl = document.getElementById(`cp-stock-${cId}`);
                const price = priceEl ? parseFloat(priceEl.value) : NaN;
                const stock = stockEl ? parseInt(stockEl.value) : NaN;

                if (!isNaN(price) && price >= 0) {
                    await apiRequest(`/products/${productId}/prices`, {
                        method: 'PUT',
                        body: JSON.stringify({ countryId: cId, price })
                    });
                    pricesSaved++;
                }
                if (!isNaN(stock) && stock >= 0) {
                    await apiRequest(`/products/${productId}/inventory`, {
                        method: 'PUT',
                        body: JSON.stringify({ countryId: cId, quantity: stock })
                    });
                }
            }
            showToast('success', `✅ Product saved with ${pricesSaved} country price(s)!`);
        }

        closeAddProductModal();
        loadProducts();
        loadOverviewStats();
    } catch (err) {
        showToast('error', `Save product failed: ${err.message}`);
    }
}

async function deleteProduct(id) {
    if (!id || id === 'undefined') {
        showToast('error', 'Invalid Product ID');
        return;
    }
    if (!confirm('Are you sure you want to delete this product?')) return;
    try {
        await ensureAdminToken();
        await apiRequest(`/products/${id}`, { method: 'DELETE' });
        showToast('success', 'Product deleted');
        loadProducts();
        loadOverviewStats();
    } catch (err) {
        showToast('error', `Delete failed: ${err.message}`);
    }
}

// --- CATEGORIES CRUD ---

async function loadCategories() {
    const container = document.getElementById('categories-list');
    if (!container) return;
    container.innerHTML = '<div class="loading-state">Fetching categories...</div>';

    try {
        const data = await apiRequest('/categories');
        allCategories = data || [];

        if (allCategories.length === 0) {
            container.innerHTML = '<div class="loading-state">📭 No categories yet. Create one!</div>';
            return;
        }

        container.innerHTML = allCategories.map(c => {
            const cId = c.id || c.Id;
            return `
                <div class="list-item">
                    <div>
                        <strong>${escapeHtml(c.nameAr || c.NameAr || '')}</strong> / ${escapeHtml(c.nameEn || c.NameEn || '')}
                        <div style="font-size:11px;color:var(--text-muted)">ID: ${cId}</div>
                    </div>
                    <button class="btn btn-xs btn-danger" onclick="deleteCategory('${cId}')">Delete</button>
                </div>
            `;
        }).join('');
    } catch (err) {
        container.innerHTML = `<div class="loading-state" style="color:var(--accent-rose)">❌ Failed to load categories: ${err.message}</div>`;
    }
}

async function createCategory() {
    const nameAr = document.getElementById('cat-name-ar').value.trim();
    const nameEn = document.getElementById('cat-name-en').value.trim();

    if (!nameAr || !nameEn) {
        showToast('error', 'Please enter Arabic and English category names');
        return;
    }

    try {
        await ensureAdminToken();
        await apiRequest('/categories', {
            method: 'POST',
            body: JSON.stringify({ nameAr, nameEn })
        });
        showToast('success', 'Category added successfully!');
        document.getElementById('cat-name-ar').value = '';
        document.getElementById('cat-name-en').value = '';
        loadCategories();
        loadCategoriesDropdown();
        loadOverviewStats();
    } catch (err) {
        showToast('error', `Failed to create category: ${err.message}`);
    }
}

async function deleteCategory(id) {
    if (!id || id === 'undefined') return;
    if (!confirm('Are you sure you want to delete this category?')) return;
    try {
        await ensureAdminToken();
        await apiRequest(`/categories/${id}`, { method: 'DELETE' });
        showToast('success', 'Category deleted');
        loadCategories();
        loadCategoriesDropdown();
        loadOverviewStats();
    } catch (err) {
        showToast('error', `Delete failed: ${err.message}`);
    }
}

async function loadCategoriesDropdown() {
    try {
        const data = await apiRequest('/categories');
        allCategories = data || [];

        const shopSelect = document.getElementById('shop-category-filter');
        const filterSelect = document.getElementById('product-category-filter');
        const modalSelect = document.getElementById('prod-category-select');

        const optionsHtml = allCategories.map(c => {
            const cId = c.id || c.Id;
            const nameEn = c.nameEn || c.NameEn || '';
            const nameAr = c.nameAr || c.NameAr || '';
            return `<option value="${cId}">${escapeHtml(nameEn)} (${escapeHtml(nameAr)})</option>`;
        }).join('');

        if (shopSelect) shopSelect.innerHTML = '<option value="">All Categories</option>' + optionsHtml;
        if (filterSelect) filterSelect.innerHTML = '<option value="">All Categories</option>' + optionsHtml;
        if (modalSelect) modalSelect.innerHTML = '<option value="">Select Category...</option>' + optionsHtml;
    } catch (err) {
        console.error('Failed to load categories dropdown', err);
    }
}

// --- ORDERS & INVOICES ---

async function loadOrders() {
    const tableContainer = document.getElementById('orders-list-table');
    if (!tableContainer) return;
    tableContainer.innerHTML = '<div class="loading-state">Fetching order records...</div>';

    try {
        await ensureAdminToken();
        const data = await apiRequest('/orders?page=1&pageSize=20');
        const orders = data.data || data || [];

        if (orders.length === 0) {
            tableContainer.innerHTML = '<div class="loading-state">📭 No orders found. Place an order in the Customer Shop!</div>';
            return;
        }

        tableContainer.innerHTML = `
            <table>
                <thead>
                    <tr>
                        <th>Order ID</th>
                        <th>Customer</th>
                        <th>Total</th>
                        <th>Status</th>
                        <th>Date</th>
                        <th>Actions / Invoices</th>
                    </tr>
                </thead>
                <tbody>
                    ${orders.map(o => {
                        const oId = o.id || o.Id;
                        return `
                            <tr>
                                <td><strong>#${oId ? oId.substring(0, 8) : 'N/A'}</strong></td>
                                <td>${escapeHtml(o.customerName || o.phone || 'Guest')}</td>
                                <td><strong>${o.totalAmount || 0} EGP</strong></td>
                                <td><span class="status-chip status-${(o.status || 'pending').toLowerCase()}">${o.status}</span></td>
                                <td>${o.createdAt ? new Date(o.createdAt).toLocaleDateString() : 'Recent'}</td>
                                <td>
                                    <div style="display:flex;gap:6px;">
                                        <button class="btn btn-xs btn-outline" onclick="downloadInvoicePdf('${oId}')" title="Download Backend PDF Invoice">📄 PDF Invoice</button>
                                        <select style="width:auto;padding:2px 6px;font-size:11px;" onchange="updateOrderStatus('${oId}', this.value)">
                                            <option value="">-- Change Status --</option>
                                            <option value="NewOrder">🆕 New Order</option>
                                            <option value="PendingApproval">⏳ Pending Approval</option>
                                            <option value="Prepared">📦 Prepared</option>
                                            <option value="Shipping">🚚 Shipping</option>
                                            <option value="Delivered">✅ Delivered</option>
                                            <option value="Cancelled">❌ Cancelled</option>
                                            <option value="Incomplete">⚠️ Incomplete</option>
                                        </select>
                                    </div>
                                </td>
                            </tr>
                        `;
                    }).join('')}
                </tbody>
            </table>
        `;
    } catch (err) {
        tableContainer.innerHTML = `<div class="loading-state" style="color:var(--accent-rose)">❌ Failed to load orders: ${err.message}</div>`;
    }
}

async function updateOrderStatus(orderId, status) {
    if (!status || !orderId || orderId === 'undefined') return;
    try {
        await ensureAdminToken();
        await apiRequest(`/orders/${orderId}/status`, {
            method: 'PATCH',
            body: JSON.stringify({ status })
        });
        showToast('success', `Order #${orderId.substring(0, 8)} status updated to ${status}`);
        loadOrders();
    } catch (err) {
        showToast('error', `Failed to update status: ${err.message}`);
    }
}

async function downloadInvoicePdf(orderId) {
    if (!orderId || orderId === 'undefined') {
        showToast('error', 'Invalid Order ID');
        return;
    }
    try {
        await ensureAdminToken();
        showToast('info', '📄 Generating invoice...');

        // Step 1: Create (or retrieve existing) invoice for this order
        let invoice;
        try {
            invoice = await apiRequest(`/invoices/${orderId}`, { method: 'POST' });
        } catch (createErr) {
            // If creation fails try fetching the existing one
            try {
                invoice = await apiRequest(`/invoices/by-order/${orderId}`);
            } catch (_) {
                throw createErr; // surface the original error
            }
        }

        const invoiceId = invoice.id || invoice.Id;
        if (!invoiceId) throw new Error('Could not get invoice ID from server');

        showToast('info', `📋 Invoice ${invoice.invoiceNumber || invoice.InvoiceNumber} ready — downloading PDF...`);

        // Step 2: Fetch the PDF blob using the invoice ID
        const blob = await apiRequest(`/invoices/${invoiceId}/pdf`);

        if (blob instanceof Blob) {
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `Invoice-${invoice.invoiceNumber || invoice.InvoiceNumber || invoiceId.substring(0, 8)}.pdf`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            window.URL.revokeObjectURL(url);
            showToast('success', '✅ Invoice PDF downloaded!');
        } else {
            showToast('error', 'Server did not return a PDF file');
        }
    } catch (err) {
        showToast('error', `Invoice failed: ${err.message}`);
    }
}


// --- OFFERS CRUD ---

async function loadOffers() {
    const container = document.getElementById('offers-list');
    if (!container) return;
    container.innerHTML = '<div class="loading-state">Fetching active offers...</div>';

    try {
        const data = await apiRequest('/offers?activeOnly=true');
        const offers = data || [];

        if (offers.length === 0) {
            container.innerHTML = '<div class="loading-state">📭 No active offers currently running.</div>';
            return;
        }

        container.innerHTML = offers.map(o => {
            const oId = o.id || o.Id;
            return `
                <div class="list-item">
                    <div>
                        <strong>🎯 ${escapeHtml(o.productName || 'Product Deal')}</strong>
                        <div style="font-size:12px;color:var(--accent-rose);font-weight:700;">${o.discountPercent}% OFF</div>
                        <div style="font-size:11px;color:var(--text-muted)">Valid: ${new Date(o.startDate).toLocaleDateString()} - ${new Date(o.endDate).toLocaleDateString()}</div>
                    </div>
                    <button class="btn btn-xs btn-danger" onclick="deleteOffer('${oId}')">Remove</button>
                </div>
            `;
        }).join('');
    } catch (err) {
        container.innerHTML = `<div class="loading-state" style="color:var(--accent-rose)">❌ Failed to load offers: ${err.message}</div>`;
    }
}

async function loadProductsForOfferSelect() {
    try {
        const data = await apiRequest('/products?page=1&pageSize=100');
        const prods = data.data || data || [];
        const select = document.getElementById('offer-product-select');
        if (select) select.innerHTML = '<option value="">Select Product...</option>' + prods.map(p => {
            const pId = p.id || p.Id;
            const pName = p.nameEn || p.NameEn;
            const pPrice = p.price || p.Price || p.basePrice || p.BasePrice;
            return `<option value="${pId}">${escapeHtml(pName)} (${pPrice} EGP)</option>`;
        }).join('');
    } catch (err) {
        console.error('Failed to load products for offer select', err);
    }
}

async function createOffer() {
    const productId = document.getElementById('offer-product-select').value;
    const discountPercent = parseInt(document.getElementById('offer-discount').value);
    const startDate = document.getElementById('offer-start-date').value;
    const endDate = document.getElementById('offer-end-date').value;

    if (!productId || isNaN(discountPercent) || !startDate || !endDate) {
        showToast('error', 'Please fill in all offer details');
        return;
    }

    try {
        await ensureAdminToken();
        await apiRequest('/offers', {
            method: 'POST',
            body: JSON.stringify({ productId, discountPercent, startDate, endDate })
        });
        showToast('success', 'Offer launched successfully!');
        loadOffers();
        loadOverviewStats();
    } catch (err) {
        showToast('error', `Failed to create offer: ${err.message}`);
    }
}

async function deleteOffer(id) {
    if (!id || id === 'undefined') return;
    if (!confirm('Deactivate this offer?')) return;
    try {
        await ensureAdminToken();
        await apiRequest(`/offers/${id}`, { method: 'DELETE' });
        showToast('success', 'Offer deleted');
        loadOffers();
        loadOverviewStats();
    } catch (err) {
        showToast('error', `Delete failed: ${err.message}`);
    }
}

// --- STAFF CRUD ---

async function loadStaff() {
    const tableContainer = document.getElementById('staff-list-table');
    if (!tableContainer) return;
    tableContainer.innerHTML = '<div class="loading-state">Fetching staff roster...</div>';

    try {
        await ensureAdminToken();
        const data = await apiRequest('/users/staff?page=1&pageSize=50');
        const staff = data.data || data || [];

        if (staff.length === 0) {
            tableContainer.innerHTML = '<div class="loading-state">📭 No staff members found. Add one above!</div>';
            return;
        }

        tableContainer.innerHTML = `
            <table>
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Email</th>
                        <th>Role</th>
                        <th>Country</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    ${staff.map(s => {
                        const sId = s.id || s.Id;
                        return `
                            <tr>
                                <td><strong>${escapeHtml(s.name || s.Name)}</strong></td>
                                <td>${escapeHtml(s.email || s.Email)}</td>
                                <td><span class="badge badge-${s.role ? s.role.toLowerCase() : 'employee'}">${s.role}</span></td>
                                <td>${escapeHtml(s.country || s.Country || 'N/A')}</td>
                                <td>
                                    <div style="display:flex;gap:6px;">
                                        <button class="btn btn-xs btn-outline" onclick="toggleStaffStatus('${sId}')">Toggle Status</button>
                                        <button class="btn btn-xs btn-danger" onclick="deleteUser('${sId}')">Delete</button>
                                    </div>
                                </td>
                            </tr>
                        `;
                    }).join('')}
                </tbody>
            </table>
        `;
    } catch (err) {
        tableContainer.innerHTML = `<div class="loading-state" style="color:var(--accent-rose)">❌ Staff load failed: ${err.message}</div>`;
    }
}

async function createManager() {
    const name = document.getElementById('mgr-name').value.trim();
    const email = document.getElementById('mgr-email').value.trim();
    const phone = document.getElementById('mgr-phone').value.trim();
    const password = document.getElementById('mgr-password').value;
    const countryName = document.getElementById('mgr-country').value.trim() || 'Egypt';

    if (!name || !email || !password) {
        showToast('error', 'Please provide Name, Email, and Password');
        return;
    }

    try {
        await ensureAdminToken();
        await apiRequest('/users/admin/create-manager', {
            method: 'POST',
            body: JSON.stringify({ name, email, phone, password, countryName })
        });
        showToast('success', `Store Manager '${name}' created!`);
        document.getElementById('mgr-name').value = '';
        document.getElementById('mgr-email').value = '';
        document.getElementById('mgr-phone').value = '';
        document.getElementById('mgr-password').value = '';
        loadStaff();
        loadOverviewStats();
    } catch (err) {
        showToast('error', `Failed to create manager: ${err.message}`);
    }
}

async function createEmployee() {
    const name = document.getElementById('emp-name').value.trim();
    const email = document.getElementById('emp-email').value.trim();
    const phone = document.getElementById('emp-phone').value.trim();
    const password = document.getElementById('emp-password').value;
    const countryName = document.getElementById('emp-country').value.trim() || 'Egypt';

    if (!name || !email || !password) {
        showToast('error', 'Please provide Name, Email, and Password');
        return;
    }

    try {
        await ensureAdminToken();
        await apiRequest('/users/staff/create-employee', {
            method: 'POST',
            body: JSON.stringify({ name, email, phone, password, countryName })
        });
        showToast('success', `Sales Employee '${name}' created!`);
        document.getElementById('emp-name').value = '';
        document.getElementById('emp-email').value = '';
        document.getElementById('emp-phone').value = '';
        document.getElementById('emp-password').value = '';
        loadStaff();
        loadOverviewStats();
    } catch (err) {
        showToast('error', `Failed to create employee: ${err.message}`);
    }
}

async function toggleStaffStatus(id) {
    if (!id || id === 'undefined') return;
    try {
        await ensureAdminToken();
        await apiRequest(`/users/staff/${id}/toggle-status`, { method: 'PATCH' });
        showToast('success', 'Staff status toggled');
        loadStaff();
    } catch (err) {
        showToast('error', `Failed: ${err.message}`);
    }
}

async function deleteUser(id) {
    if (!id || id === 'undefined') return;
    if (!confirm('Are you sure you want to delete this staff user?')) return;
    try {
        await ensureAdminToken();
        await apiRequest(`/users/admin/${id}`, { method: 'DELETE' });
        showToast('success', 'User deleted');
        loadStaff();
        loadOverviewStats();
    } catch (err) {
        showToast('error', `Delete user failed: ${err.message}`);
    }
}

// --- SITE VISITS ---

async function loadSiteVisits() {
    const tableContainer = document.getElementById('site-visits-table');
    if (!tableContainer) return;
    tableContainer.innerHTML = '<div class="loading-state">Fetching traffic logs...</div>';

    try {
        const [visitsData, countData] = await Promise.all([
            apiRequest('/site-visits?page=1&pageSize=20'),
            apiRequest('/site-visits/today-count')
        ]);

        document.getElementById('analytics-today-count').textContent = countData.todayCount || 0;

        const visits = visitsData.data || visitsData || [];
        if (visits.length === 0) {
            tableContainer.innerHTML = '<div class="loading-state">📭 No visit records logged yet.</div>';
            return;
        }

        tableContainer.innerHTML = `
            <table>
                <thead>
                    <tr>
                        <th>Country</th>
                        <th>Page / Endpoint</th>
                        <th>Visited At</th>
                    </tr>
                </thead>
                <tbody>
                    ${visits.map(v => `
                        <tr>
                            <td><strong>🌍 ${escapeHtml(v.countryName || 'Global')}</strong></td>
                            <td><code>${escapeHtml(v.page || '/')}</code></td>
                            <td>${new Date(v.visitedAt).toLocaleString()}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        `;
    } catch (err) {
        tableContainer.innerHTML = `<div class="loading-state" style="color:var(--accent-rose)">❌ Failed to load visits: ${err.message}</div>`;
    }
}

// ============================================================
// TOAST NOTIFICATIONS
// ============================================================

function showToast(type, message) {
    const container = document.getElementById('toast-container');
    if (!container) return;
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;

    let icon = 'ℹ️';
    if (type === 'success') icon = '✅';
    if (type === 'error') icon = '❌';

    toast.innerHTML = `<span>${icon}</span> <span>${escapeHtml(message)}</span>`;
    container.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(50px)';
        toast.style.transition = 'all 0.4s ease';
        setTimeout(() => toast.remove(), 400);
    }, 4000);
}
