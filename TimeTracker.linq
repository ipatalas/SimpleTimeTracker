<Query Kind="Program">
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Diagnostics.Eventing.Reader</Namespace>
  <Namespace>System.Diagnostics.Contracts</Namespace>
</Query>

void Main()
{
	var calendar = new GregorianCalendar(GregorianCalendarTypes.Localized);
	var events = GetEvents(from: "2020-07-20")
		.OrderBy(x => x.TimeCreated)
		.Select(StateChangeEvent.FromEvent)
		.Where(WorkDaysOnly)
		.Where(NotHolidays)
		.Concat(GetStaticEvents())
		.OrderBy(x => x.Timestamp)
		//.Dump()
		.SkipWhile(x => x.Task != TaskEnum.WakingUp) // align starting point to Wake Up event
		.ChunkBy(2)
		.Select(ComputerRunningSpan.FromEvents) // flatten paired events
		.Select(NormalizeSpansTo(hoursAboveThat: 11, trimTo: 9));
		
	var grouped = events.GroupBy(x => x.Start.Date, x => x.Duration, (g, r) => new 
	{
		Day = g.ToShortDateString(),
		Week = g.DayOfWeek,
		WeekNo = calendar.GetWeekOfYear(g, CalendarWeekRule.FirstDay, DayOfWeek.Monday),
		Duration = Math.Round(r.Sum(x => x.TotalHours), 2)
	});
	
	var expectedHours = (grouped.Count() * 8);
	var workedHours = grouped.Sum(g => g.Duration);
	
	expectedHours.Dump("Expected hours");
	Math.Round(workedHours, 2).Dump("Worked hours");
	
	(DateTime.Now + TimeSpan.FromHours(expectedHours - workedHours)).ToShortTimeString().Dump("Estimated end of the day");

	grouped.GroupBy(x => x.WeekNo, x => x, (g, r) => new
	{
		Week = $"{g} ({r.First().Day})",
		Duration = Math.Round(r.Sum(x => x.Duration), 2),
		NumberOfDaysWorked = r.Count(),
		AverageHoursPerDay = Math.Round(r.Average(x => x.Duration), 2)
	}).Dump();

	grouped.Reverse().Take(5).Dump();	
	events.Reverse().Take(10).Dump();

	bool WorkDaysOnly(StateChangeEvent e)
	{
		return e.Timestamp.DayOfWeek != DayOfWeek.Saturday && e.Timestamp.DayOfWeek != DayOfWeek.Sunday;
	}

	bool NotHolidays(StateChangeEvent e)
	{
		var holidays = new List<TimeRange>()
		{
			//new TimeRange(DateTime.Parse("2020-06-11"), DateTime.Parse("2020-06-22")),
			//new TimeRange(DateTime.Parse("2020-06-29 20:00"), DateTime.Parse("2020-06-29 21:00"))	
		};
		
		return !holidays.Any(h => e.Timestamp > h.From && e.Timestamp < h.To);
	}

	IEnumerable<StateChangeEvent> GetStaticEvents()
	{
		yield return new StateChangeEvent() { Task = TaskEnum.EnteringSleep, Timestamp = DateTime.Parse("2020-07-23 11:00") }; 
		yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-07-23 12:20") }; // C
		
		yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-07-24 08:25") }; // C
		yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-07-27 08:05") }; // C
		yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-07-28 16:45") }; // C
		yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-07-28 18:40") }; // C
		
		yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-07-29 8:44") }; // C
		
		yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-08-17 8:45") }; // C
		yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-08-18 10:45") }; // C
		//
		//yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-07-14 08:30") }; 
		//yield return new StateChangeEvent() { Task = TaskEnum.WakingUp, Timestamp = DateTime.Parse("2020-07-14 20:15") }; 
		yield break;
	}

	List<EventRecord> GetEvents(string from)
	{
		var dateCondition = $"TimeCreated[@SystemTime>='{DateTime.Parse(from).Date.ToString("o")}']";

		var query = new EventLogQuery("System", PathType.LogName, $"*[System[Provider[@Name='Microsoft-Windows-Kernel-Power' or @Name='Microsoft-Windows-Power-Troubleshooter'] and (Level=4 or Level=0) and (EventID=1 or EventID=42) and {dateCondition}]]");
		var reader = new EventLogReader(query);
		
		var list = new List<EventRecord>();

		var item = reader.ReadEvent();
		while (item != null)
		{
			list.Add(item);
			item = reader.ReadEvent();
		}

		return list;
	}
}

Func<ComputerRunningSpan, ComputerRunningSpan> NormalizeSpansTo(int hoursAboveThat, int trimTo)
{
	return x => 
	{
		if (x.Duration > TimeSpan.FromHours(hoursAboveThat))
		{
			return new ComputerRunningSpan(x.Start, x.Start + TimeSpan.FromHours(trimTo));
		}
		
		return x;
	};
}

public class ComputerRunningSpan
{
	public DateTime Start { get; private set; }
	public DateTime End { get; private set; }
	public TimeSpan Duration {get; private set;}
	
	public ComputerRunningSpan(DateTime start, DateTime end)
	{
		this.Start = start;
		this.End = end;
		this.Duration = end - start;
	}

	public static ComputerRunningSpan FromEvents(IEnumerable<StateChangeEvent> events)
	{
		var start = events.SingleOrDefault(x => x.Task == TaskEnum.WakingUp);
		var end = events.SingleOrDefault(x => x.Task == TaskEnum.EnteringSleep);

		Contract.Assert(start != null, "Start event missing");

		return new ComputerRunningSpan(start.Timestamp, end?.Timestamp ?? DateTime.Now);
	}
}

public class StateChangeEvent
{
	public DateTime Timestamp { get; set; }
	public TaskEnum Task { get; set; }
	
	public static StateChangeEvent FromEvent(EventRecord record)
	{
		return new StateChangeEvent
		{
			Timestamp = record.TimeCreated.Value,
			Task = (TaskEnum)record.Task.Value
		};
	}
}

public class TimeRange
{
	public DateTime From {get;set;}
	public DateTime To {get;set;}
	
	public TimeRange(DateTime from, DateTime to)
	{
		this.From = from;
		this.To = to;
	}
}

// Define other methods, classes and namespaces here
public enum TaskEnum : int
{
	EnteringSleep = 64,
	WakingUp = 0
}

public static class Extensions
{
	public static IEnumerable<IEnumerable<TSource>> ChunkBy<TSource>(this IEnumerable<TSource> source, int chunkSize)
	{
		while (source.Any())                     // while there are elements left
		{   // still something to chunk:
			yield return source.Take(chunkSize); // return a chunk of chunkSize
			source = source.Skip(chunkSize);     // skip the returned chunk
		}
	}
}