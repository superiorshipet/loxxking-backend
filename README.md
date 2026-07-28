# LoxxKing E-Commerce Backend Documentation

## Overview
LoxxKing is a modern e-commerce platform backend built with ASP.NET Core. It provides a robust, highly scalable RESTful API supporting a rich feature set including product management, order processing, wishlist handling (for both guests and authenticated users), and dynamic settings configuration. The architecture is designed to seamlessly integrate with a Vanilla JavaScript frontend administration panel.

## Architecture & Technology Stack
- **Framework:** ASP.NET Core 10.0 Web API
- **Database:** PostgreSQL (Relational Data)
- **ORM:** Entity Framework Core
- **Caching:** Redis (Distributed Caching for high performance)
- **Media Storage:** Cloudinary
- **Authentication:** JWT (JSON Web Tokens) with Role-Based Access Control (RBAC) and Google OAuth integration
- **External Integrations:** GreenAPI (WhatsApp Messaging), SMTP (Email Notifications)

## Core Features & Modules

### 1. Product & Category Management
- **CRUD Operations:** Complete lifecycle management for products and categories.
- **Image Handling:** Direct integration with Cloudinary for uploading, storing, and serving optimized product images.
- **Pagination & Filtering:** Optimized endpoints returning paginated sets and allowing category-based filtering.

### 2. Wishlist System (Hybrid Approach)
- **Guest Support:** Allows unauthenticated users to maintain a wishlist using a unique `guestId` stored in local storage on the frontend.
- **Authenticated Sync:** Designed to support merging guest wishlists when a user successfully registers or logs in.
- **Database Structure:** Tracks `UserId` or `GuestId` seamlessly via EF Core.

### 3. Order Processing & Notifications
- **Checkout Flow:** Captures user details, calculates totals, and validates product availability.
- **Smart Phone Number Formatting:** Automatically detects the user's selected country from a predefined database of 17 supported countries (including Turkey, Egypt, Saudi Arabia, UAE, etc.) and injects the proper country dial code (e.g., stripping local leading zeros and prepending `90` or `966`) to ensure delivery.
- **Multi-Channel Notifications:**
  - **WhatsApp:** Integrates with GreenAPI to send instant order receipts and notifications to both the customer and the business administration number.
  - **Email:** Uses SMTP (via MailKit/MimeKit) to dispatch order details to a designated business email.

### 4. Dynamic Application Settings (Hot-Reload)
- **Settings Controller:** Exposes `GET /api/settings/notifications` and `PUT /api/settings/notifications` to allow administrators to modify environment parameters directly from the frontend UI.
- **Live Updates:** Modifies `appsettings.json` safely. ASP.NET Core's `IOptionsMonitor` automatically detects file changes and reloads configuration in-memory, applying new Email addresses, GreenAPI tokens, and WhatsApp routing numbers instantly without requiring a server restart.
- **Security:** Sensitive tokens and passwords are never returned in the `GET` request to prevent exposure in the browser console.

### 5. Security & Rate Limiting
- **CORS Configuration:** Strictly defined origins for frontend applications (localhost environments, Vercel, Railway).
- **Rate Limiting:** Global rate limiting is enforced to prevent DDoS attacks and spam. Configured to allow 1000 requests per minute per IP address, using a Fixed Window algorithm.
- **Authorization:** Controller endpoints are protected using the `[Authorize]` attribute, with specific administrative endpoints restricted via `[Authorize(Roles = "Admin")]`.

## Frontend Integration (Admin Dashboard)
The backend is accompanied by a frontend administrative dashboard (`frontend/index.html` & `frontend/app.js`).
- **Tab-Based Navigation:** Vanilla JavaScript handles asynchronous data fetching and DOM manipulation for Overview, Products, Orders, and Settings.
- **Settings Management:** Administrators can update receiving emails, WhatsApp numbers, GreenAPI instance details, and SMTP credentials. The frontend safely handles these updates, masking secure fields and providing instant visual feedback.

## Configuration (appsettings.json)
The application relies on `appsettings.json` for environment configuration. Key sections include:
- `ConnectionStrings`: PostgreSQL and Redis connections.
- `Jwt`: Secret keys and issuer strings for token generation.
- `Cloudinary`: API keys for image management.
- `Notifications`: 
  - `BusinessEmail`: Destination for admin order emails.
  - `WhatsAppPhone`: Destination for admin WhatsApp alerts.
  - `GreenApiInstanceId` & `GreenApiToken`: Credentials for the WhatsApp sender bot.
  - `Smtp`: Host, Port, Username, and Password for the email sender.

## Deployment Considerations
- Ensure the PostgreSQL database is migrated (`dotnet ef database update`).
- Verify Redis is running and accessible.
- Ensure the `appsettings.json` file is writable by the application process if dynamic settings modifications via the Admin Dashboard are required in production environments.
