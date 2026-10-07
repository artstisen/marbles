using System.Globalization;
using System.Text;

namespace Marbles;

public static class IsoDateTime
{
    #region ISO 8601 Timestamp

    // ISO-8601 datetime
    // Get culture invariant datetime string
    /// yyyy-MM-dd HH:mm:ss
    private static string _tempDateTimeIso = string.Empty;

    public static string GetDateTimeNow()
    {
        _tempDateTimeIso = "";
        _tempDateTimeIso += DateTime.Now.Year.ToString();
        _tempDateTimeIso += "-";
        _tempDateTimeIso += DateTime.Now.Month.ToString("00");
        _tempDateTimeIso += "-";
        _tempDateTimeIso += DateTime.Now.Day.ToString("00");
        _tempDateTimeIso += " ";
        _tempDateTimeIso += DateTime.Now.Hour.ToString("00");
        _tempDateTimeIso += ":";
        _tempDateTimeIso += DateTime.Now.Minute.ToString("00");
        _tempDateTimeIso += ":";
        _tempDateTimeIso += DateTime.Now.Second.ToString("00");
        return _tempDateTimeIso;
    }

    public static string GetDateTimeNowCompressed(string divider)
    {
        _tempDateTimeIso = "";
        _tempDateTimeIso += DateTime.Now.Year.ToString();
        _tempDateTimeIso += DateTime.Now.Month.ToString("00");
        _tempDateTimeIso += DateTime.Now.Day.ToString("00");
        _tempDateTimeIso += divider;
        _tempDateTimeIso += DateTime.Now.Hour.ToString("00");
        _tempDateTimeIso += DateTime.Now.Minute.ToString("00");
        _tempDateTimeIso += DateTime.Now.Second.ToString("00");
        return _tempDateTimeIso;
    }

    #endregion

    #region Calendar with ISO week-numbers

    private static DateTime _date = DateTime.Now;
    private static int _count;

    public static string ConfigureCalendar()
    {
        Tools.ClearScreen();
        Menu.WriteModeLabel("Calendar", "Select options and press Enter (Esc to exit)", 0);
        if (DrawCalendarSelector())
        {
            Tools.ClearScreen();
            return GetCalendar(_date.Year, _date.Month, _count).Item1;
        }

        Tools.ClearScreen();
        return string.Empty;
    }

    private static bool DrawCalendarSelector()
    {
        bool select = true;
        int selectionIndex = 3;
        _date = DateTime.Now;
        _count = 1;
        while (select)
        {
            Tools.CursorTopLeft();
            Tools.CursorDown(3);
            // Clear the three lines before writing them again
            for (int i = 0; i < 3; i++)
            {
                Console.Write(new string(' ', 40));
                Tools.CursorReturn();
                Tools.CursorDown();
            }

            Tools.CursorTopLeft();
            Tools.CursorDown(3);
            Console.Write(new string(' ', 3) + "<- " + _date.Year + " ->" + Entries.NewLineCharString);
            Console.Write(new string(' ', 3) + "<- " + _date.ToString("MMMM", CultureInfo.InvariantCulture) + " ->" +
                          Entries.NewLineCharString);
            Console.Write(new string(' ', 3) + "<- " + GetMonthsCount(_count) + " ->");
            Tools.CursorTopLeft();
            Tools.CursorDown(selectionIndex);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write("->");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            // Take input and act on Up/Down, Left/right, Esc and Enter
            var c = Keyboard.ReadNavigationKey();
            switch (c.Key)
            {
                case ConsoleKey.Escape:
                    select = false; // Exit
                    break;
                case ConsoleKey.UpArrow:
                    if (selectionIndex > 3)
                        selectionIndex -= 1;
                    else
                    {
                        selectionIndex = 5;
                    }

                    break;
                case ConsoleKey.DownArrow:
                    if (selectionIndex < 5)
                        selectionIndex += 1;
                    else
                    {
                        selectionIndex = 3;
                    }

                    break;
                case ConsoleKey.LeftArrow:
                    switch (selectionIndex)
                    {
                        case 3:
                            _date = _date.AddYears(-1);
                            break;
                        case 4:
                            _date = _date.AddMonths(-1);
                            break;
                        case 5:
                            switch (_count)
                            {
                                case > 1 and <= 6:
                                    _count -= 1;
                                    break;
                                case > 6:
                                    _count = 6;
                                    break;
                                default:
                                    _count = 12;
                                    break;
                            }

                            break;
                    }

                    break;
                case ConsoleKey.RightArrow:
                    switch (selectionIndex)
                    {
                        case 3:
                            _date = _date.AddYears(1);
                            break;
                        case 4:
                            _date = _date.AddMonths(1);
                            break;
                        case 5:
                            switch (_count)
                            {
                                case < 6:
                                    _count += 1;
                                    break;
                                case > 6:
                                    _count = 1;
                                    break;
                                default:
                                    _count = 12;
                                    break;
                            }

                            break;
                    }


                    break;
                case ConsoleKey.Enter:
                    return true;
            }
        }

        return false;
    }
    
