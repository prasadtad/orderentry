namespace OrderEntry.Utils
{
    public static class DateUtils
    {
        public static DateOnly TodayEST
        {
            get
            {
                return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")));
            }
        }

        public static DateOnly GetFirstWorkingDayOfWeek(DateOnly date, Func<DateOnly, bool> isHoliday)
        {
            date = date.DayOfWeek == DayOfWeek.Sunday ? date.AddDays(-6) : date.AddDays(DayOfWeek.Monday - date.DayOfWeek + 1);
            while (isHoliday(date)) {
                date.AddDays(1);
            }
            return date;
        }

        public static DateOnly GetLastWorkingDay(DateOnly date, Func<DateOnly, bool> isHoliday)
        {
            while (date.DayOfWeek == DayOfWeek.Sunday || date.DayOfWeek == DayOfWeek.Saturday || isHoliday(date)) {
                date.AddDays(-1);
            }
            return date;
        }
    }
}
