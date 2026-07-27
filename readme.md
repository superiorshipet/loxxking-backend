# 🏆 LoxxKing API - Backend

A modern e-commerce backend API built with **.NET 10**, **Clean Architecture**, and **PostgreSQL**. This API powers the LoxxKing e-commerce platform with features like user management, product catalog, order processing, and staff role management.

---

## 📋 **Table of Contents**

- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Setup & Installation](#setup--installation)
- [Configuration](#configuration)
- [API Endpoints](#api-endpoints)
- [Authentication](#authentication)
- [Postman Collection](#postman-collection)
- [Frontend Integration Guide](#frontend-integration-guide)
- [Database Schema](#database-schema)
- [Testing](#testing)
- [Deployment](#deployment)

---

## 🛠️ **Tech Stack**

| Layer | Technology |
|-------|-----------|
| **Framework** | .NET 10 (ASP.NET Core Web API) |
| **Database** | PostgreSQL 15+ |
| **ORM** | Entity Framework Core 10 |
| **Cache** | Redis (StackExchange.Redis) |
| **Auth** | JWT Bearer Tokens |
| **File Storage** | Cloudinary |
| **PDF Generation** | QuestPDF |
| **Logging** | Serilog |
| **Architecture** | Clean Architecture (Domain, Application, Infrastructure, Api) |

---

## 📁 **Project Structure**

src/
├── Domain/ # Enterprise-wide business rules & entities
│ ├── Entities/ # Core business entities
│ ├── Enums/ # Enumerations (UserRole, OrderStatus, etc.)
│ └── Domain.csproj
│
├── Application/ # Application business logic & interfaces
│ ├── Common/
│ │ └── Interfaces/ # Repository & service interfaces
│ └── Application.csproj
│
├── Infrastructure/ # External concerns (DB, File Storage, etc.)
│ ├── Persistence/ # DbContext & Migrations
│ ├── Repositories/ # Repository implementations
│ ├── Services/ # External services (Cloudinary, PDF, JWT)
│ └── Infrastructure.csproj
│
├── Api/ # API Layer (Controllers, DTOs, Middleware)
│ ├── Controllers/ # API Endpoints
│ ├── DTOs/ # Request/Response DTOs
│ ├── Middlewares/ # Global Exception Handling
│ ├── Properties/ # Launch settings
│ ├── appsettings.json # Configuration
│ └── Api.csproj
│
├── Tests/ # Unit & Integration Tests
│ └── Api.Tests/
│ └── Controllers/ # Controller tests
│
└── LoxxKingApi.sln # Solution file

---

## ⚙️ **Prerequisites**

- **.NET 10 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **PostgreSQL 15+** - [Download](https://www.postgresql.org/download/)
- **Redis** - [Download](https://redis.io/download/) (for caching)
- **Git** - [Download](https://git-scm.com/downloads)

---

## 🚀 **Setup & Installation**

### 1️⃣ **Clone the Repository**

```bash
git clone https://github.com/yourusername/loxxking-backend.git
cd loxxking-backend


2️⃣ Configure appsettings.json

Create appsettings.Development.json in src/Api/:
json

{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=loxxking_db;Username=your_user;Password=your_password",
    "RedisConnection": "localhost:6379"
  },
  "Jwt": {
    "Secret": "YOUR_SUPER_SECRET_KEY_32_BYTES_MINIMUM",
    "Issuer": "LoxxKingApi",
    "Audience": "LoxxKingClient"
  },
  "Cloudinary": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret"
  },
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id",
      "ClientSecret": "your-google-client-secret"
    }
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "http://localhost:3000",
      "https://your-frontend-domain.com"
    ]
  }
}

sudo systemctl start postgresql
# or using Docker:
docker run -d --name postgres-loxx -p 5432:5432 -e POSTGRES_PASSWORD=your_password postgres:15
3️⃣ Setup Database & Redis
sudo systemctl start redis-server
# or using Docker:
docker run -d --name redis-loxx -p 6379:6379 redis
4️⃣ Run Migrations
dotnet ef database update --project src/Infrastructure --startup-project src/Api
5️⃣ Run the API
dotnet run --project src/Api/Api.csproj

The API will be available at: http://localhost:5196

🔑 Default Admin Credentials

After running migrations, the system automatically seeds a default admin:
Field	Value
Email	admin@loxxking.com
Password	Admin@123456
Role	Admin
📡 API Endpoints
🔐 Authentication
Method	Endpoint	Description	Auth
POST	/api/auth/login	Login user	❌
POST	/api/auth/register	Register new customer	❌
GET	/api/auth/me	Get current user	✅
GET	/api/auth/countries	Get all countries	❌
👥 User Management (Staff)
Method	Endpoint	Description	Auth
POST	/api/users/admin/create-manager	Create Store Manager	Admin
POST	/api/users/staff/create-employee	Create Sales Employee	Admin/Manager
GET	/api/users/staff	Get all staff members	Admin/Manager
PATCH	/api/users/staff/{id}/toggle-status	Activate/Deactivate staff	Admin/Manager
DELETE	/api/users/admin/{id}	Delete user	Admin
POST	/api/users/change-password	Change own password	Any
POST	/api/users/staff/reset-password	Reset staff password	Admin/Manager
POST	/api/users/admin/change-password	Change any user password	Admin
📦 Products
Method	Endpoint	Description	Auth
GET	/api/products	Get all products (paginated)	❌
GET	/api/products/{id}	Get product by ID	❌
POST	/api/products	Create product	Admin/Manager
PUT	/api/products/{id}	Update product	Admin/Manager
DELETE	/api/products/{id}	Delete product	Admin/Manager
PUT	/api/products/{id}/prices	Update product price	Admin/Manager
PUT	/api/products/{id}/inventory	Update inventory	Admin/Manager
📂 Categories
Method	Endpoint	Description	Auth
GET	/api/categories	Get all categories (cached)	❌
POST	/api/categories	Create category	Admin/Manager
PUT	/api/categories/{id}	Update category	Admin/Manager
DELETE	/api/categories/{id}	Delete category	Admin/Manager
🎯 Offers
Method	Endpoint	Description	Auth
GET	/api/offers?activeOnly=true	Get active offers (cached)	❌
GET	/api/offers/{id}	Get offer by ID	❌
POST	/api/offers	Create offer	Admin/Manager
PUT	/api/offers/{id}	Update offer	Admin/Manager
DELETE	/api/offers/{id}	Delete offer	Admin/Manager
📋 Orders
Method	Endpoint	Description	Auth
GET	/api/orders	Get orders (filtered)	Admin/Manager
GET	/api/orders/{id}	Get order details	Any
POST	/api/orders	Create order	Customer
PATCH	/api/orders/{id}/status	Update order status	Admin/Manager
PUT	/api/orders/{id}	Update order	Admin
📄 Invoices
Method	Endpoint	Description	Auth
GET	/api/invoices	Get all invoices	Admin/Manager
GET	/api/invoices/{id}	Get invoice by ID	Any
POST	/api/invoices	Create invoice	Admin/Manager
GET	/api/invoices/{id}/pdf	Download PDF invoice	Any
🏦 Bank Transfers
Method	Endpoint	Description	Auth
POST	/api/bank-transfers	Upload transfer proof	Customer
GET	/api/bank-transfers/order/{orderId}	Get transfer by order	Any
GET	/api/bank-transfers/pending	Get pending transfers	Admin/Manager
PATCH	/api/bank-transfers/{id}/review	Approve/Reject transfer	Admin/Manager
💬 Support Chat
Method	Endpoint	Description	Auth
GET	/api/support-chat/messages/{conversationId}	Get conversation messages	Any
POST	/api/support-chat/send	Send message	Any
POST	/api/support-chat/conversation	Create conversation	Any
🔔 Notifications
Method	Endpoint	Description	Auth
GET	/api/notifications	Get user notifications	Any
GET	/api/notifications/unread-count	Get unread count	Any
PATCH	/api/notifications/{id}/read	Mark as read	Any
PATCH	/api/notifications/read-all	Mark all as read	Any
DELETE	/api/notifications/{id}	Delete notification	Any
📊 Site Visits
Method	Endpoint	Description	Auth
POST	/api/site-visits	Track site visit	❌
GET	/api/site-visits/today-count	Today's visit count	Admin/Manager
GET	/api/site-visits	Get visits (filtered)	Admin/Manager
🔐 Authentication
Login Flow
POST /api/auth/login
{
  "email": "user@example.com",
  "password": "your_password"
}
Response
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "generated-refresh-token",
  "expiresAt": "2025-01-01T12:00:00Z",
  "user": {
    "id": "uuid",
    "name": "John Doe",
    "email": "user@example.com",
    "phone": "01000000000",
    "role": "Admin",
    "country": "Egypt",
    "createdAt": "2025-01-01T12:00:00Z"
  }
}

🚀 Frontend Integration Guide
📡 Base URL
http://localhost:5196/api

🔑 Authentication Headers
// Add token to all requests
const headers = {
  'Authorization': `Bearer ${token}`,
  'Content-Type': 'application/json',
};
📝 Important Notes for Frontend
1️⃣ CORS Configuration

Make sure to add your frontend URL to Cors:AllowedOrigins in appsettings.json:


"Cors": {
  "AllowedOrigins": [
    "http://localhost:5173",  // Vite
    "http://localhost:3000",   // React/Next
    "https://your-production-domain.com"
  ]
}
2️⃣ File Uploads

For endpoints with file uploads (like bank transfers):

const formData = new FormData();
formData.append('orderId', orderId);
formData.append('proofImage', file);

fetch('http://localhost:5196/api/bank-transfers', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
  },
  body: formData
});

3️⃣ Pagination

Most list endpoints support pagination:

// Query parameters
{
  page: 1,        // Default: 1
  pageSize: 20,   // Default: 10-20
  search: 'keyword',
  status: 'Pending',
  countryId: 'uuid',
  dateFrom: '2025-01-01',
  dateTo: '2025-01-31'
}

4️⃣ Response Format

// Paginated response
{
  data: [...],
  totalCount: 100,
  page: 1,
  pageSize: 20,
  totalPages: 5
}

// Single item
{
  id: "uuid",
  name: "Product Name",
  // ... other fields
}

// Error response
{
  message: "Error description"
}

5️⃣ Error Handling

Always handle these HTTP status codes:

    200: Success

    400: Bad Request (validation error)

    401: Unauthorized (token expired/invalid)

    403: Forbidden (insufficient permissions)

    404: Not Found

    429: Too Many Requests (rate limiting)

    500: Internal Server Error

fetch('/api/endpoint')
  .then(res => {
    if (res.status === 401) {
      // Redirect to login
      window.location.href = '/login';
    }
    return res.json();
  })
  .then(data => {
    // Handle data
  })
  .catch(error => {
    // Handle network errors
  });