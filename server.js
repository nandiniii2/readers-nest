const express = require('express');
const sqlite3 = require('sqlite3').verbose();
const jwt = require('jsonwebtoken');
const cors = require('cors');
const path = require('path');
const bcrypt = require('bcryptjs');
const fs = require('fs');

const app = express();
const PORT = process.env.PORT || 3000;
const SECRET_KEY = process.env.SECRET_KEY || 'super_secret_jwt_key_readers_nest_dev';

app.use(cors());
app.use(express.json());

// Initialize SQLite DB
const dbFile = path.resolve(__dirname, 'database.sqlite');
const db = new sqlite3.Database(dbFile);

db.serialize(() => {
    // Users Table
    db.run(`CREATE TABLE IF NOT EXISTS Users (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        firstName TEXT,
        lastName TEXT,
        email TEXT UNIQUE,
        mobile TEXT,
        password TEXT,
        blocked BOOLEAN DEFAULT 0,
        active BOOLEAN DEFAULT 1,
        createdOn DATETIME DEFAULT CURRENT_TIMESTAMP,
        fine REAL DEFAULT 0,
        userType TEXT DEFAULT 'USER'
    )`);

    // Categories Table
    db.run(`CREATE TABLE IF NOT EXISTS Categories (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        category TEXT,
        subCategory TEXT
    )`);

    // Books Table
    db.run(`CREATE TABLE IF NOT EXISTS Books (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        title TEXT,
        author TEXT,
        price REAL,
        categoryId INTEGER,
        FOREIGN KEY(categoryId) REFERENCES Categories(id)
    )`);

    // Orders Table
    db.run(`CREATE TABLE IF NOT EXISTS Orders (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        userId INTEGER,
        bookId INTEGER,
        orderDate DATETIME DEFAULT CURRENT_TIMESTAMP,
        returned BOOLEAN DEFAULT 0,
        returnDate DATETIME,
        finePaid REAL DEFAULT 0,
        FOREIGN KEY(userId) REFERENCES Users(id),
        FOREIGN KEY(bookId) REFERENCES Books(id)
    )`);

    // Seed default Admin User if not exists
    db.get('SELECT * FROM Users WHERE email = ?', ['admin@readersnest.com'], (err, row) => {
        if (!err && !row) {
            const hash = bcrypt.hashSync('Admin123!', 8);
            db.run(`INSERT INTO Users (firstName, lastName, email, mobile, password, userType) 
                    VALUES ('System', 'Admin', 'admin@readersnest.com', '0000000000', ?, 'ADMIN')`, [hash]);
        }
    });
});

// API Routes
const apiRouter = express.Router();

// 1. CreateAccount
apiRouter.post('/CreateAccount', (req, res) => {
    const { firstName, lastName, email, mobile, password } = req.body;
    const hash = bcrypt.hashSync(password, 8);
    db.run(`INSERT INTO Users (firstName, lastName, email, mobile, password) VALUES (?, ?, ?, ?, ?)`,
        [firstName, lastName, email, mobile, hash],
        function(err) {
            if (err) return res.status(400).send("Email already exists.");
            res.send("Account Created Successfully");
        }
    );
});

// 2. Login
apiRouter.get('/Login', (req, res) => {
    const { email, password } = req.query;
    db.get(`SELECT * FROM Users WHERE email = ?`, [email], (err, user) => {
        if (err || !user) return res.status(400).send("Invalid credentials.");
        if (user.blocked) return res.status(403).send("Account blocked.");
        
        if (!bcrypt.compareSync(password, user.password)) {
            return res.status(400).send("Invalid credentials.");
        }

        const token = jwt.sign({
            id: user.id,
            firstName: user.firstName,
            lastName: user.lastName,
            email: user.email,
            mobile: user.mobile,
            blocked: user.blocked ? 'true' : 'false',
            active: user.active ? 'true' : 'false',
            createdAt: user.createdOn,
            userType: user.userType
        }, SECRET_KEY, { expiresIn: '2h' });

        res.send(token);
    });
});

