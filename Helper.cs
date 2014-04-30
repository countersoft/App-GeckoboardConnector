using System;

namespace GeckoboardConnector
{
    public static class Helper
    {
        public static DateTime GetStartDayOfWeek(DateTime dt, DayOfWeek startOfWeek = DayOfWeek.Monday)
        {
            int diff = dt.DayOfWeek - startOfWeek;
            
            if (diff < 0)
            {
                diff += 7;
            }

            return dt.AddDays(-1 * diff).Date;
        }

        public static DateTime GetStartDayOfMonth(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, 1);
        }

        public static DateTime GetLastDayOfMonth(DateTime dateTime)
        {   
            int dayInMonth = DateTime.DaysInMonth(dateTime.Year, dateTime.Month);

            return GetStartDayOfMonth(dateTime).AddDays(dayInMonth - 1);             
        }

        public static string GetFriendlyDate(double minutes)
        {
            TimeSpan ts = TimeSpan.FromMinutes(minutes);
            string result = "";

            if (ts.Days > 0) result += string.Format("{0}d ", ts.Days);
            if (ts.Hours > 0) result += string.Format("{0}h ", ts.Hours);
            if (ts.Minutes > 0) result += string.Format("{0}m", ts.Minutes);

            return result;
        }
    }
}