    private static bool DrawYearSelector()
    {
        _date = DateTime.Now;
        _count = 1;
        while (true)
        {
            // Clear line before writing again
            Tools.CursorTopLeft();
            Tools.CursorDown(3);
            Console.Write(new string(' ', 40));
            // Write year selector
            Tools.CursorTopLeft();
            Tools.CursorDown(3);
            Console.Write(new string(' ', 3) + "<- " + _date.Year + " ->" + Entries.NewLineCharString);
            
            // Take input and act on Left/right, Esc and Enter
            var c = Keyboard.ReadNavigationKey();
            switch (c.Key)
            {
                case ConsoleKey.Escape:// Exit
                    return false;
                case ConsoleKey.LeftArrow:
                    _date = _date.AddYears(-1);
                    break;
                case ConsoleKey.RightArrow:
                    _date = _date.AddYears(1);
                    break;
                case ConsoleKey.Enter:
                    CalendarYear = _date.Year;
                    return true;
            }
        }
    }

    private static string GetMonthsCount(int count)
    {
        if (count is >= 1 and <= 6)
        {
            if (count == 1)
                return count + " month";
            else
            {
                return count + " months";
            }
        }
        else
        {
            return "Year";
        }
    }

    private static (string, string, string) GetCalendar(int year, int month, int count)
    {
        var output = new StringBuilder();
        var title = string.Empty;
        var weekNumberFirstLast = string.Empty;
        for (int j = 0; j < count; j++)
        {
            var date = new DateTime(year, month, 1);
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var monthName = date.ToString("MMMM", CultureInfo.InvariantCulture);
            title = (monthName + new string(' ', (10 - monthName.Length)) + date.Year);
            output.Append("*** " + title + " ***" +
                          "\n");
            // DayOfWeek enumeration is not localized (always english)
            for (int i = 0; i < daysInMonth; i++)
            {
                var weekNumber = GetIso8601WeekNumber(date.AddDays(i));
                if (date.AddDays(i).DayOfWeek == DayOfWeek.Monday)
                {
                    output.Append("-- Week " + weekNumber + "\n"); 
                }
                
                // Calculate week number range if 1 month
                if (count == 1 && i == 0)
                {
                    weekNumberFirstLast = "[week " + weekNumber.ToString("00");
                }
                else if (count == 1 && i == daysInMonth - 1)
                {
                    weekNumberFirstLast += "-" + weekNumber.ToString("00") + "]";
                }

                output.Append((i + 1).ToString("00") + " " + date.AddDays(i).DayOfWeek.ToString()[..3] + "\n");
            }

            // Keep it within 12 months
            if (month + 1 <= 12)
            {
                month += 1;
            }
            else
            {
                break;
            }
        }

        return (output.ToString(), title, weekNumberFirstLast);
    }

