using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;
using System.Collections.ObjectModel;
using System.Linq;

namespace FoodLy
{
    public partial class MainWindow : Window
    {
        private SQLiteConnection connection;
        private string currentUser;
        private string currentRole;
        private ObservableCollection<MenuItem> menuItems = new ObservableCollection<MenuItem>();
        private ObservableCollection<CartItem> cartItems = new ObservableCollection<CartItem>();
        private ObservableCollection<UserItem> adminUsers = new ObservableCollection<UserItem>();
        private ObservableCollection<OrderItem> adminOrders = new ObservableCollection<OrderItem>();
        private decimal totalPrice = 0;
        private int pointsToUse = 0;
        private int availablePoints = 0;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadMenu();
            CartListBox.ItemsSource = cartItems;
            TotalPriceTextBlock.Text = $"Итого: {totalPrice} руб.";
            LoadCategories();
        }

        private void InitializeDatabase()
        {
            connection = new SQLiteConnection("Data Source=foodly.db;Version=3;");
            connection.Open();

            // Создание таблицы Users с полем LoyaltyPoints
            string createUsersTable = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Login TEXT PRIMARY KEY,
                    Password TEXT,
                    Role TEXT,
                    FullName TEXT,
                    BirthDate TEXT,
                    PhoneNumber TEXT,
                    IsBlocked INTEGER DEFAULT 0,
                    LoyaltyPoints INTEGER DEFAULT 0
                )";
            using (var cmd = new SQLiteCommand(createUsersTable, connection))
                cmd.ExecuteNonQuery();

            // Создание таблицы Menu
            string createMenuTable = @"
                CREATE TABLE IF NOT EXISTS Menu (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT,
                    Price REAL,
                    Ingredients TEXT,
                    Calories INTEGER,
                    Category TEXT
                )";
            using (var cmd = new SQLiteCommand(createMenuTable, connection))
                cmd.ExecuteNonQuery();

