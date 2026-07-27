// ============================================================
// LoxxKing Admin Dashboard - Main JavaScript
// ============================================================

const API_BASE = 'https://loxxking-backend-production.up.railway.app/api';

let currentToken = null;
let currentUser = null;

// ============================================================
// AUTHENTICATION
// ============================================================

async function login() {
    const email = document.getElementById('login-email').value;
    const password = document.getElementById('login-password').value;
    const messageEl = document.getElementById('login-message');

    try {
        const response = await fetch(`${API_BASE}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.message || 'Login failed');
        }

        currentToken = data.accessToken;
        currentUser = data.user;

        localStorage.setItem('token', currentToken);
        localStorage.setItem('user', JSON.stringify(currentUser));

        document.getElementById('login-section').style.display = 'none';
        document.getElementById('dashboard').style.display = 'block';
        document.getElementById('user-info').textContent = `👋 ${currentUser.name} (${currentUser.role})`;
        document.getElementById('logout-btn').style.display = 'inline-block';
        document.getElementById('login-message').innerHTML = '';

        showToast('success', `مرحباً ${currentUser.name}!`);
        loadInitialData();

    } catch (error) {
        messageEl.innerHTML = `<div class="message error">❌ ${error.message}</div>`;
    }
}

function logout() {
    currentToken = null;
    currentUser = null;
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    document.getElementById('login-section').style.display = 'block';
    document.getElementById('dashboard').style.display = 'none';
    document.getElementById('user-info').textContent = 'غير مسجل الدخول';
    document.getElementById('logout-btn').style.display = 'none';
    showToast('info', 'تم تسجيل الخروج');
}

function checkAuth() {
    const token = localStorage.getItem('token');
    const user = localStorage.getItem('user');
    if (token && user) {
        currentToken = token;
        currentUser = JSON.parse(user);
        document.getElementById('login-section').style.display = 'none';
        document.getElementById('dashboard').style.display = 'block';
        document.getElementById('user-info').textContent = `👋 ${currentUser.name} (${currentUser.role})`;
        document.getElementById('logout-btn').style.display = 'inline-block';
        loadInitialData();
    }
}

// ============================================================
// API HELPERS
// ============================================================

async function apiRequest(endpoint, options = {}) {
    const defaultOptions = {
        headers: {
            'Content-Type': 'application/json',
            ...(currentToken ? { 'Authorization': `Bearer ${currentToken}` } : {})
        }
    };

    const config = {
        ...defaultOptions,
        ...options,
        headers: { ...defaultOptions.headers, ...options.headers }
    };

    try {
        const response = await fetch(`${API_BASE}${endpoint}`, config);
        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.message || 'Request failed');
        }

        return data;
    } catch (error) {
        showToast('error', error.message);
        throw error;
    }
}

// ============================================================
// TOAST NOTIFICATIONS
// ============================================================

function showToast(type, message) {
    const container = document.getElementById('message-container');
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.textContent = message;
    container.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transition = 'opacity 0.5s';
        setTimeout(() => toast.remove(), 500);
    }, 4000);
}

// ============================================================
// TAB MANAGEMENT
// ============================================================

function showTab(tabName) {
    document.querySelectorAll('.tab-pane').forEach(el => el.classList.remove('active'));
    document.querySelectorAll('.tab-btn').forEach(el => el.classList.remove('active'));

    document.getElementById(`tab-${tabName}`).classList.add('active');
    document.querySelector(`.tab-btn[onclick="showTab('${tabName}')"]`).classList.add('active');

    switch(tabName) {
        case 'users': loadUsers(); break;
        case 'categories': loadCategories(); break;
        case 'products': loadProducts(); break;
        case 'offers': loadOffers(); break;
        case 'orders': loadOrders(); break;
        case 'site-visits': loadSiteVisits(); loadTodayCount(); break;
        case 'staff': loadStaff(); break;
    }
}

// ============================================================
// LOAD FUNCTIONS
// ============================================================

function loadInitialData() {
    loadUsers();
    loadCategories();
}

async function loadUsers() {
    const container = document.getElementById('users-list');
    container.innerHTML = '<div class="loading">جاري التحميل...</div>';

    try {
        const data = await apiRequest('/auth/me');
        container.innerHTML = `
            <div class="data-item">
                <strong>👤 ${data.name}</strong><br>
                📧 ${data.email}<br>
                📱 ${data.phone}<br>
                🏷️ <span class="badge badge-${data.role.toLowerCase()}">${data.role}</span><br>
                🌍 ${data.country}
            </div>
        `;
    } catch (error) {
        container.innerHTML = `<div class="message error">❌ فشل تحميل المستخدمين</div>`;
    }
}

async function loadCategories() {
    const container = document.getElementById('categories-list');
    container.innerHTML = '<div class="loading">جاري التحميل...</div>';

    try {
        const data = await apiRequest('/categories');
        if (data.length === 0) {
            container.innerHTML = '<div class="data-item">📭 لا توجد تصنيفات</div>';
            return;
        }
        container.innerHTML = data.map(c => `
            <div class="data-item">
                <strong>${c.nameAr}</strong> / ${c.nameEn}
                <span style="color:#999;font-size:12px;"> (${c.id})</span>
            </div>
        `).join('');
    } catch (error) {
        container.innerHTML = `<div class="message error">❌ فشل تحميل التصنيفات</div>`;
    }
}

async function createCategory() {
    const nameAr = document.getElementById('category-name-ar').value;
    const nameEn = document.getElementById('category-name-en').value;

    if (!nameAr || !nameEn) {
        showToast('error', 'الرجاء إدخال الاسم بالعربية والإنجليزية');
        return;
    }

    try {
        await apiRequest('/categories', {
            method: 'POST',
            body: JSON.stringify({ nameAr, nameEn })
        });
        showToast('success', '✅ تم إضافة التصنيف بنجاح');
        document.getElementById('category-name-ar').value = '';
        document.getElementById('category-name-en').value = '';
        loadCategories();
    } catch (error) {
        showToast('error', '❌ فشل إضافة التصنيف');
    }
}

async function loadProducts() {
    const container = document.getElementById('products-list');
    container.innerHTML = '<div class="loading">جاري التحميل...</div>';

    try {
        const data = await apiRequest('/products?page=1&pageSize=10');
        if (!data.data || data.data.length === 0) {
            container.innerHTML = '<div class="data-item">📭 لا توجد منتجات</div>';
            return;
        }
        container.innerHTML = data.data.map(p => `
            <div class="data-item">
                <strong>${p.nameEn}</strong> / ${p.nameAr}<br>
                💰 ${p.price || p.basePrice} EGP | 📦 ${p.stock || 0} متاح
            </div>
        `).join('');
    } catch (error) {
        container.innerHTML = `<div class="message error">❌ فشل تحميل المنتجات</div>`;
    }
}

async function loadOffers() {
    const container = document.getElementById('offers-list');
    container.innerHTML = '<div class="loading">جاري التحميل...</div>';

    try {
        const data = await apiRequest('/offers?activeOnly=true');
        if (data.length === 0) {
            container.innerHTML = '<div class="data-item">📭 لا توجد عروض نشطة</div>';
            return;
        }
        container.innerHTML = data.map(o => `
            <div class="data-item">
                <strong>🎯 ${o.productName}</strong><br>
                خصم: ${o.discountPercent}% | من ${new Date(o.startDate).toLocaleDateString()} إلى ${new Date(o.endDate).toLocaleDateString()}
            </div>
        `).join('');
    } catch (error) {
        container.innerHTML = `<div class="message error">❌ فشل تحميل العروض</div>`;
    }
}

async function loadOrders() {
    const container = document.getElementById('orders-list');
    container.innerHTML = '<div class="loading">جاري التحميل...</div>';

    try {
        const data = await apiRequest('/orders?page=1&pageSize=10');
        if (!data.data || data.data.length === 0) {
            container.innerHTML = '<div class="data-item">📭 لا توجد طلبات</div>';
            return;
        }
        container.innerHTML = data.data.map(o => `
            <div class="data-item">
                <strong>#${o.id.substring(0,8)}</strong> - ${o.customerName}<br>
                💰 ${o.totalAmount} EGP | 📦 ${o.status} | 📱 ${o.phone}
            </div>
        `).join('');
    } catch (error) {
        container.innerHTML = `<div class="message error">❌ فشل تحميل الطلبات</div>`;
    }
}

async function loadSiteVisits() {
    const container = document.getElementById('site-visits-list');
    container.innerHTML = '<div class="loading">جاري التحميل...</div>';

    try {
        const data = await apiRequest('/site-visits?page=1&pageSize=10');
        if (!data.data || data.data.length === 0) {
            container.innerHTML = '<div class="data-item">📭 لا توجد زيارات</div>';
            return;
        }
        container.innerHTML = data.data.map(v => `
            <div class="data-item">
                🌍 ${v.countryName} | 📄 ${v.page}<br>
                🕐 ${new Date(v.visitedAt).toLocaleString()}
            </div>
        `).join('');
    } catch (error) {
        container.innerHTML = `<div class="message error">❌ فشل تحميل الزيارات</div>`;
    }
}

async function loadTodayCount() {
    try {
        const data = await apiRequest('/site-visits/today-count');
        document.getElementById('today-count').textContent = data.todayCount || 0;
    } catch (error) {
        document.getElementById('today-count').textContent = '❌';
    }
}

async function loadStaff() {
    const container = document.getElementById('staff-list');
    container.innerHTML = '<div class="loading">جاري التحميل...</div>';

    try {
        const data = await apiRequest('/users/staff?page=1&pageSize=20');
        if (!data.data || data.data.length === 0) {
            container.innerHTML = '<div class="data-item">📭 لا يوجد موظفين</div>';
            return;
        }
        container.innerHTML = data.data.map(s => `
            <div class="data-item">
                <strong>${s.name}</strong> (${s.role})<br>
                📧 ${s.email} | 📱 ${s.phone} | 🌍 ${s.country}
            </div>
        `).join('');
    } catch (error) {
        container.innerHTML = `<div class="message error">❌ فشل تحميل الموظفين</div>`;
    }
}

async function createManager() {
    const name = document.getElementById('manager-name').value;
    const email = document.getElementById('manager-email').value;
    const phone = document.getElementById('manager-phone').value;
    const password = document.getElementById('manager-password').value;
    const countryName = document.getElementById('manager-country').value || 'Egypt';

    if (!name || !email || !phone || !password) {
        showToast('error', 'الرجاء ملء جميع الحقول');
        return;
    }

    try {
        await apiRequest('/users/admin/create-manager', {
            method: 'POST',
            body: JSON.stringify({ name, email, phone, password, countryName })
        });
        showToast('success', '✅ تم إضافة مدير المتجر بنجاح');
        document.getElementById('manager-name').value = '';
        document.getElementById('manager-email').value = '';
        document.getElementById('manager-phone').value = '';
        document.getElementById('manager-password').value = '';
        loadStaff();
    } catch (error) {
        showToast('error', '❌ فشل إضافة مدير المتجر');
    }
}

async function createEmployee() {
    const name = document.getElementById('employee-name').value;
    const email = document.getElementById('employee-email').value;
    const phone = document.getElementById('employee-phone').value;
    const password = document.getElementById('employee-password').value;
    const countryName = document.getElementById('employee-country').value || 'Egypt';

    if (!name || !email || !phone || !password) {
        showToast('error', 'الرجاء ملء جميع الحقول');
        return;
    }

    try {
        await apiRequest('/users/staff/create-employee', {
            method: 'POST',
            body: JSON.stringify({ name, email, phone, password, countryName })
        });
        showToast('success', '✅ تم إضافة موظف المبيعات بنجاح');
        document.getElementById('employee-name').value = '';
        document.getElementById('employee-email').value = '';
        document.getElementById('employee-phone').value = '';
        document.getElementById('employee-password').value = '';
        loadStaff();
    } catch (error) {
        showToast('error', '❌ فشل إضافة موظف المبيعات');
    }
}

document.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
        const loginSection = document.getElementById('login-section');
        if (loginSection.style.display !== 'none') {
            login();
        }
    }
});

// Init
checkAuth();
