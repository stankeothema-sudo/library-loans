using System.Globalization;

namespace LibraryLoans;

/// <summary>One loan record. Stored in books.txt as pipe-separated text.</summary>
class Loan
{
    public string LoanNumber, MemberName, BookTitle, Author, Category, Contact;
    public DateTime BorrowDate;
    public DateTime? ReturnDate;   // null = still on loan

    public string ToLine() => string.Join("|", LoanNumber, MemberName, BookTitle, Author, Category,
        BorrowDate.ToString(DateFmt), ReturnDate?.ToString(DateFmt) ?? "", Contact);

    public static Loan FromLine(string line)
    {
        string[] p = line.Split('|');
        if (p.Length != 8) return null;
        if (!DateTime.TryParseExact(p[5], DateFmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime b)) return null;
        DateTime? r = null;
        if (p[6] != "")
        {
            if (!DateTime.TryParseExact(p[6], DateFmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime rd)) return null;
            r = rd;
        }
        return new Loan { LoanNumber = p[0], MemberName = p[1], BookTitle = p[2], Author = p[3],
            Category = p[4], BorrowDate = b, ReturnDate = r, Contact = p[7] };
    }

    public const string DateFmt = "yyyy-MM-dd";
    public bool IsOverdue => ReturnDate == null && BorrowDate.AddDays(Program.LoanDays) < DateTime.Today;
    public string Status => ReturnDate != null ? "Returned" : IsOverdue ? "OVERDUE" : "Active";
}

class Program
{
    const string LoanFile = "books.txt";          // active / current loan records
    const string ArchiveFile = "returned.txt";    // loans removed when the book is returned
    public const int LoanDays = 14;               // a book is overdue after 14 days

    static void Main()
    {
        bool running = true;
        while (running)
        {
            ShowMenu();
            switch (Console.ReadLine()?.Trim())
            {
                case "1": BorrowBook(); break;
                case "2": ViewLoans(); break;
                case "3": SearchLoan(); break;
                case "4": UpdateLoan(); break;
                case "5": ReturnBook(); break;
                case "6": GenerateReports(); break;
                case "7": running = false; Console.WriteLine("Goodbye!"); break;
                default: Console.WriteLine("Invalid choice. Enter a number from 1 to 7."); break;
            }
            if (running) { Console.WriteLine("\nPress Enter to continue..."); Console.ReadLine(); }
        }
    }

    static void ShowMenu()
    {
        Console.Clear();
        Console.WriteLine("===========================");
        Console.WriteLine("COMMUNITY LIBRARY");
        Console.WriteLine("===========================");
        Console.WriteLine("1. Borrow Book");
        Console.WriteLine("2. View Borrowed Books");
        Console.WriteLine("3. Search Loan");
        Console.WriteLine("4. Update Loan");
        Console.WriteLine("5. Return Book");
        Console.WriteLine("6. Generate Reports");
        Console.WriteLine("7. Exit");
        Console.Write("\nChoose an option: ");
    }

