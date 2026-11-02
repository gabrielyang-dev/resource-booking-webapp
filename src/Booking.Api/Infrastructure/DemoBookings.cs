using Booking.Api.Domain;

namespace Booking.Api.Infrastructure;

// Demo bookings around "now": four past weeks plus the current week, so Insights and history look real,
// and a ready-made clash for the walkthrough. Generated from a fixed pattern (no randomness) every reset.
//
// Past bookings are inserted directly, not through BookingManager: the rules would rightly refuse them
// for starting in the past. The pattern itself never overlaps on one resource, so the database constraint holds.
//
// Walkthrough scenarios on the next weekday ("the demo day"):
// - Clash: Bob holds Room A 10:00 to 11:00, so Alice asking for 10:30 gets SLOT_TAKEN.
// - Rule rejection: Room A 17:00 to 19:30 breaks opening hours (08:00 to 18:00) and the 2-hour maximum.
// - Room B is free all day, so it appears as an alternative.
// - Add-on notice: Alice's Boardroom lunch 12:00 to 13:00 has 6 sandwiches (24 hours' notice).
// - Check-in: past meeting-room bookings include released no-shows, so Insights shows booked, checked-in and no-show hours.
public static class DemoBookings
{
    private record Slot(Guid ResourceId, int StartHour, int StartMinute, int EndHour, int EndMinute, Guid? Owner = null);

    public static DateOnly DemoDay(DateTime nowUtc, TimeZoneInfo zone)
    {
        var day = DateOnly.FromDateTime(LocalTime.ToLocal(nowUtc, zone)).AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            day = day.AddDays(1);
        }
        return day;
    }

    public static List<Domain.Booking> Create(DateTime nowUtc)
    {
        var zone = LocalTime.Zone("Europe/London");
        var today = DateOnly.FromDateTime(LocalTime.ToLocal(nowUtc, zone));
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var demoDay = DemoDay(nowUtc, zone);
        Guid[] owners = [DemoData.AdminId, DemoData.AliceId, DemoData.BobId];

        var bookings = new List<Domain.Booking>();
        var dayIndex = 0;
        for (var date = thisMonday.AddDays(-28); date < thisMonday.AddDays(5); date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }
            var slots = SlotsFor(dayIndex);
            for (var s = 0; s < slots.Count; s++)
            {
                var slot = slots[s];
                // Meeting rooms on the demo day are left for the scripted scenarios below.
                if (date == demoDay && IsMeetingRoom(slot.ResourceId))
                {
                    continue;
                }
                var start = LocalTime.ToUtc(date, new TimeOnly(slot.StartHour, slot.StartMinute), zone);
                var end = LocalTime.ToUtc(date, new TimeOnly(slot.EndHour, slot.EndMinute), zone);
                // Skip anything happening right now: it would be neither finished nor bookable.
                if (start < nowUtc && nowUtc < end)
                {
                    continue;
                }
                var booking = New(slot.ResourceId, slot.Owner ?? owners[(dayIndex + s) % owners.Length], start, end, BookingStatus.Confirmed, nowUtc);
                if ((dayIndex * 7 + s) % 10 == 3)
                {
                    booking.Status = BookingStatus.Cancelled;
                }
                else if (end <= nowUtc)
                {
                    SetPastOutcome(booking, slot.ResourceId, dayIndex, s);
                }
                bookings.Add(booking);
            }
            dayIndex++;
        }

        bookings.Add(New(DemoData.RoomAId, DemoData.BobId,
            LocalTime.ToUtc(demoDay, new TimeOnly(10, 0), zone), LocalTime.ToUtc(demoDay, new TimeOnly(11, 0), zone), BookingStatus.Confirmed, nowUtc));
        // Alice's lunch meeting comes with sandwiches, so moving it to today shows the 24 hours' notice rule.
        var lunch = New(DemoData.BoardroomId, DemoData.AliceId,
            LocalTime.ToUtc(demoDay, new TimeOnly(12, 0), zone), LocalTime.ToUtc(demoDay, new TimeOnly(13, 0), zone), BookingStatus.Confirmed, nowUtc);
        lunch.AddOnOrders.Add(new AddOnOrder { Id = Guid.NewGuid(), AddOnId = DemoData.SandwichesId, Quantity = 6, Status = AddOnOrderStatus.Requested });
        bookings.Add(lunch);
        return bookings;
    }

    // One weekday's pattern. Room A is the busiest (about 60% of 08:00 to 18:00), Room B about 35%,
    // the Boardroom about 15%. Alice and Bob use their usual desks, and the pool car goes out twice a week.
    private static List<Slot> SlotsFor(int dayIndex)
    {
        var slots = new List<Slot>
        {
            new(DemoData.RoomAId, 9, 0, 10, 0),
            new(DemoData.RoomAId, 10, 0, 11, 30),
            new(DemoData.RoomAId, 15, 30, 17, 0),
            new(DemoData.RoomBId, 9, 30, 11, 0),
            new(DemoData.RoomBId, 14, 0, 16, 0),
        };
        if (dayIndex % 5 != 2)
        {
            slots.Add(new(DemoData.RoomAId, 13, 0, 15, 0));
        }
        if (dayIndex % 3 == 0)
        {
            slots.Add(new(DemoData.RoomBId, 11, 0, 12, 0));
        }
        if (dayIndex % 2 == 0)
        {
            slots.Add(new(DemoData.BoardroomId, 10, 0, 12, 0));
        }
        if (dayIndex % 5 != 4)
        {
            slots.Add(new(DemoData.Desk1Id, 9, 0, 17, 0, DemoData.AliceId));
        }
        if (dayIndex % 2 == 0)
        {
            slots.Add(new(DemoData.Desk2Id, 8, 30, 16, 30, DemoData.BobId));
        }
        if (dayIndex % 5 is 1 or 3)
        {
            slots.Add(new(DemoData.PoolCarId, 8, 0, 17, 0));
        }
        return slots;
    }

    // How a past booking turned out. Meeting rooms require check-in, so about one in nine was a no-show
    // and got released; the rest checked in a few minutes after the start. Desks and the car don't require it,
    // and about one in four times people didn't bother: completed, but never checked in.
    private static void SetPastOutcome(Domain.Booking booking, Guid resourceId, int dayIndex, int slotIndex)
    {
        if (IsMeetingRoom(resourceId))
        {
            if ((dayIndex * 3 + slotIndex) % 9 == 4)
            {
                booking.Status = BookingStatus.Released;
                return;
            }
            booking.Status = BookingStatus.Completed;
            booking.CheckedInAt = booking.Start.AddMinutes(3);
            return;
        }
        booking.Status = BookingStatus.Completed;
        booking.CheckedInAt = (dayIndex + slotIndex) % 4 == 0 ? null : booking.Start.AddMinutes(5);
    }

    private static bool IsMeetingRoom(Guid resourceId) =>
        resourceId == DemoData.RoomAId || resourceId == DemoData.RoomBId || resourceId == DemoData.BoardroomId;

    private static Domain.Booking New(Guid resourceId, Guid ownerId, DateTime start, DateTime end, BookingStatus status, DateTime nowUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            ResourceId = resourceId,
            OwnerId = ownerId,
            Start = start,
            End = end,
            Status = status,
            CreatedAt = start < nowUtc ? start.AddDays(-2) : nowUtc,
        };
}
