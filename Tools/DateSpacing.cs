public static class DateSpacing
{
    public static DateTime CalculateNextDueAt(int consecutiveCorrect)
    {
        // base date
        var today = DateTime.UtcNow.Date;

        // intervals based on spaced repetition research
        int daysToAdd = consecutiveCorrect switch
        {
            1 => 1,     // first correct answer → next day
            2 => 6,     // second review after ~6 days
            3 => 15,    // longer spacing
            4 => 30,
            5 => 60,
            _ => 60,
        };

        return today.AddDays(daysToAdd);
    }
}