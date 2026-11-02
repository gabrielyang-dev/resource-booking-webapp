namespace Booking.Api.Features.Catalog;

public static class TimeZoneIds
{
    // True only for an IANA id written exactly as IANA writes it, such as "Europe/London".
    //
    // TryConvertIanaIdToWindowsId knows only real IANA ids, and it is case-sensitive, on Windows and
    // Linux alike. FindSystemTimeZoneById is not a good test on its own: Windows accepts Windows ids
    // and, depending on what it has cached, ids in the wrong case.
    //
    // Trade-off: an IANA zone that has no Windows equivalent is rejected too. Admins can use the
    // nearest zone that has one.
    public static bool IsIana(string id)
    {
        if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _))
        {
            return false;
        }
        // Also check that the zone can be loaded here, so it can be used for local-time rules later.
        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
    }
}
