namespace Shelly_Notifications.Services;

public static class NextNotification
{
    public static TimeSpan GetNextNotificationTime(List<DayOfWeek> daysOfWeek, TimeOnly? time, TimeSpan fallback)
    {
        if (time == null || daysOfWeek.Count == 0)
            return fallback;

        var now = DateTime.Now;
        var targetTime = time.Value;
        var targetDaysSet = daysOfWeek.ToHashSet();

        var todayAtTargetTime = now.Date.Add(targetTime.ToTimeSpan());
        if (targetDaysSet.Contains(now.DayOfWeek) && todayAtTargetTime > now)
        {
            return todayAtTargetTime - now;
        }
        
        for (var daysAhead = 1; daysAhead <= 7; daysAhead++)
        {
            var nextDate = now.Date.AddDays(daysAhead);
            if (targetDaysSet.Contains(nextDate.DayOfWeek))
            {
                return nextDate.Add(targetTime.ToTimeSpan()) - now;
            }
        }

        return fallback;
    }
}