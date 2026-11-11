namespace CineManagement.Common;

public static class VietnameseDateHelper
{
    public static string ToVietnameseDayOfWeek(this DateTime dt)
    {
        return dt.DayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ Hai",
            DayOfWeek.Tuesday => "Thứ Ba",
            DayOfWeek.Wednesday => "Thứ Tư",
            DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu",
            DayOfWeek.Saturday => "Thứ Bảy",
            DayOfWeek.Sunday => "Chủ Nhật",
            _ => "Thứ Hai"
        };
    }

    public static string ToVietnameseDayOfWeek(this DateTime? dt)
        => dt.HasValue ? dt.Value.ToVietnameseDayOfWeek() : string.Empty;

    public static string ToVietnameseDateString(this DateTime dt)
    {
        return $"{dt.ToVietnameseDayOfWeek()}, ngày {dt:dd/MM/yyyy}";
    }

    public static string ToVietnameseDateString(this DateTime? dt)
        => dt.HasValue ? dt.Value.ToVietnameseDateString() : string.Empty;

    public static string ToVietnameseDateTimeString(this DateTime dt)
    {
        return $"{dt:HH:mm} – {dt.ToVietnameseDayOfWeek()}, {dt:dd/MM/yyyy}";
    }

    public static string ToVietnameseDateTimeString(this DateTime? dt)
        => dt.HasValue ? dt.Value.ToVietnameseDateTimeString() : string.Empty;
}
