using System.Globalization;
using System.Text;

namespace Marbles;

public static class IsoDateTime
{
    #region ISO 8601 Timestamp

    // A date with the day first, as an early version of Marbles wrote it: "17-11-2021 12:21:46"
    private static readonly System.Text.RegularExpressions.Regex DayFirst = new(
        @"^\s*(\d{1,2})[-./](\d{1,2})[-./](\d{4})(?:[ T]+(\d{1,2})[:.](\d{2})(?:[:.](\d{2}))?)?\s*$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// A timestamp written with the day first ("17-11-2021 12:21:46") turned around to the format of Marbles
    /// ("2021-11-17 12:21:46"), so it sorts by its date. A timestamp in the right format, and anything that is
    /// not a date with the day first, is returned as it is
    /// </summary>
    /// <param name="timestamp">The timestamp of an entry</param>
    /// <param name="corrected">True if it was turned around</param>
    internal static string CorrectDayFirst(string timestamp, out bool corrected)
    {
        corrected = false;
        if (string.IsNullOrEmpty(timestamp)) return timestamp;
        var match = DayFirst.Match(timestamp);
        if (!match.Success) return timestamp;
        int Number(int group) => match.Groups[group].Success ? int.Parse(match.Groups[group].Value) : 0;
        int day = Number(1), month = Number(2), year = Number(3);
        // Not a day and a month: the text is left alone
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(Math.Max(1, year), month)) return timestamp;
        int hour = Number(4), minute = Number(5), second = Number(6);
        if (hour > 23 || minute > 59 || second > 59) return timestamp;
        corrected = true;
        return year.ToString("0000") + "-" + month.ToString("00") + "-" + day.ToString("00") + " " +
               hour.ToString("00") + ":" + minute.ToString("00") + ":" + second.ToString("00");
    }

    // The time now as ISO-8601 (yyyy-MM-dd HH:mm:ss), the same in all cultures. The time is read once: read for each part, a date written at midnight could have the day of one day and the
    // month or year of the next ("2026-12-31" + a second = "2026-01-01")
    public static string GetDateTimeNow()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string GetDateTimeNowCompressed(string divider)
    {
        var now = DateTime.Now;
        return now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + divider +
               now.ToString("HHmmss", System.Globalization.CultureInfo.InvariantCulture);
    }

    #endregion

    #region ISO 8601 week number

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