// 3. GetAllBooks
apiRouter.get('/GetAllBooks', (req, res) => {
    const query = `
        SELECT b.*, 
               CASE WHEN o.id IS NULL OR o.returned = 1 THEN 1 ELSE 0 END as available
        FROM Books b
        LEFT JOIN Orders o ON b.id = o.bookId AND o.returned = 0
    `;
    db.all(query, [], (err, books) => {
        res.json(books || []);
    });
});

// 4. GetAllCategories
apiRouter.get('/GetAllCategories', (req, res) => {
    db.all('SELECT * FROM Categories', [], (err, rows) => {
        res.json(rows || []);
    });
});

// 5. InsertCategory
apiRouter.post('/InsertCategory', (req, res) => {
    const { category, subCategory } = req.body;
    db.run('INSERT INTO Categories (category, subCategory) VALUES (?, ?)', [category, subCategory], function(err) {
        if (err) return res.status(500).send(err.message);
        res.send("Category inserted");
    });
});

// 6. InsertBook
apiRouter.post('/InsertBook', (req, res) => {
    const { title, author, price, categoryId } = req.body;
    db.run('INSERT INTO Books (title, author, price, categoryId) VALUES (?, ?, ?, ?)', [title, author, price, categoryId], function(err) {
        if (err) return res.status(500).send(err.message);
        res.send("Book inserted");
    });
});

// 7. DeleteBook
apiRouter.delete('/DeleteBook/:id', (req, res) => {
    db.run('DELETE FROM Books WHERE id = ?', [req.params.id], function(err) {
        if (err) return res.status(500).send(err.message);
        res.send("Book deleted");
    });
});

// 8. OrderBook
apiRouter.get('/OrderBook/:userId/:bookId', (req, res) => {
    db.run('INSERT INTO Orders (userId, bookId) VALUES (?, ?)', [req.params.userId, req.params.bookId], function(err) {
        if (err) return res.status(500).send(err.message);
        res.send("success");
    });
});

// 9. GetOrders / GetAllOrders
apiRouter.get('/GetOrders/:userId', (req, res) => {
    const query = `SELECT o.*, b.title, b.author FROM Orders o JOIN Books b ON o.bookId = b.id WHERE o.userId = ?`;
    db.all(query, [req.params.userId], (err, rows) => {
        res.json(rows || []);
    });
});

apiRouter.get('/GetAllOrders', (req, res) => {
    const query = `SELECT o.*, b.title, b.author, u.firstName, u.lastName FROM Orders o JOIN Books b ON o.bookId = b.id JOIN Users u ON o.userId = u.id`;
    db.all(query, [], (err, rows) => {
        res.json(rows || []);
    });
});

// 10. ReturnBook
apiRouter.get('/ReturnBook/:bookId/:userId', (req, res) => {
    const { bookId, userId } = req.params;
    db.run('UPDATE Orders SET returned = 1, returnDate = CURRENT_TIMESTAMP WHERE bookId = ? AND userId = ? AND returned = 0', 
    [bookId, userId], function(err) {
        if (err) return res.status(500).send(err.message);
        res.send("success");
    });
});

// 11. User Management
apiRouter.get('/GetAllUsers', (req, res) => {
    db.all('SELECT * FROM Users', [], (err, rows) => res.json(rows || []));
});

apiRouter.get('/ChangeBlockStatus/:status/:id', (req, res) => {
    db.run('UPDATE Users SET blocked = ? WHERE id = ?', [req.params.status, req.params.id], function(err) {
        res.send("success");
    });
});

apiRouter.get('/ChangeEnableStatus/:status/:id', (req, res) => {
    db.run('UPDATE Users SET active = ? WHERE id = ?', [req.params.status, req.params.id], function(err) {
        res.send("success");
    });
});

app.use('/api/Library', apiRouter);

// Serve Angular static files from the build directory
const angularDist = path.join(__dirname, 'dist', 'ui');
if (fs.existsSync(angularDist)) {
    app.use(express.static(angularDist));
    app.get('*', (req, res) => {
        res.sendFile(path.join(angularDist, 'index.html'));
    });
} else {
    console.warn("WARN: Angular dist directory not found. Please run 'npm run build' to generate static files.");
    app.get('/', (req, res) => res.send('API running, but Angular UI is not built. Run npm run build.'));
}

app.listen(PORT, () => {
    console.log(`Server running on port ${PORT}`);
});