    internal static int CalendarYear { get; set; }

    internal static bool SelectYear()
    {
        Tools.ClearScreen();
        Menu.WriteModeLabel("Calendar", "Select year and press Enter (Esc to exit)", 0);
        return DrawYearSelector();
    }

    internal static void CreateYearCalendar()
    {
        for (int i = 0; i < 12; i++)
        {
            var (item1, item2, item3) = GetCalendar(CalendarYear, (i + 1), 1);
            var newEntry = new Entry(string.Empty, string.Empty);
            // Create the title: "a - January 2022 [week 1-5]"
            newEntry.Title = item2 + " " + item3;
            // Add the content
            newEntry.Text = item1;
            // Set the tags and sorting index
            newEntry.Options = Entries.FavoritesTag.ToString() + Entries.PinnedTag + i;
            if (i == 0)
                newEntry.Options += Entries.HighlightType2;
            // Add the entry to the collection
            Entries.AddEntryToEntries(newEntry);
        }
        // Save the file
        Entries.SaveEntriesToFile();
    }

    internal static int GetIso8601WeekNumber(DateTime date)
    {
        // Calculates the ISO 8601 Week-number
        // In this scenario the first day of the week is monday, 
        // and the week rule states that:
        // [...] the first calendar week of a year is the one 
        // that includes the first Thursday of that year and 
        // [...] the last calendar week of a calendar year is 
        // the week immediately preceding the first 
        // calendar week of the next year.
        // The first week of the year may thus start in the 
        // preceding year

        const int jan = 1;
        const int dec = 12;
        const int lastDayOfDec = 31;
        const int firstDayOfJan = 1;
        const int thursday = 4;
        bool thursdayFlag = false;

        // Get the day number since the beginning of the year
        int dayOfYear = date.DayOfYear;

        // Get the numeric weekday of the first day of the 
        // year (using sunday as FirstDay)
        int startWeekDayOfYear =
            (int) (new DateTime(date.Year, jan, firstDayOfJan)).DayOfWeek;
        int endWeekDayOfYear =
            (int) (new DateTime(date.Year, dec, lastDayOfDec)).DayOfWeek;

        // Compensate for the fact that we are using monday
        // as the first day of the week
        if (startWeekDayOfYear == 0)
            startWeekDayOfYear = 7;
        if (endWeekDayOfYear == 0)
            endWeekDayOfYear = 7;

        // Calculate the number of days in the first and last week
        int daysInFirstWeek = 8 - (startWeekDayOfYear);

        // If the year either starts or ends on a thursday it will have a 53rd week
        if (startWeekDayOfYear == thursday || endWeekDayOfYear == thursday)
            thursdayFlag = true;

        // We begin by calculating the number of FULL weeks between the start of the year and
        // our date. The number is rounded up, so the smallest possible value is 0.
        int fullWeeks = (int) Math.Ceiling((dayOfYear - (daysInFirstWeek)) / 7.0);
        int weekNumber = fullWeeks;

        // If the first week of the year has at least four days, then the actual week number for our date
        // can be incremented by one.
        if (daysInFirstWeek >= thursday)
            weekNumber = weekNumber + 1;

        // If week number is larger than week 52 (and the year doesn't either start or end on a thursday)
        // then the correct week number is 1. 
        if (weekNumber > 52 && !thursdayFlag)
            weekNumber = 1;

        // If week number is still 0, it means that we are trying to evaluate the week number for a
        // week that belongs in the previous year (since that week has 3 days or less in our date's year).
        // We therefore make a recursive call using the last day of the previous year.
        if (weekNumber == 0)
        {
            weekNumber = GetIso8601WeekNumber(
                new DateTime(date.Year - 1, dec, lastDayOfDec));
        }

        return weekNumber;
    }

    #endregion
}