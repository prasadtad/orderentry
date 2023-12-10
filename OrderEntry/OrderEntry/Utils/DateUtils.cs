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
    }
}