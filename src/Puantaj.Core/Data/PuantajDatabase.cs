using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Puantaj.Core.Data;

public sealed class PuantajDatabase
{
    private readonly string _connectionString;

    public string DatabasePath { get; }

    public PuantajDatabase(string? databasePath = null)
    {
        DatabasePath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Puantaj",
            "puantaj.db");
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();
    }

    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS Employees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                DisplayOrder INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Shifts (
                Code TEXT PRIMARY KEY,
                Description TEXT NOT NULL DEFAULT '',
                StartTime TEXT NULL,
                EndTime TEXT NULL,
                IsWorkShift INTEGER NOT NULL,
                DisplayOrder INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Assignments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                WorkDate TEXT NOT NULL,
                Code TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY(EmployeeId) REFERENCES Employees(Id),
                UNIQUE(EmployeeId, WorkDate)
            );
            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                HotelName TEXT NOT NULL,
                DepartmentName TEXT NOT NULL,
                LogoPath TEXT NOT NULL,
                DepartmentManager TEXT NOT NULL,
                DepartmentManagerTitle TEXT NOT NULL,
                HumanResourcesManager TEXT NOT NULL,
                HumanResourcesTitle TEXT NOT NULL,
                GeneralManager TEXT NOT NULL,
                GeneralManagerTitle TEXT NOT NULL,
                LogoSizeCm REAL NOT NULL,
                MarginLeftCm REAL NOT NULL,
                MarginRightCm REAL NOT NULL,
                MarginTopCm REAL NOT NULL,
                MarginBottomCm REAL NOT NULL,
                PrintLogo INTEGER NOT NULL,
                CenterHorizontally INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS LockedMonths (
                Year INTEGER NOT NULL,
                Month INTEGER NOT NULL CHECK (Month BETWEEN 1 AND 12),
                LockedAt TEXT NOT NULL,
                PRIMARY KEY (Year, Month)
            );
            """;
        command.ExecuteNonQuery();
        EnsureEmployeeDetailColumns(connection);
        EnsureDescriptionColumn(connection);
        SeedShifts(connection);
    }

    public IReadOnlyList<Employee> GetEmployees(bool activeOnly = true)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Id, FullName, IsActive, DisplayOrder, CreatedAt, Position, WorkPattern, HireDate
            FROM Employees
            {(activeOnly ? "WHERE IsActive = 1" : string.Empty)}
            ORDER BY DisplayOrder, FullName;
            """;
        using var reader = command.ExecuteReader();
        var employees = new List<Employee>();
        while (reader.Read())
        {
            employees.Add(new Employee(
                reader.GetInt64(0), reader.GetString(1), reader.GetBoolean(2), reader.GetInt32(3),
                DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : DateOnly.ParseExact(reader.GetString(7), "yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        return employees;
    }

    public IReadOnlyList<Employee> GetEmployeesForPeriod(DateOnly from, DateOnly to)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT e.Id, e.FullName, e.IsActive, e.DisplayOrder, e.CreatedAt,
                   e.Position, e.WorkPattern, e.HireDate
            FROM Employees e
            LEFT JOIN Assignments a ON a.EmployeeId=e.Id AND a.WorkDate >= $from AND a.WorkDate <= $to
            WHERE e.IsActive=1 OR a.Id IS NOT NULL
            ORDER BY e.DisplayOrder, e.FullName;
            """;
        command.Parameters.AddWithValue("$from", FormatDate(from)); command.Parameters.AddWithValue("$to", FormatDate(to));
        using var reader = command.ExecuteReader(); var employees = new List<Employee>();
        while (reader.Read()) employees.Add(ReadEmployee(reader));
        return employees;
    }

    public long AddEmployee(string fullName)
    {
        fullName = RequireName(fullName);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Employees (FullName, IsActive, DisplayOrder, CreatedAt)
            VALUES ($name, 1, COALESCE((SELECT MAX(DisplayOrder) + 1 FROM Employees), 1), $created);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", fullName);
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        return (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Personel eklenemedi."));
    }

    public void UpdateEmployee(long id, string fullName)
    {
        using var connection = Open();
        Execute(connection, "UPDATE Employees SET FullName = $name WHERE Id = $id;",
            ("$name", RequireName(fullName)), ("$id", id));
    }

    public void UpdateEmployeeDetails(long id, string position, string workPattern, DateOnly? hireDate)
    {
        using var connection = Open();
        Execute(connection, "UPDATE Employees SET Position=$position, WorkPattern=$pattern, HireDate=$hire WHERE Id=$id;",
            ("$position", position.Trim()), ("$pattern", workPattern.Trim()), ("$hire", hireDate is null ? null : FormatDate(hireDate.Value)), ("$id", id));
    }

    public void SetEmployeeActive(long id, bool active)
    {
        using var connection = Open();
        Execute(connection, "UPDATE Employees SET IsActive = $active WHERE Id = $id;",
            ("$active", active ? 1 : 0), ("$id", id));
    }

    public void MoveEmployee(long id, int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var current = connection.CreateCommand();
        current.Transaction = transaction;
        current.CommandText = "SELECT DisplayOrder FROM Employees WHERE Id = $id;";
        current.Parameters.AddWithValue("$id", id);
        var order = Convert.ToInt32(current.ExecuteScalar(), CultureInfo.InvariantCulture);
        using var neighbor = connection.CreateCommand();
        neighbor.Transaction = transaction;
        neighbor.CommandText = direction < 0
            ? "SELECT Id, DisplayOrder FROM Employees WHERE DisplayOrder < $order ORDER BY DisplayOrder DESC LIMIT 1;"
            : "SELECT Id, DisplayOrder FROM Employees WHERE DisplayOrder > $order ORDER BY DisplayOrder LIMIT 1;";
        neighbor.Parameters.AddWithValue("$order", order);
        using var reader = neighbor.ExecuteReader();
        if (!reader.Read()) return;
        var neighborId = reader.GetInt64(0);
        var neighborOrder = reader.GetInt32(1);
        reader.Close();
        Execute(connection, "UPDATE Employees SET DisplayOrder = $order WHERE Id = $id;", transaction,
            ("$order", neighborOrder), ("$id", id));
        Execute(connection, "UPDATE Employees SET DisplayOrder = $order WHERE Id = $id;", transaction,
            ("$order", order), ("$id", neighborId));
        transaction.Commit();
    }

    public IReadOnlyList<Shift> GetShifts()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Code, StartTime, EndTime, IsWorkShift, DisplayOrder FROM Shifts ORDER BY IsWorkShift DESC, Code COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        var shifts = new List<Shift>();
        while (reader.Read())
        {
            shifts.Add(new Shift(reader.GetString(0), ParseTime(reader, 1), ParseTime(reader, 2), reader.GetBoolean(3), reader.GetInt32(4)));
        }

        return shifts;
    }

    public IReadOnlyList<AssignmentCodeDefinition> GetAssignmentCodes()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Code, Description, StartTime, EndTime, IsWorkShift, DisplayOrder FROM Shifts ORDER BY IsWorkShift DESC, Code COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        var result = new List<AssignmentCodeDefinition>();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), ParseTime(reader, 2), ParseTime(reader, 3), reader.GetBoolean(4), reader.GetInt32(5)));
        return result;
    }

    public void SaveAssignmentCode(string code, string description, TimeSpan? start, TimeSpan? end, bool isWorkShift)
    {
        code = RequireCode(code);
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Açıklama boş olamaz.");
        if (isWorkShift && (start is null || end is null)) throw new ArgumentException("Vardiya saatleri zorunludur.");
        using var connection = Open();
        Execute(connection, """
            INSERT INTO Shifts (Code, Description, StartTime, EndTime, IsWorkShift, DisplayOrder)
            VALUES ($code, $description, $start, $end, $work, COALESCE((SELECT MAX(DisplayOrder)+1 FROM Shifts),1))
            ON CONFLICT(Code) DO UPDATE SET Description=excluded.Description, StartTime=excluded.StartTime,
            EndTime=excluded.EndTime, IsWorkShift=excluded.IsWorkShift;
            """, ("$code", code), ("$description", description.Trim()), ("$start", start is null ? null : FormatTime(start.Value)),
            ("$end", end is null ? null : FormatTime(end.Value)), ("$work", isWorkShift ? 1 : 0));
    }

    public void UpdateShift(string code, TimeSpan startTime, TimeSpan endTime)
    {
        code = RequireCode(code);
        using var connection = Open();
        Execute(connection, "UPDATE Shifts SET StartTime = $start, EndTime = $end WHERE Code = $code;",
            ("$start", FormatTime(startTime)), ("$end", code == "C" && endTime == TimeSpan.Zero ? "24:00" : FormatTime(endTime)), ("$code", code));
    }

    public IReadOnlyList<Assignment> GetAssignments(DateOnly from, DateOnly to)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, EmployeeId, WorkDate, Code, UpdatedAt FROM Assignments
            WHERE WorkDate >= $from AND WorkDate <= $to ORDER BY EmployeeId, WorkDate;
            """;
        command.Parameters.AddWithValue("$from", FormatDate(from));
        command.Parameters.AddWithValue("$to", FormatDate(to));
        using var reader = command.ExecuteReader();
        var assignments = new List<Assignment>();
        while (reader.Read())
        {
            assignments.Add(new Assignment(reader.GetInt64(0), reader.GetInt64(1),
                DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture), reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
        }

        return assignments;
    }

    public void Assign(long employeeId, DateOnly date, string code) => AssignMany([employeeId], [date], code);

    public void SaveWeekAssignments(long employeeId, DateOnly from, DateOnly to,
        IReadOnlyDictionary<DateOnly, string> assignments, bool allowOverwrite)
    {
        if (to < from) throw new ArgumentOutOfRangeException(nameof(to));
        var definitions = GetAssignmentCodes();
        var resolver = new AssignmentCodeResolver(definitions);
        var previousEmploymentEnd = allowOverwrite
            ? GetAssignments(from, to).Where(item => item.EmployeeId == employeeId && resolver.Resolve(item.Code).IsEmploymentEnded)
                .Select(item => (DateOnly?)item.WorkDate).Min()
            : null;
        foreach (var month in assignments.Keys.Select(date => (date.Year, date.Month)).Distinct())
            EnsureMonthUnlocked(month.Year, month.Month);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        if (previousEmploymentEnd is not null)
        {
            var monthEnd = new DateOnly(previousEmploymentEnd.Value.Year, previousEmploymentEnd.Value.Month,
                DateTime.DaysInMonth(previousEmploymentEnd.Value.Year, previousEmploymentEnd.Value.Month));
            foreach (var endedCode in definitions.Where(item => item.IsEmploymentEnded).Select(item => item.Code))
                Execute(connection, "DELETE FROM Assignments WHERE EmployeeId=$employee AND WorkDate >= $from AND WorkDate <= $to AND Code=$code;",
                    transaction, ("$employee", employeeId), ("$from", FormatDate(previousEmploymentEnd.Value)),
                    ("$to", FormatDate(monthEnd)), ("$code", endedCode));
        }
        using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = "SELECT COUNT(*) FROM Assignments WHERE EmployeeId=$employee AND WorkDate >= $from AND WorkDate <= $to;";
            existing.Parameters.AddWithValue("$employee", employeeId); existing.Parameters.AddWithValue("$from", FormatDate(from)); existing.Parameters.AddWithValue("$to", FormatDate(to));
            if (!allowOverwrite && Convert.ToInt32(existing.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
                throw new InvalidOperationException("Bu hafta daha önce oluşturuldu. Değişiklik yapmak için Düzenle butonunu kullanın.");
        }
        foreach (var assignment in assignments)
        {
            using var validation = connection.CreateCommand(); validation.Transaction = transaction;
            validation.CommandText = "SELECT COUNT(*) FROM Shifts WHERE Code=$code;"; validation.Parameters.AddWithValue("$code", assignment.Value);
            if (Convert.ToInt32(validation.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                throw new ArgumentException($"Tanımsız çalışma kodu: {assignment.Value}");
            Execute(connection, """
                INSERT INTO Assignments (EmployeeId, WorkDate, Code, UpdatedAt)
                VALUES ($employeeId, $date, $code, $updated)
                ON CONFLICT(EmployeeId, WorkDate) DO UPDATE SET Code=excluded.Code, UpdatedAt=excluded.UpdatedAt;
                """, transaction, ("$employeeId", employeeId), ("$date", FormatDate(assignment.Key)),
                ("$code", assignment.Value), ("$updated", DateTimeOffset.UtcNow.ToString("O")));
        }
        transaction.Commit();
    }

    public void CopyMonthAssignments(long sourceEmployeeId, long targetEmployeeId, int year, int month)
    {
        if (sourceEmployeeId == targetEmployeeId) throw new ArgumentException("Kaynak ve hedef personel farklı olmalıdır.");
        ValidateMonth(year, month);
        EnsureMonthUnlocked(year, month);
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        Execute(connection, "DELETE FROM Assignments WHERE EmployeeId=$employee AND WorkDate >= $from AND WorkDate <= $to;",
            transaction, ("$employee", targetEmployeeId), ("$from", FormatDate(from)), ("$to", FormatDate(to)));
        Execute(connection, """
            INSERT INTO Assignments (EmployeeId, WorkDate, Code, UpdatedAt)
            SELECT $target, WorkDate, Code, $updated
            FROM Assignments
            WHERE EmployeeId=$source AND WorkDate >= $from AND WorkDate <= $to;
            """, transaction, ("$target", targetEmployeeId), ("$updated", DateTimeOffset.UtcNow.ToString("O")),
            ("$source", sourceEmployeeId), ("$from", FormatDate(from)), ("$to", FormatDate(to)));
        transaction.Commit();
    }

    public void AssignMany(IEnumerable<long> employeeIds, IEnumerable<DateOnly> dates, string code)
    {
        var ids = employeeIds.Distinct().ToArray();
        var workDates = dates.Distinct().ToArray();
        if (ids.Length == 0 || workDates.Length == 0) return;
        foreach (var month in workDates.Select(date => (date.Year, date.Month)).Distinct())
            EnsureMonthUnlocked(month.Year, month.Month);
        code = RequireCode(code);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var validation = connection.CreateCommand())
        {
            validation.Transaction = transaction;
            validation.CommandText = "SELECT COUNT(*) FROM Shifts WHERE Code=$code;";
            validation.Parameters.AddWithValue("$code", code);
            if (Convert.ToInt32(validation.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                throw new ArgumentException($"Tanımsız çalışma kodu: {code}");
        }
        foreach (var employeeId in ids)
        {
            foreach (var date in workDates)
            {
                Execute(connection, """
                    INSERT INTO Assignments (EmployeeId, WorkDate, Code, UpdatedAt)
                    VALUES ($employeeId, $date, $code, $updated)
                    ON CONFLICT(EmployeeId, WorkDate) DO UPDATE SET Code = excluded.Code, UpdatedAt = excluded.UpdatedAt;
                    """, transaction, ("$employeeId", employeeId), ("$date", FormatDate(date)), ("$code", code),
                    ("$updated", DateTimeOffset.UtcNow.ToString("O")));
            }
        }
        transaction.Commit();
    }

    public AppSettings EnsureSettings(string hotelName, string departmentName)
    {
        var defaults = AppSettings.CreateDefault(hotelName, departmentName);
        using var connection = Open();
        Execute(connection, """
            INSERT OR IGNORE INTO AppSettings
            (Id, HotelName, DepartmentName, LogoPath, DepartmentManager, DepartmentManagerTitle,
             HumanResourcesManager, HumanResourcesTitle, GeneralManager, GeneralManagerTitle,
             LogoSizeCm, MarginLeftCm, MarginRightCm, MarginTopCm, MarginBottomCm, PrintLogo, CenterHorizontally)
            VALUES (1, $hotel, $department, '', '', '', '', '', '', '', 2.5, 0.7, 0.7, 0.7, 0.7, 1, 1);
            """, ("$hotel", defaults.HotelName), ("$department", defaults.DepartmentName));
        return GetSettings();
    }

    public AppSettings GetSettings()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT HotelName, DepartmentName, LogoPath, DepartmentManager, DepartmentManagerTitle,
                   HumanResourcesManager, HumanResourcesTitle, GeneralManager, GeneralManagerTitle,
                   LogoSizeCm, MarginLeftCm, MarginRightCm, MarginTopCm, MarginBottomCm, PrintLogo, CenterHorizontally
            FROM AppSettings WHERE Id=1;
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException("Uygulama ayarları henüz oluşturulmamış.");
        return new AppSettings(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8),
            reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.GetDecimal(12), reader.GetDecimal(13),
            reader.GetBoolean(14), reader.GetBoolean(15));
    }

    public void SaveSettings(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.HotelName) || string.IsNullOrWhiteSpace(settings.DepartmentName))
            throw new ArgumentException("Otel ve departman adı boş olamaz.");
        if (settings.LogoSizeCm <= 0 || settings.MarginLeftCm < 0 || settings.MarginRightCm < 0 ||
            settings.MarginTopCm < 0 || settings.MarginBottomCm < 0)
            throw new ArgumentException("Logo boyutu ve sayfa kenar boşlukları geçerli olmalıdır.");
        using var connection = Open();
        Execute(connection, """
            INSERT INTO AppSettings
            (Id, HotelName, DepartmentName, LogoPath, DepartmentManager, DepartmentManagerTitle,
             HumanResourcesManager, HumanResourcesTitle, GeneralManager, GeneralManagerTitle,
             LogoSizeCm, MarginLeftCm, MarginRightCm, MarginTopCm, MarginBottomCm, PrintLogo, CenterHorizontally)
            VALUES (1,$hotel,$department,$logo,$dm,$dmt,$hr,$hrt,$gm,$gmt,$size,$left,$right,$top,$bottom,$printLogo,$center)
            ON CONFLICT(Id) DO UPDATE SET HotelName=excluded.HotelName, DepartmentName=excluded.DepartmentName,
            LogoPath=excluded.LogoPath, DepartmentManager=excluded.DepartmentManager,
            DepartmentManagerTitle=excluded.DepartmentManagerTitle, HumanResourcesManager=excluded.HumanResourcesManager,
            HumanResourcesTitle=excluded.HumanResourcesTitle, GeneralManager=excluded.GeneralManager,
            GeneralManagerTitle=excluded.GeneralManagerTitle, LogoSizeCm=excluded.LogoSizeCm,
            MarginLeftCm=excluded.MarginLeftCm, MarginRightCm=excluded.MarginRightCm,
            MarginTopCm=excluded.MarginTopCm, MarginBottomCm=excluded.MarginBottomCm,
            PrintLogo=excluded.PrintLogo, CenterHorizontally=excluded.CenterHorizontally;
            """, ("$hotel", settings.HotelName.Trim()), ("$department", settings.DepartmentName.Trim()),
            ("$logo", settings.LogoPath.Trim()), ("$dm", settings.DepartmentManager.Trim()),
            ("$dmt", settings.DepartmentManagerTitle.Trim()), ("$hr", settings.HumanResourcesManager.Trim()),
            ("$hrt", settings.HumanResourcesTitle.Trim()), ("$gm", settings.GeneralManager.Trim()),
            ("$gmt", settings.GeneralManagerTitle.Trim()), ("$size", settings.LogoSizeCm),
            ("$left", settings.MarginLeftCm), ("$right", settings.MarginRightCm), ("$top", settings.MarginTopCm),
            ("$bottom", settings.MarginBottomCm), ("$printLogo", settings.PrintLogo ? 1 : 0),
            ("$center", settings.CenterHorizontally ? 1 : 0));
    }

    public void LockMonth(int year, int month)
    {
        ValidateMonth(year, month);
        using var connection = Open();
        Execute(connection, "INSERT OR IGNORE INTO LockedMonths (Year, Month, LockedAt) VALUES ($year,$month,$at);",
            ("$year", year), ("$month", month), ("$at", DateTimeOffset.UtcNow.ToString("O")));
    }

    public void UnlockMonth(int year, int month)
    {
        ValidateMonth(year, month);
        using var connection = Open();
        Execute(connection, "DELETE FROM LockedMonths WHERE Year=$year AND Month=$month;", ("$year", year), ("$month", month));
    }

    public bool IsMonthLocked(int year, int month)
    {
        ValidateMonth(year, month);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM LockedMonths WHERE Year=$year AND Month=$month;";
        command.Parameters.AddWithValue("$year", year); command.Parameters.AddWithValue("$month", month);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    public void EnsureMonthUnlocked(int year, int month)
    {
        if (IsMonthLocked(year, month))
            throw new InvalidOperationException($"{month:00}/{year} ayı kilitlidir. Değişiklik yapmak için önce 'Kilitli Ayları Yönet' ekranından kilidi kaldırın.");
    }

    public IReadOnlyList<LockedMonth> GetLockedMonths()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Year, Month, LockedAt FROM LockedMonths ORDER BY Year DESC, Month DESC;";
        using var reader = command.ExecuteReader(); var result = new List<LockedMonth>();
        while (reader.Read()) result.Add(new(reader.GetInt32(0), reader.GetInt32(1),
            DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
        return result;
    }

    private static void ValidateMonth(int year, int month)
    {
        if (year is < 2000 or > 9999 || month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static void SeedShifts(SqliteConnection connection)
    {
        var rows = new (string Code, string Description, string? Start, string? End, bool Work)[]
        {
            ("A", "Vardiya A", "09:00", "17:00", true), ("B", "Vardiya B", "08:00", "16:00", true),
            ("C", "Vardiya C", "16:00", "24:00", true), ("D", "Vardiya D", "00:00", "08:00", true),
            ("E", "Vardiya E", "13:00", "21:00", true), ("HT", "Hafta Tatili", null, null, false),
            ("RT", "Resmî Tatil", null, null, false), ("RP", "Raporlu", null, null, false), ("ÜZ", "Ücretsiz İzin", null, null, false),
            ("Üİ", "Ücretli İzin", null, null, false), ("Yİ", "Yıllık İzin", null, null, false), ("Aİ", "Alacak İzin", null, null, false),
            ("G", "Görevli", null, null, false), ("İA", "İşten Ayrıldı", null, null, false)
        };
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            Execute(connection, """
                INSERT OR IGNORE INTO Shifts (Code, Description, StartTime, EndTime, IsWorkShift, DisplayOrder)
                VALUES ($code, $description, $start, $end, $work, $order);
                """, ("$code", row.Code), ("$description", row.Description), ("$start", row.Start), ("$end", row.End),
                ("$work", row.Work ? 1 : 0), ("$order", index + 1));
        }
    }

    private static void EnsureDescriptionColumn(SqliteConnection connection)
    {
        using var check = connection.CreateCommand(); check.CommandText = "PRAGMA table_info(Shifts);";
        using var reader = check.ExecuteReader(); var exists = false;
        while (reader.Read()) if (reader.GetString(1) == "Description") exists = true;
        reader.Close();
        if (!exists) Execute(connection, "ALTER TABLE Shifts ADD COLUMN Description TEXT NOT NULL DEFAULT ''; ");
        Execute(connection, "UPDATE Shifts SET Description = CASE Code WHEN 'HT' THEN 'Hafta Tatili' WHEN 'RT' THEN 'Resmî Tatil' WHEN 'RP' THEN 'Raporlu' WHEN 'ÜZ' THEN 'Ücretsiz İzin' WHEN 'Üİ' THEN 'Ücretli İzin' WHEN 'Yİ' THEN 'Yıllık İzin' WHEN 'Aİ' THEN 'Alacak İzin' WHEN 'G' THEN 'Görevli' ELSE 'Vardiya ' || Code END WHERE Description='';");
    }

    private static void EnsureEmployeeDetailColumns(SqliteConnection connection)
    {
        using var check = connection.CreateCommand(); check.CommandText = "PRAGMA table_info(Employees);";
        using var reader = check.ExecuteReader(); var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) columns.Add(reader.GetString(1)); reader.Close();
        if (!columns.Contains("Position")) Execute(connection, "ALTER TABLE Employees ADD COLUMN Position TEXT NOT NULL DEFAULT ''; ");
        if (!columns.Contains("WorkPattern")) Execute(connection, "ALTER TABLE Employees ADD COLUMN WorkPattern TEXT NOT NULL DEFAULT ''; ");
        if (!columns.Contains("HireDate")) Execute(connection, "ALTER TABLE Employees ADD COLUMN HireDate TEXT NULL; ");
    }

    private static string RequireCode(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Kod boş olamaz.") : value.Trim().ToUpperInvariant();

    private static string RequireName(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Personel adı boş olamaz.") : value.Trim();
    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string FormatTime(TimeSpan time) => time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    private static TimeSpan? ParseTime(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal) == "24:00"
            ? TimeSpan.Zero
            : TimeSpan.ParseExact(reader.GetString(ordinal), @"hh\:mm", CultureInfo.InvariantCulture);

    private static Employee ReadEmployee(SqliteDataReader reader) => new(
        reader.GetInt64(0), reader.GetString(1), reader.GetBoolean(2), reader.GetInt32(3),
        DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture), reader.GetString(5), reader.GetString(6),
        reader.IsDBNull(7) ? null : DateOnly.ParseExact(reader.GetString(7), "yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object? Value)[] values) =>
        Execute(connection, sql, null, values);

    private static void Execute(SqliteConnection connection, string sql, SqliteTransaction? transaction,
        params (string Name, object? Value)[] values)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var value in values) command.Parameters.AddWithValue(value.Name, value.Value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