    // ================= File handling =================
    /// <summary>Reads every valid record from a file (empty list if the file does not exist).</summary>
    static List<Loan> ReadLoans(string path)
    {
        var loans = new List<Loan>();
        if (!File.Exists(path)) return loans;
        try
        {
            using StreamReader reader = new StreamReader(path);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Trim() == "") continue;
                Loan l = Loan.FromLine(line);
                if (l != null) loans.Add(l);        // skip corrupt lines
            }
        }
        catch (IOException ex) { Console.WriteLine("Could not read " + path + ": " + ex.Message); }
        return loans;
    }

    /// <summary>Overwrites books.txt with the given list.</summary>
    static void SaveLoans(List<Loan> loans)
    {
        try
        {
            using StreamWriter writer = new StreamWriter(LoanFile, false);
            foreach (Loan l in loans) writer.WriteLine(l.ToLine());
        }
        catch (IOException ex) { Console.WriteLine("Could not save loans: " + ex.Message); }
    }

    static string NextLoanNumber()
    {
        int max = 0;
        foreach (Loan l in ReadLoans(LoanFile).Concat(ReadLoans(ArchiveFile)))
            if (l.LoanNumber.Length > 1 && int.TryParse(l.LoanNumber.Substring(1), out int n) && n > max) max = n;
        return "L" + (max + 1).ToString("D3");
    }

    // ================= Input helpers =================
    static string ReadRequired(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string s = (Console.ReadLine() ?? "").Trim().Replace("|", "/");
            if (s != "") return s;
            Console.WriteLine("This field cannot be empty.");
        }
    }

    static string ReadContact(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string s = (Console.ReadLine() ?? "").Trim();
            if (s.Length == 10 && s.All(char.IsDigit)) return s;
            Console.WriteLine("Contact number must be exactly 10 digits.");
        }
    }

    /// <summary>Reads a yyyy-MM-dd date. If allowBlank, an empty entry returns null.</summary>
    static DateTime? ReadDate(string prompt, bool allowBlank)
    {
        while (true)
        {
            Console.Write(prompt);
            string s = (Console.ReadLine() ?? "").Trim();
            if (s == "" && allowBlank) return null;
            if (DateTime.TryParseExact(s, Loan.DateFmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)) return d;
            Console.WriteLine("Invalid date. Use yyyy-MM-dd (e.g. 2026-10-27).");
        }
    }

    static void PrintHeader() =>
        Console.WriteLine($"{"Loan",-6}{"Member",-18}{"Title",-24}{"Author",-18}{"Category",-12}{"Borrowed",-12}{"Returned",-12}{"Contact",-12}Status");

    static void PrintLoan(Loan l) =>
        Console.WriteLine($"{l.LoanNumber,-6}{Cut(l.MemberName, 17),-18}{Cut(l.BookTitle, 23),-24}{Cut(l.Author, 17),-18}{Cut(l.Category, 11),-12}" +
            $"{l.BorrowDate.ToString(Loan.DateFmt),-12}{(l.ReturnDate?.ToString(Loan.DateFmt) ?? "-"),-12}{l.Contact,-12}{l.Status}");

    static string Cut(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

    // ================= Menu actions =================
    static void BorrowBook()
    {
        try
        {
            Console.WriteLine("\n--- Borrow Book ---");
            var loan = new Loan { LoanNumber = NextLoanNumber() };
            Console.WriteLine("Loan Number: " + loan.LoanNumber);
            loan.MemberName = ReadRequired("Member name: ");
            loan.BookTitle = ReadRequired("Book title: ");
            loan.Author = ReadRequired("Author: ");
            loan.Category = ReadRequired("Category (e.g. Fiction, Science, History): ");
            loan.BorrowDate = ReadDate("Borrow date (yyyy-MM-dd, Enter = today): ", true) ?? DateTime.Today;
            loan.ReturnDate = null;                       // not yet returned
            loan.Contact = ReadContact("Contact number (10 digits): ");

            File.AppendAllText(LoanFile, loan.ToLine() + Environment.NewLine);   // creates the file if missing
            Console.WriteLine($"Loan {loan.LoanNumber} recorded. Due back by {loan.BorrowDate.AddDays(LoanDays):yyyy-MM-dd}.");
        }
        catch (Exception ex) { Console.WriteLine("Error: " + ex.Message); }
    }

    static void ViewLoans()
    {
        Console.WriteLine("\n--- All Loans ---");
        List<Loan> loans = ReadLoans(LoanFile);
        if (loans.Count == 0) { Console.WriteLine("No loan records found."); return; }
        PrintHeader();
        foreach (Loan l in loans) PrintLoan(l);
        Console.WriteLine($"\nTotal records: {loans.Count}");
    }

    static void SearchLoan()
    {
        Console.WriteLine("\n--- Search Loan ---");
        Console.Write("Enter Loan Number or Member Name: ");
        string term = (Console.ReadLine() ?? "").Trim();
        if (term == "") { Console.WriteLine("Search term cannot be empty."); return; }

        bool found = false;
        foreach (Loan l in ReadLoans(LoanFile))
        {
            if (l.LoanNumber.Equals(term, StringComparison.OrdinalIgnoreCase) ||
                l.MemberName.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                if (!found) PrintHeader();
                PrintLoan(l);
                found = true;
            }
        }
        if (!found) Console.WriteLine("No matching loan found.");
    }

    static void UpdateLoan()
    {
        Console.WriteLine("\n--- Update Loan ---");
        Console.Write("Loan Number to update: ");
        string num = (Console.ReadLine() ?? "").Trim();
        List<Loan> loans = ReadLoans(LoanFile);
        Loan loan = loans.Find(l => l.LoanNumber.Equals(num, StringComparison.OrdinalIgnoreCase));
        if (loan == null) { Console.WriteLine("Loan not found."); return; }

        PrintHeader(); PrintLoan(loan);
        Console.WriteLine("\n1. Update return date\n2. Update contact number\n3. Cancel");
        Console.Write("Choice: ");
        switch (Console.ReadLine()?.Trim())
        {
            case "1":
                DateTime? d = ReadDate("New return date (yyyy-MM-dd, Enter = clear/not returned): ", true);
                if (d != null && d < loan.BorrowDate) { Console.WriteLine("Return date cannot be before the borrow date."); return; }
                loan.ReturnDate = d;
                break;
            case "2":
                loan.Contact = ReadContact("New contact number (10 digits): ");
                break;
            default:
                Console.WriteLine("Update cancelled."); return;
        }
        SaveLoans(loans);
        Console.WriteLine("Loan updated.");
    }

    static void ReturnBook()
    {
        Console.WriteLine("\n--- Return Book ---");
        Console.Write("Loan Number being returned: ");
        string num = (Console.ReadLine() ?? "").Trim();
        List<Loan> loans = ReadLoans(LoanFile);
        Loan loan = loans.Find(l => l.LoanNumber.Equals(num, StringComparison.OrdinalIgnoreCase));
        if (loan == null) { Console.WriteLine("Loan not found."); return; }

        PrintHeader(); PrintLoan(loan);
        Console.Write("Confirm return and remove this loan? (Y/N): ");
        if ((Console.ReadLine() ?? "").Trim().ToUpper() != "Y") { Console.WriteLine("Cancelled."); return; }

        try
        {
            loan.ReturnDate ??= DateTime.Today;
            File.AppendAllText(ArchiveFile, loan.ToLine() + Environment.NewLine);   // keep history for reports
            loans.Remove(loan);
            SaveLoans(loans);
            Console.WriteLine("Book returned. Loan removed from books.txt.");
        }
        catch (Exception ex) { Console.WriteLine("Error: " + ex.Message); }
    }

    static void GenerateReports()
    {
        Console.WriteLine("\n--- Library Reports ---");
        List<Loan> current = ReadLoans(LoanFile);
        List<Loan> archived = ReadLoans(ArchiveFile);
        var all = current.Concat(archived).ToList();

        if (all.Count == 0) { Console.WriteLine("No loan data available."); return; }

        int active = 0, returned = archived.Count;
        var overdue = new List<Loan>();
        foreach (Loan l in current)
        {
            if (l.ReturnDate != null) returned++;
            else { active++; if (l.IsOverdue) overdue.Add(l); }
        }

        // Most borrowed category: count each category with a dictionary
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Loan l in all)
            counts[l.Category] = counts.TryGetValue(l.Category, out int c) ? c + 1 : 1;
        string topCat = ""; int topCount = 0;
        foreach (var kv in counts)
            if (kv.Value > topCount) { topCat = kv.Key; topCount = kv.Value; }

        Console.WriteLine($"Total loans            : {all.Count}");
        Console.WriteLine($"Active loans           : {active}");
        Console.WriteLine($"Returned books         : {returned}");
        Console.WriteLine($"Most borrowed category : {topCat} ({topCount} loans)");
        Console.WriteLine($"Overdue books (>{LoanDays} days): {overdue.Count}");
        if (overdue.Count > 0)
        {
            Console.WriteLine();
            PrintHeader();
            foreach (Loan l in overdue) PrintLoan(l);
        }
    }
}
