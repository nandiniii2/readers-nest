using API.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace API.DataAccess
{
    public class SqliteBoolHandler : SqlMapper.TypeHandler<bool>
    {
        public override void SetValue(System.Data.IDbDataParameter parameter, bool value)
        {
            parameter.Value = value ? 1 : 0;
        }

        public override bool Parse(object value)
        {
            if (value == null || value is DBNull) return false;
            return Convert.ToInt64(value) == 1;
        }
    }

    public class DataAccess : IDataAccess
    {
        private readonly IConfiguration configuration;
        private readonly string DbConnection;

        public DataAccess(IConfiguration _configuration)
        {
            SqlMapper.AddTypeHandler(new SqliteBoolHandler());
            configuration = _configuration;
            DbConnection = configuration["connectionStrings:DBConnect"] ?? "";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(DbConnection))
            {
                var sql = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FirstName TEXT,
                        LastName TEXT,
                        Email TEXT UNIQUE,
                        Mobile TEXT,
                        Password TEXT,
                        Blocked INTEGER DEFAULT 0,
                        Active INTEGER DEFAULT 1,
                        CreatedOn DATETIME DEFAULT CURRENT_TIMESTAMP,
                        Fine REAL DEFAULT 0,
                        UserType TEXT DEFAULT 'USER'
                    );
                    CREATE TABLE IF NOT EXISTS BookCategories (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Category TEXT,
                        SubCategory TEXT
                    );
                    CREATE TABLE IF NOT EXISTS Books (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT,
                        Author TEXT,
                        Price REAL,
                        Ordered INTEGER DEFAULT 0,
                        CategoryId INTEGER,
                        FOREIGN KEY(CategoryId) REFERENCES BookCategories(Id)
                    );
                    CREATE TABLE IF NOT EXISTS Orders (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER,
                        BookId INTEGER,
                        OrderedOn DATETIME DEFAULT CURRENT_TIMESTAMP,
                        Returned INTEGER DEFAULT 0,
                        ReturnDate DATETIME,
                        FinePaid REAL DEFAULT 0,
                        FOREIGN KEY(UserId) REFERENCES Users(Id),
                        FOREIGN KEY(BookId) REFERENCES Books(Id)
                    );
                ";
                connection.Execute(sql);
                
                var adminExists = connection.ExecuteScalar<int>("SELECT COUNT(1) FROM Users WHERE Email='admin@readersnest.com'");
                if (adminExists == 0) {
                    connection.Execute("INSERT INTO Users (FirstName, LastName, Email, Mobile, Password, UserType) VALUES ('System', 'Admin', 'admin@readersnest.com', '0000000000', 'Admin', 'ADMIN')");
                }
            }
        }

        public int CreateUser(User user)
        {
            var result = 0;
            using (var connection = new SqliteConnection(DbConnection))
            {
                var parameters = new
                {
                    fn = user.FirstName,
                    ln = user.LastName,
                    em = user.Email,
                    mb = user.Mobile,
                    pwd = user.Password,
                    blk = user.Blocked,
                    act = user.Active,
                    con = user.CreatedOn,
                    type = user.UserType.ToString()
                };
                var sql = "insert into Users (FirstName, LastName, Email, Mobile, Password, Blocked, Active, CreatedOn, UserType) values (@fn, @ln, @em, @mb, @pwd, @blk, @act, @con, @type);";
                result = connection.Execute(sql, parameters);
            }
            return result;
        }

        public bool IsEmailAvailable(string email)
        {
            var result = false;

            using (var connection = new SqliteConnection(DbConnection))
            {
                result = connection.ExecuteScalar<bool>("select count(*) from Users where Email=@email;", new { email });
            }

            return !result;
        }

        public bool AuthenticateUser(string email, string password, out User? user)
        {
            var result = false;
            using (var connection = new SqliteConnection(DbConnection))
            {
                result = connection.ExecuteScalar<bool>("select count(1) from Users where email=@email and password=@password;", new { email, password });
                if (result)
                {
                    user = connection.QueryFirst<User>("select * from Users where email=@email;", new { email });
                }
                else
                {
                    user = null;
                }
            }
            return result;
        }

        public IList<Book> GetAllBooks()
        {
            IEnumerable<Book> books = null;
            using (var connection = new SqliteConnection(DbConnection))
            {
                var sql = "select * from Books;";
                books = connection.Query<Book>(sql);

                foreach (var book in books)
                {
                    sql = "select * from BookCategories where Id=" + book.CategoryId;
                    book.Category = connection.QuerySingle<BookCategory>(sql);
                }
            }
            return books.ToList();
        }

        public bool OrderBook(int userId, int bookId)
        {
            var ordered = false;

            using (var connection = new SqliteConnection(DbConnection))
            {
                var sql = $"insert into Orders (UserId, BookId, OrderedOn, Returned) values ({userId}, {bookId}, '{DateTime.Now:yyyy-MM-dd HH:mm:ss}', 0);";
                var inserted = connection.Execute(sql) == 1;
                if (inserted)
                {
                    sql = $"update Books set Ordered=1 where Id={bookId}";
                    var updated = connection.Execute(sql) == 1;
                    ordered = updated;
                }
            }

            return ordered;
        }

        public IList<Order> GetOrdersOfUser(int userId)
        {
            IEnumerable<Order> orders;
            using (var connection = new SqliteConnection(DbConnection))
            {
                var sql = @"
                    select 
                        o.Id, 
                        u.Id as UserId, CONCAT(u.FirstName, ' ', u.LastName) as Name,
                        o.BookId as BookId, b.Title as BookName,
                        o.OrderedOn as OrderDate, o.Returned as Returned
                    from Users u LEFT JOIN Orders o ON u.Id=o.UserId
                    LEFT JOIN Books b ON o.BookId=b.Id
                    where o.UserId IN (@Id);
                ";
                orders = connection.Query<Order>(sql, new { Id = userId });
            }
            return orders.ToList();
        }

        public IList<Order> GetAllOrders()
        {
            IEnumerable<Order> orders;
            using (var connection = new SqliteConnection(DbConnection))
            {
                var sql = @"
                    select 
                        o.Id, 
                        u.Id as UserId, CONCAT(u.FirstName, ' ', u.LastName) as Name,
                        o.BookId as BookId, b.Title as BookName,
                        o.OrderedOn as OrderDate, o.Returned as Returned
                    from Users u LEFT JOIN Orders o ON u.Id=o.UserId
                    LEFT JOIN Books b ON o.BookId=b.Id
                    where o.Id IS NOT NULL;
                ";
                orders = connection.Query<Order>(sql);
            }
            return orders.ToList();
        }

        public bool ReturnBook(int userId, int bookId)
        {
            var returned = false;
            using (var connection = new SqliteConnection(DbConnection))
            {
                var sql = $"update Books set Ordered=0 where Id={bookId};";
                connection.Execute(sql);
                sql = $"update Orders set Returned=1 where UserId={userId} and BookId={bookId};";
                returned = connection.Execute(sql) == 1;
            }
            return returned;
        }

        public IList<User> GetUsers()
        {
            IEnumerable<User> users;
            using (var connection = new SqliteConnection(DbConnection))
            {
                users = connection.Query<User>("select * from Users;");

                var listOfOrders =
                    connection.Query("select u.Id as UserId, o.BookId as BookId, o.OrderedOn as OrderDate, o.Returned as Returned from Users u LEFT JOIN Orders o ON u.Id=o.UserId;");

                foreach (var user in users)
                {
                    var orders = listOfOrders.Where(lo => lo.UserId == user.Id).ToList();
                    var fine = 0;
                    foreach (var order in orders)
                    {
                        if (order.BookId != null && order.Returned != null && order.Returned == false)
                        {
                            var orderDate = order.OrderDate;
                            var maxDate = orderDate.AddDays(10);
                            var currentDate = DateTime.Now;

                            var extraDays = (currentDate - maxDate).Days;
                            extraDays = extraDays < 0 ? 0 : extraDays;

                            fine = extraDays * 50;
                            user.Fine += fine;
                        }
                    }
                }
            }
            return users.ToList();
        }

        public void BlockUser(int userId)
        {
            using var connection = new SqliteConnection(DbConnection);
            connection.Execute("update Users set Blocked=1 where Id=@Id", new { Id = userId });
        }

        public void UnblockUser(int userId)
        {
            using var connection = new SqliteConnection(DbConnection);
            connection.Execute("update Users set Blocked=0 where Id=@Id", new { Id = userId });
        }

        public void ActivateUser(int userId)
        {
            using var connection = new SqliteConnection(DbConnection);
            connection.Execute("update Users set Active=1 where Id=@Id", new { Id = userId });
        }

        public void DeactivateUser(int userId)
        {
            using var connection = new SqliteConnection(DbConnection);
            connection.Execute("update Users set Active=0 where Id=@Id", new { Id = userId });
        }

        public IList<BookCategory> GetAllCategories()
        {
            IEnumerable<BookCategory> categories;

            using (var connection = new SqliteConnection(DbConnection))
            {
                categories = connection.Query<BookCategory>("select * from BookCategories;");
            }

            return categories.ToList();
        }

        public void InsertNewBook(Book book)
        {
            using var conn = new SqliteConnection(DbConnection);
            var sql = "select Id from BookCategories where Category=@cat and SubCategory=@subcat";
            var parameter1 = new
            {
                cat = book.Category.Category,
                subcat = book.Category.SubCategory
            };
            var categoryId = conn.ExecuteScalar<int>(sql, parameter1);

            sql = "insert into Books (Title, Author, Price, Ordered, CategoryId) values (@title, @author, @price, @ordered, @catid);";
            var parameter2 = new
            {
                title = book.Title,
                author = book.Author,
                price = book.Price,
                ordered = false,
                catid = categoryId
            };
            conn.Execute(sql, parameter2);
        }

        public bool DeleteBook(int bookId)
        {
            var deleted = false;
            using (var connection = new SqliteConnection(DbConnection))
            {
                var sql = $"delete from Books where Id={bookId}";
                deleted = connection.Execute(sql) == 1;
            }
            return deleted;
        }

        public void CreateCategory(BookCategory bookCategory)
        {
            using var connection = new SqliteConnection(DbConnection);
            var parameter = new
            {
                cat = bookCategory.Category,
                subcat = bookCategory.SubCategory
            };
            connection.Execute("insert into BookCategories (category, subcategory) values (@cat, @subcat);", parameter);
        }
    }
}