            // Создание таблицы Orders
            string createOrdersTable = @"
                CREATE TABLE IF NOT EXISTS Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserLogin TEXT,
                    ItemName TEXT,
                    Quantity INTEGER,
                    Street TEXT,
                    Apartment TEXT,
                    Entrance TEXT,
                    Floor TEXT,
                    PhoneNumber TEXT,
                    PaymentMethod TEXT,
                    Status TEXT,
                    CourierLogin TEXT,
                    OrderDate TEXT,
                    DeliveryTime TEXT,
                    LoyaltyPointsUsed INTEGER DEFAULT 0
                )";
            using (var cmd = new SQLiteCommand(createOrdersTable, connection))
                cmd.ExecuteNonQuery();

            // Добавление тестовых блюд
            string insertMenu = @"
                INSERT OR IGNORE INTO Menu (Name, Price, Ingredients, Calories, Category) VALUES 
                ('Салат Цезарь', 350.0, 'Курица, салат романо, пармезан, гренки, соус Цезарь', 450, 'Салаты'),
                ('Греческий салат', 300.0, 'Огурцы, помидоры, фета, оливки, красный лук, оливковое масло', 320, 'Салаты'),
                ('Фруктовый смузи', 200.0, 'Банан, клубника, яблоко, йогурт, мед', 250, 'Напитки'),
                ('Киноа с овощами', 250.0, 'Киноа, брокколи, морковь, перец, оливковое масло', 300, 'Основные блюда'),
                ('Запеченный лосось', 400.0, 'Лосось, лимон, шпинат, чеснок, оливковое масло', 350, 'Основные блюда'),
                ('Овощной суп', 180.0, 'Цветная капуста, морковь, сельдерей, лук, бульон', 150, 'Супы'),
                ('Чечевичный суп', 200.0, 'Чечевица, томаты, морковь, специи', 220, 'Супы'),
                ('Тунец с киноа', 320.0, 'Тунец, киноа, авокадо, лимонный сок', 280, 'Основные блюда'),
                ('Овсяная каша с ягодами', 150.0, 'Овёс, черника, мёд, миндальное молоко', 200, 'Завтраки'),
                ('Греческий йогурт с орехами', 180.0, 'Йогурт, грецкие орехи, мёд, семена чиа', 250, 'Завтраки'),
                ('Томатный суп с базиликом', 190.0, 'Помидоры, базилик, сливки, чеснок', 160, 'Супы'),
                ('Куриный суп с лапшой', 220.0, 'Курица, лапша, морковь, петрушка', 240, 'Супы'),
                ('Грибной крем-суп', 200.0, 'Шампиньоны, сливки, лук, тимьян', 180, 'Супы'),
                ('Рыба на гриле', 380.0, 'Треска, лимон, розмарин, оливковое масло', 320, 'Основные блюда'),
                ('Овощное рагу', 230.0, 'Баклажаны, кабачки, томаты, специи', 270, 'Основные блюда'),
                ('Ягодный смузи', 210.0, 'Малина, черника, банан, йогурт', 260, 'Напитки'),
                ('Зеленый смузи', 190.0, 'Шпинат, яблоко, огурец, имбирь', 230, 'Напитки'),
                ('Панкейки с медом', 170.0, 'Мука, яйца, молоко, мед', 300, 'Завтраки'),
                ('Творожная запеканка', 160.0, 'Творог, изюм, яйца, ваниль', 280, 'Завтраки'),
                ('Салат с авокадо', 340.0, 'Авокадо, креветки, руккола, лимон', 400, 'Салаты')";
            using (var cmd = new SQLiteCommand(insertMenu, connection))
                cmd.ExecuteNonQuery();
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        private void LoadCategories()
        {
            CategoryComboBox.SelectedIndex = 0;
            AdminMenuCategoryComboBox.SelectedIndex = 0;
        }

        private int GetUserLoyaltyPoints(string login)
        {
            string query = "SELECT LoyaltyPoints FROM Users WHERE Login = @login";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@login", login);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return Convert.ToInt32(reader["LoyaltyPoints"]);
                    }
                }
            }
            return 0;
        }

        private void UpdateLoyaltyPointsDisplay()
        {
            if (currentRole == "Пользователь")
            {
                availablePoints = GetUserLoyaltyPoints(currentUser);
                LoyaltyPointsTextBlock.Text = $"Баллы: {availablePoints}";
                LoyaltyPointsTextBlock.Visibility = Visibility.Visible;
                AvailablePointsTextBlock.Text = $"{availablePoints} баллов";
            }
            else
            {
                LoyaltyPointsTextBlock.Visibility = Visibility.Hidden;
            }
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedCategory = (CategoryComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            LoadMenu(selectedCategory == "Все" ? null : selectedCategory);
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text;
            string password = PasswordBox.Password;
            string role = (RoleComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string fullName = FullNameTextBox.Text;
            string birthDate = BirthDatePicker.Text;
            string phoneNumber = PhoneNumberTextBox.Text;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(role) ||
                string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(birthDate) || string.IsNullOrEmpty(phoneNumber))
            {
                MessageBox.Show("Заполните все поля!");
                return;
            }

            // Check if an administrator already exists
            string checkAdminQuery = "SELECT COUNT(*) FROM Users WHERE Role = 'Администратор'";
            using (var cmd = new SQLiteCommand(checkAdminQuery, connection))
            {
                int adminCount = Convert.ToInt32(cmd.ExecuteScalar());
                if (adminCount > 0 && role == "Администратор")
                {
                    MessageBox.Show("Регистрация нового администратора невозможна! Уже существует один администратор.");
                    return;
                }
            }

            string hashedPassword = HashPassword(password);
            string query = "INSERT INTO Users (Login, Password, Role, FullName, BirthDate, PhoneNumber, IsBlocked, LoyaltyPoints) VALUES (@login, @password, @role, @fullName, @birthDate, @phoneNumber, 0, 0)";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@login", login);
                cmd.Parameters.AddWithValue("@password", hashedPassword);
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@fullName", fullName);
                cmd.Parameters.AddWithValue("@birthDate", birthDate);
                cmd.Parameters.AddWithValue("@phoneNumber", phoneNumber);
                try
                {
                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Регистрация успешна!");
                    RegistrationFields.Visibility = Visibility.Collapsed;
                    LoginTextBox.Text = "";
                    PasswordBox.Password = "";
                    FullNameTextBox.Text = "";
                    BirthDatePicker.SelectedDate = null;
                    PhoneNumberTextBox.Text = "";
                    RoleComboBox.SelectedIndex = -1;
                }
                catch
                {
                    MessageBox.Show("Пользователь уже существует!");
                }
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text;
            string password = HashPassword(PasswordBox.Password);

            string query = "SELECT Role, IsBlocked FROM Users WHERE Login = @login AND Password = @password";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@login", login);
                cmd.Parameters.AddWithValue("@password", password);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        if (Convert.ToInt32(reader["IsBlocked"]) == 1)
                        {
                            MessageBox.Show("Ваш аккаунт заблокирован!");
                            return;
                        }

                        currentUser = login;
                        currentRole = reader["Role"].ToString();
                        UpdateLoyaltyPointsDisplay();
                        if (currentRole == "Пользователь")
                        {
                            UserMenuTab.Visibility = Visibility.Visible;
                            CartTab.Visibility = Visibility.Visible;
                            UserOrdersTab.Visibility = Visibility.Visible;
                            CourierOrdersTab.Visibility = Visibility.Hidden;
                            AdminPanelTab.Visibility = Visibility.Hidden;
                            LoadUserOrders();
                        }
                        else if (currentRole == "Курьер")
                        {
                            UserMenuTab.Visibility = Visibility.Hidden;
                            CartTab.Visibility = Visibility.Hidden;
                            UserOrdersTab.Visibility = Visibility.Hidden;
                            CourierOrdersTab.Visibility = Visibility.Visible;
                            AdminPanelTab.Visibility = Visibility.Hidden;
                            LoadOrders();
                        }
                        else if (currentRole == "Администратор")
                        {
                            UserMenuTab.Visibility = Visibility.Hidden;
                            CartTab.Visibility = Visibility.Hidden;
                            UserOrdersTab.Visibility = Visibility.Hidden;
                            CourierOrdersTab.Visibility = Visibility.Hidden;
                            AdminPanelTab.Visibility = Visibility.Visible;
                            LoadAdminMenu();
                            LoadAdminUsers();
                            LoadAdminOrders();
                        }
                        MessageBox.Show("Вход успешен!");
                    }
                    else
                    {
                        MessageBox.Show("Неверный логин или пароль!");
                    }
                }
            }
        }

        private void ShowRegistrationFields(object sender, RoutedEventArgs e)
        {
            RegistrationFields.Visibility = Visibility.Visible;
        }

        private void LoadMenu(string category = null)
        {
            MenuListBox.ItemsSource = menuItems;
            menuItems.Clear();
            string query = "SELECT Id, Name, Price, Ingredients, Calories, Category FROM Menu";
            if (!string.IsNullOrEmpty(category))
                query += " WHERE Category = @category";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                if (!string.IsNullOrEmpty(category))
                    cmd.Parameters.AddWithValue("@category", category);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        menuItems.Add(new MenuItem
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Name = reader["Name"].ToString(),
                            Price = Convert.ToDecimal(reader["Price"]),
                            ImagePath = $"/Images/{reader["Name"].ToString().Replace(" ", "_")}.jpg",
                            Ingredients = reader["Ingredients"].ToString(),
                            Calories = reader["Calories"].ToString() + " ккал",
                            Category = reader["Category"].ToString()
                        });
                    }
                }
            }
        }

        private void LoadOrders()
        {
            OrdersListBox.Items.Clear();
            string query = "SELECT o.Id, o.ItemName, o.UserLogin, o.Street, o.Apartment, o.Entrance, o.Floor, o.PhoneNumber, o.PaymentMethod, o.Status, o.CourierLogin, o.OrderDate, o.DeliveryTime, o.LoyaltyPointsUsed, u.PhoneNumber AS CourierPhone FROM Orders o LEFT JOIN Users u ON o.CourierLogin = u.Login WHERE o.Status IN ('Новый', 'В доставке') AND (o.CourierLogin = @courier OR o.CourierLogin IS NULL)";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@courier", currentUser);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string status = reader["Status"].ToString();
                        string courierInfo = reader["CourierLogin"] != DBNull.Value ? $"Курьер: {reader["CourierLogin"]}" : "Курьер не назначен";
                        string courierPhone = reader["CourierPhone"] != DBNull.Value ? $", Телефон курьера: {reader["CourierPhone"]}" : "";
                        string address = BuildAddress(reader["Street"], reader["Apartment"], reader["Entrance"], reader["Floor"]);
                        string phone = reader["PhoneNumber"] != DBNull.Value ? $", Телефон: {reader["PhoneNumber"]}" : "";
                        string orderDate = reader["OrderDate"] != DBNull.Value ? $", Дата заказа: {reader["OrderDate"]}" : "";
                        string deliveryTime = reader["DeliveryTime"] != DBNull.Value ? $", Время доставки: {reader["DeliveryTime"]}" : "";
                        string pointsUsed = reader["LoyaltyPointsUsed"] != DBNull.Value ? $", Использовано баллов: {reader["LoyaltyPointsUsed"]}" : "";
                        OrdersListBox.Items.Add($"Заказ #{reader["Id"]}: {reader["ItemName"]} от {reader["UserLogin"]}, Адрес: {address}{phone}, Оплата: {reader["PaymentMethod"]}, Статус: {status}{orderDate}{deliveryTime}{pointsUsed}, {courierInfo}{courierPhone}");
                    }
                }
            }
        }

        private void LoadUserOrders()
        {
            UserOrdersListBox.ItemsSource = null;
            var orders = new ObservableCollection<OrderItem>();
            string query = "SELECT Id, ItemName, Street, Apartment, Entrance, Floor, PhoneNumber, PaymentMethod, Status, CourierLogin, OrderDate, DeliveryTime, LoyaltyPointsUsed FROM Orders WHERE UserLogin = @user";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@user", currentUser);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string courierInfo = reader["CourierLogin"] != DBNull.Value ? $"{reader["CourierLogin"]}" : "Не назначен";
                        string address = BuildAddress(reader["Street"], reader["Apartment"], reader["Entrance"], reader["Floor"]);
                        string status = reader["Status"]?.ToString() ?? "Неизвестно";
                        double progressValue = GetProgressValue(status);
                        SolidColorBrush statusColor = GetStatusColor(status);
                        string pointsUsed = reader["LoyaltyPointsUsed"] != DBNull.Value ? $"Использовано баллов: {reader["LoyaltyPointsUsed"]}" : "";

                        orders.Add(new OrderItem
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            OrderSummary = $"Заказ #{reader["Id"]}: {reader["ItemName"]}",
                            Address = $"Адрес: {address}",
                            PhoneNumber = $"Телефон: {reader["PhoneNumber"]?.ToString()}",
                            PaymentMethod = $"Оплата: {reader["PaymentMethod"]?.ToString()}",
                            Status = $"Статус: {status}",
                            CourierInfo = $"Курьер: {courierInfo}",
                            OrderDate = $"Дата заказа: {reader["OrderDate"]?.ToString()}",
                            DeliveryTime = $"Время доставки: {reader["DeliveryTime"]?.ToString()}",
                            LoyaltyPointsUsed = pointsUsed,
                            ProgressValue = progressValue,
                            StatusColor = statusColor
                        });
                    }
                }
            }
            UserOrdersListBox.ItemsSource = orders;
        }

        private double GetProgressValue(string status)
        {
            switch (status)
            {
                case "Новый":
                    return 0.0;
                case "В доставке":
                    return 50.0;
                case "Доставлен":
                    return 100.0;
                case "Отменен":
                    return 0.0;
                default:
                    return 0.0;
            }
        }

        private SolidColorBrush GetStatusColor(string status)
        {
            switch (status)
            {
                case "Новый":
                    return new SolidColorBrush(Colors.Gray);
                case "В доставке":
                    return new SolidColorBrush(Colors.Orange);
                case "Доставлен":
                    return new SolidColorBrush(Colors.Green);
                case "Отменен":
                    return new SolidColorBrush(Colors.Red);
                default:
                    return new SolidColorBrush(Colors.Gray);
            }
        }

        private string BuildAddress(object street, object apartment, object entrance, object floor)
        {
            List<string> addressParts = new List<string>();
            if (street != DBNull.Value && !string.IsNullOrEmpty(street.ToString()))
                addressParts.Add(street.ToString());
            if (apartment != DBNull.Value && !string.IsNullOrEmpty(apartment.ToString()))
                addressParts.Add($"Кв. {apartment}");
            if (entrance != DBNull.Value && !string.IsNullOrEmpty(entrance.ToString()))
                addressParts.Add($"Подъезд {entrance}");
            if (floor != DBNull.Value && !string.IsNullOrEmpty(floor.ToString()))
                addressParts.Add($"Этаж {floor}");
            return string.Join(", ", addressParts);
        }

        private void ApplyPoints_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PointsToUseTextBox.Text) || !int.TryParse(PointsToUseTextBox.Text, out int points))
            {
                MessageBox.Show("Введите корректное количество баллов!");
                return;
            }

            availablePoints = GetUserLoyaltyPoints(currentUser);
            if (points < 0 || points > availablePoints)
            {
                MessageBox.Show($"Недостаточно баллов! Доступно: {availablePoints}");
                return;
            }

            if (points > totalPrice)
            {
                MessageBox.Show("Нельзя использовать баллов больше, чем сумма заказа!");
                return;
            }

            pointsToUse = points;
            TotalPriceTextBlock.Text = $"Итого: {totalPrice - pointsToUse} руб. (использовано {pointsToUse} баллов)";
            MessageBox.Show($"Применено {pointsToUse} баллов");
        }

        private void TakeOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (OrdersListBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите заказ!");
                return;
            }

            string selectedOrder = OrdersListBox.SelectedItem.ToString();
            int orderId = int.Parse(selectedOrder.Split('#')[1].Split(':')[0]);
            string query = "UPDATE Orders SET Status = 'В доставке', CourierLogin = @courier, DeliveryTime = @deliveryTime WHERE Id = @id AND Status = 'Новый'";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@courier", currentUser);
                cmd.Parameters.AddWithValue("@id", orderId);
                cmd.Parameters.AddWithValue("@deliveryTime", DateTime.Now.AddHours(1).ToString("HH:mm"));
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    MessageBox.Show("Заказ принят!");
                    LoadOrders();
                    LoadUserOrders();
                }
                else
                {
                    MessageBox.Show("Заказ уже взят или недоступен!");
                }
            }
        }

        private void ConfirmDeliveryButton_Click(object sender, RoutedEventArgs e)
        {
            if (OrdersListBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите заказ!");
                return;
            }

            string selectedOrder = OrdersListBox.SelectedItem.ToString();
            int orderId = int.Parse(selectedOrder.Split('#')[1].Split(':')[0]);
            string query = "UPDATE Orders SET Status = 'Доставлен' WHERE Id = @id AND Status = 'В доставке' AND CourierLogin = @courier";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@courier", currentUser);
                cmd.Parameters.AddWithValue("@id", orderId);
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    MessageBox.Show("Доставка подтверждена!");
                    LoadOrders();
                    LoadUserOrders();
                }
                else
                {
                    MessageBox.Show("Нельзя подтвердить доставку этого заказа!");
                }
            }
        }

        private void CancelOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserOrdersListBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите заказ!");
                return;
            }

            var selectedOrder = UserOrdersListBox.SelectedItem as OrderItem;
            int orderId = selectedOrder.Id;
            string query = "UPDATE Orders SET Status = 'Отменен' WHERE Id = @id AND Status = 'Новый' AND UserLogin = @user";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@user", currentUser);
                cmd.Parameters.AddWithValue("@id", orderId);
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    MessageBox.Show("Заказ отменен!");
                    LoadUserOrders();
                    LoadOrders();
                }
                else
                {
                    MessageBox.Show("Нельзя отменить этот заказ!");
                }
            }
        }

        private void MenuListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuListBox.SelectedItem == null && e.AddedItems.Count == 0)
            {
                IngredientsTextBlock.Text = "";
                CaloriesTextBlock.Text = "";
                return;
            }

            var selectedItem = e.AddedItems.Count > 0 ? e.AddedItems[e.AddedItems.Count - 1] as MenuItem : MenuListBox.SelectedItem as MenuItem;
            if (selectedItem != null)
            {
                IngredientsTextBlock.Text = selectedItem.Ingredients;
                CaloriesTextBlock.Text = selectedItem.Calories;
            }
        }

        private void CartIncreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var cartItem = button.Tag as CartItem;
            cartItem.Quantity++;
            totalPrice += cartItem.Price;
            TotalPriceTextBlock.Text = $"Итого: {totalPrice - pointsToUse} руб. {(pointsToUse > 0 ? $"(использовано {pointsToUse} баллов)" : "")}";
            CartListBox.ItemsSource = null;
            CartListBox.ItemsSource = cartItems;
        }

        private void CartDecreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var cartItem = button.Tag as CartItem;
            if (cartItem.Quantity > 1)
            {
                cartItem.Quantity--;
                totalPrice -= cartItem.Price;
            }
            else
            {
                cartItems.Remove(cartItem);
                totalPrice -= cartItem.Price;
            }
            TotalPriceTextBlock.Text = $"Итого: {totalPrice - pointsToUse} руб. {(pointsToUse > 0 ? $"(использовано {pointsToUse} баллов)" : "")}";
            CartListBox.ItemsSource = null;
            CartListBox.ItemsSource = cartItems;
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var cartItem = button.Tag as CartItem;
            totalPrice -= cartItem.TotalPrice;
            cartItems.Remove(cartItem);
            TotalPriceTextBlock.Text = $"Итого: {totalPrice - pointsToUse} руб. {(pointsToUse > 0 ? $"(использовано {pointsToUse} баллов)" : "")}";
            CartListBox.ItemsSource = null;
            CartListBox.ItemsSource = cartItems;
        }

        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (MenuListBox.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одно блюдо!");
                return;
            }

            foreach (var selectedItem in MenuListBox.SelectedItems)
            {
                var menuItem = selectedItem as MenuItem;
                if (menuItem == null) continue;

                int quantity = 1;
                var cartItem = cartItems.FirstOrDefault(ci => ci.Name == menuItem.Name);
                if (cartItem != null)
                {
                    cartItem.Quantity += quantity;
                }
                else
                {
                    cartItems.Add(new CartItem { Name = menuItem.Name, Price = menuItem.Price, Quantity = quantity });
                }

                totalPrice += menuItem.Price * quantity;
            }

            TotalPriceTextBlock.Text = $"Итого: {totalPrice - pointsToUse} руб. {(pointsToUse > 0 ? $"(использовано {pointsToUse} баллов)" : "")}";
            CartTab.Visibility = Visibility.Visible;
            MenuListBox.SelectedItems.Clear();
        }

        private void ClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (cartItems.Count == 0)
            {
                MessageBox.Show("Корзина уже пуста!");
                return;
            }

            var result = MessageBox.Show("Вы уверены, что хотите очистить корзину?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                cartItems.Clear();
                totalPrice = 0;
                pointsToUse = 0;
                PointsToUseTextBox.Text = "";
                TotalPriceTextBlock.Text = $"Итого: {totalPrice} руб.";
                CartListBox.ItemsSource = null;
                CartListBox.ItemsSource = cartItems;
                MessageBox.Show("Корзина очищена!");
            }
        }

        private void PlaceOrder_Click(object sender, RoutedEventArgs e)
        {
            if (cartItems.Count == 0)
            {
                MessageBox.Show("Корзина пуста!");
                return;
            }

            string street = CartStreetTextBox.Text;
            string apartment = CartApartmentTextBox.Text;
            string entrance = CartEntranceTextBox.Text;
            string floor = CartFloorTextBox.Text;
            string phoneNumber = CartPhoneNumberTextBox.Text;
            string paymentMethod = (CartPaymentMethodComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrEmpty(street) || string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(paymentMethod))
            {
                MessageBox.Show("Заполните обязательные поля: улица, номер телефона, способ оплаты!");
                return;
            }

            // Начисление баллов (1 балл за каждые 100 рублей)
            int pointsEarned = (int)(totalPrice / 100);
            string updatePointsQuery = "UPDATE Users SET LoyaltyPoints = LoyaltyPoints + @points WHERE Login = @login";
            using (var cmd = new SQLiteCommand(updatePointsQuery, connection))
            {
                cmd.Parameters.AddWithValue("@points", pointsEarned);
                cmd.Parameters.AddWithValue("@login", currentUser);
                cmd.ExecuteNonQuery();
            }

            // Списание использованных баллов
            if (pointsToUse > 0)
            {
                string deductPointsQuery = "UPDATE Users SET LoyaltyPoints = LoyaltyPoints - @points WHERE Login = @login";
                using (var cmd = new SQLiteCommand(deductPointsQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@points", pointsToUse);
                    cmd.Parameters.AddWithValue("@login", currentUser);
                    cmd.ExecuteNonQuery();
                }
            }

            string itemList = string.Join(", ", cartItems.Select(item => $"{item.Name} x{item.Quantity}"));
            string query = "INSERT INTO Orders (UserLogin, ItemName, Quantity, Street, Apartment, Entrance, Floor, PhoneNumber, PaymentMethod, Status, OrderDate, DeliveryTime, LoyaltyPointsUsed) VALUES (@user, @item, @quantity, @street, @apartment, @entrance, @floor, @phone, @payment, 'Новый', @orderDate, @deliveryTime, @pointsUsed)";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@user", currentUser);
                cmd.Parameters.AddWithValue("@item", itemList);
                cmd.Parameters.AddWithValue("@quantity", cartItems.Sum(item => item.Quantity));
                cmd.Parameters.AddWithValue("@street", street);
                cmd.Parameters.AddWithValue("@apartment", string.IsNullOrEmpty(apartment) ? DBNull.Value : apartment);
                cmd.Parameters.AddWithValue("@entrance", string.IsNullOrEmpty(entrance) ? DBNull.Value : entrance);
                cmd.Parameters.AddWithValue("@floor", string.IsNullOrEmpty(floor) ? DBNull.Value : floor);
                cmd.Parameters.AddWithValue("@phone", phoneNumber);
                cmd.Parameters.AddWithValue("@payment", paymentMethod);
                cmd.Parameters.AddWithValue("@orderDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                cmd.Parameters.AddWithValue("@deliveryTime", DateTime.Now.AddHours(1).ToString("HH:mm"));
                cmd.Parameters.AddWithValue("@pointsUsed", pointsToUse);
                cmd.ExecuteNonQuery();
            }

            MessageBox.Show($"Заказ оформлен на сумму {totalPrice - pointsToUse} руб.! Начислено {pointsEarned} баллов.");
            cartItems.Clear();
            totalPrice = 0;
            pointsToUse = 0;
            PointsToUseTextBox.Text = "";
            TotalPriceTextBlock.Text = $"Итого: {totalPrice} руб.";
            CartStreetTextBox.Text = "";
            CartApartmentTextBox.Text = "";
            CartEntranceTextBox.Text = "";
            CartFloorTextBox.Text = "";
            CartPhoneNumberTextBox.Text = "";
            CartPaymentMethodComboBox.SelectedIndex = -1;
            UpdateLoyaltyPointsDisplay();
            LoadUserOrders();
        }

        private void OrdersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void UserOrdersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        // Admin Panel Methods
        private void LoadAdminMenu()
        {
            AdminMenuListBox.ItemsSource = null;
            var adminMenuItems = new ObservableCollection<MenuItem>();
            string query = "SELECT Id, Name FROM Menu";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    adminMenuItems.Add(new MenuItem
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Name = reader["Name"].ToString()
                    });
                }
            }
            AdminMenuListBox.ItemsSource = adminMenuItems;
        }

        private void AddMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string name = AdminMenuNameTextBox.Text;
            string priceText = AdminMenuPriceTextBox.Text;
            string ingredients = AdminMenuIngredientsTextBox.Text;
            string caloriesText = AdminMenuCaloriesTextBox.Text;
            string category = (AdminMenuCategoryComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(priceText) || string.IsNullOrEmpty(ingredients) || string.IsNullOrEmpty(caloriesText) || string.IsNullOrEmpty(category))
            {
                MessageBox.Show("Заполните все поля!");
                return;
            }

            if (!decimal.TryParse(priceText, out decimal price) || !int.TryParse(caloriesText, out int calories))
            {
                MessageBox.Show("Некорректная цена или калории!");
                return;
            }

            string query = "INSERT INTO Menu (Name, Price, Ingredients, Calories, Category) VALUES (@name, @price, @ingredients, @calories, @category)";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@ingredients", ingredients);
                cmd.Parameters.AddWithValue("@calories", calories);
                cmd.Parameters.AddWithValue("@category", category);
                cmd.ExecuteNonQuery();
            }

            MessageBox.Show("Блюдо добавлено!");
            LoadAdminMenu();
            LoadMenu();
            AdminMenuNameTextBox.Text = "";
            AdminMenuPriceTextBox.Text = "";
            AdminMenuIngredientsTextBox.Text = "";
            AdminMenuCaloriesTextBox.Text = "";
            AdminMenuCategoryComboBox.SelectedIndex = -1;
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int menuId = (int)button.Tag;

            string query = "DELETE FROM Menu WHERE Id = @id";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@id", menuId);
                cmd.ExecuteNonQuery();
            }

            MessageBox.Show("Блюдо удалено!");
            LoadAdminMenu();
            LoadMenu();
        }

        private void LoadAdminUsers()
        {
            AdminUsersListBox.ItemsSource = null;
            adminUsers.Clear();
            string query = "SELECT Login, Role, IsBlocked, LoyaltyPoints FROM Users";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    adminUsers.Add(new UserItem
                    {
                        Login = reader["Login"].ToString(),
                        Role = reader["Role"].ToString(),
                        IsBlocked = Convert.ToInt32(reader["IsBlocked"]) == 1,
                        LoyaltyPoints = Convert.ToInt32(reader["LoyaltyPoints"])
                    });
                }
            }
            AdminUsersListBox.ItemsSource = adminUsers;
        }

        private void ToggleBlockUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string login = button.Tag as string;

            string query = "UPDATE Users SET IsBlocked = CASE WHEN IsBlocked = 1 THEN 0 ELSE 1 END WHERE Login = @login";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@login", login);
                cmd.ExecuteNonQuery();
            }

            MessageBox.Show("Статус блокировки изменен!");
            LoadAdminUsers();
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string login = button.Tag as string;

            string query = "DELETE FROM Users WHERE Login = @login";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@login", login);
                cmd.ExecuteNonQuery();
            }

            MessageBox.Show("Пользователь удален!");
            LoadAdminUsers();
        }

        private void LoadAdminOrders(string status = null)
        {
            AdminOrdersListBox.ItemsSource = null;
            adminOrders.Clear();
            string query = "SELECT Id, ItemName, UserLogin, Street, PhoneNumber, PaymentMethod, Status, CourierLogin, OrderDate, DeliveryTime, LoyaltyPointsUsed FROM Orders";
            if (!string.IsNullOrEmpty(status) && status != "All")
                query += " WHERE Status = @status";
            using (var cmd = new SQLiteCommand(query, connection))
            {
                if (!string.IsNullOrEmpty(status) && status != "All")
                    cmd.Parameters.AddWithValue("@status", status);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string courierInfo = reader["CourierLogin"] != DBNull.Value ? $"{reader["CourierLogin"]}" : "Не назначен";
                    string address = BuildAddress(reader["Street"], "", "", "");
                    string pointsUsed = reader["LoyaltyPointsUsed"] != DBNull.Value ? $"Использовано баллов: {reader["LoyaltyPointsUsed"]}" : "";
                    adminOrders.Add(new OrderItem
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        OrderSummary = $"Заказ #{reader["Id"]}: {reader["ItemName"]}",
                        Address = $"Адрес: {address}",
                        PhoneNumber = $"Телефон: {reader["PhoneNumber"]?.ToString()}",
                        PaymentMethod = $"Оплата: {reader["PaymentMethod"]?.ToString()}",
                        Status = $"Статус: {reader["Status"]?.ToString()}",
                        CourierInfo = $"Курьер: {courierInfo}",
                        OrderDate = $"Дата заказа: {reader["OrderDate"]?.ToString()}",
                        DeliveryTime = $"Время доставки: {reader["DeliveryTime"]?.ToString()}",
                        LoyaltyPointsUsed = pointsUsed
                    });
                }
            }
            AdminOrdersListBox.ItemsSource = adminOrders;
        }

        private void AdminOrderStatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedStatus = (AdminOrderStatusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            LoadAdminOrders(selectedStatus == "Все" ? null : selectedStatus);
        }
    }

    public class MenuItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string ImagePath { get; set; }
        public string Ingredients { get; set; }
        public string Calories { get; set; }
        public string Category { get; set; }
    }

    public class CartItem
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice => Price * Quantity;
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public string OrderSummary { get; set; }
        public string Address { get; set; }
        public string PhoneNumber { get; set; }
        public string PaymentMethod { get; set; }
        public string Status { get; set; }
        public string CourierInfo { get; set; }
        public string OrderDate { get; set; }
        public string DeliveryTime { get; set; }
        public string LoyaltyPointsUsed { get; set; }
        public double ProgressValue { get; set; }
        public SolidColorBrush StatusColor { get; set; }
    }

    public class UserItem
    {
        public string Login { get; set; }
        public string Role { get; set; }
        public bool IsBlocked { get; set; }
        public int LoyaltyPoints { get; set; }
    }
}