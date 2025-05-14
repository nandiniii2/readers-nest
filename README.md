# Readers Nest – Full-Stack Library Management System (Angular 15)

**Readers Nest** is a fully functional Library Management System developed using **Angular 15** for the frontend, with a connected backend API and database. It supports user authentication, role-based access control, and core library operations — all wrapped in a modern, book-inspired UI.

The project was built alongside a reference tutorial video, with custom enhancements in styling, theming, and UI layout.

---

## Core Functionalities

### 1. User Authentication System
- New users can **register** with a valid email and password.
- Existing users can **log in** to securely access their library account.
- The login state is persistent using **JWT tokens**, stored in the browser.

### 2. JWT-Based Authentication
- Authenticated sessions use **JSON Web Tokens** to authorize requests.
- Tokens are stored locally to maintain user state even after page reloads.

### 3. Role-Based Authorization
- **Admin Role:**
  - Full access to all system features
  - Manage users, books, and categories
  - View all orders and apply restrictions
- **User Role:**
  - View available books
  - Place orders and view personal history
  - Return books and monitor fines

### 4. Book & Category Management
- Admin can:
  - Add or delete **book records**
  - Create and manage **book categories**
  - Track inventory and usage

### 5. User Blocking/Disabling
- Admin has the ability to:
  - **Block** or **unblock** any registered user
  - **Disable** accounts for violation or inactivity
  - Enforce stricter control over user access

### 6. Fine Calculation System
- The system automatically calculates a **fine** if a user fails to return a book by the due date.
- Users can view their outstanding fine directly in the UI.
