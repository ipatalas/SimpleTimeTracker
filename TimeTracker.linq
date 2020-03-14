<Query Kind="Program">
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Diagnostics.Eventing.Reader</Namespace>
  <Namespace>System.Diagnostics.Contracts</Namespace>
</Query>

void Main()
{
	var calendar = new GregorianCalendar(GregorianCalendarTypes.Localized);

	var events = GetEvents(21)
		.OrderBy(x => x.TimeCreated)
		.Select(StateChangeEvent.FromEvent)
		.SkipWhile(x => x.Task != TaskEnum.WakingUp) // align starting point to Wake Up event
		.ChunkBy(2)
		.TakeWhile(x => x.Count() == 2) // skip remaining unpaired wake up event
		.Select(ComputerRunningSpan.FromEvents) // flatten paired events
		.Where(WorkDaysOnly);
		//.Dump();

	var grouped = events.GroupBy(x => x.Start.Date, x => x.Duration, (g, r) => new 
	{
		Day = g,
		Week = g.DayOfWeek,
		WeekNo = calendar.GetWeekOfYear(g, CalendarWeekRule.FirstDay, DayOfWeek.Monday),
		Duration = Math.Round(r.Sum(x => x.TotalHours), 2)
	})
	.Dump();
	//.Chart(x => x.Day, y => y.Duration, LINQPad.Util.SeriesType.Column).Dump();

	grouped.GroupBy(x => x.WeekNo, x => x, (g, r) => new 
	{
		WeekNo = g,
		Duration = Math.Round(r.Sum(x => x.Duration), 2)
	}).Dump();
	
	bool WorkDaysOnly(ComputerRunningSpan e)
	{
		return e.Start.DayOfWeek != DayOfWeek.Saturday && e.Start.DayOfWeek != DayOfWeek.Sunday;
	}

	List<EventRecord> GetEvents(int numberOfDays)
	{
		var dateCondition = $"TimeCreated[@SystemTime>='{DateTime.UtcNow.AddDays(-numberOfDays).ToString("o")}']";

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
		Contract.Assert(events.Count() == 2, "Wrong number of events");

		var start = events.SingleOrDefault(x => x.Task == TaskEnum.WakingUp);
		var end = events.SingleOrDefault(x => x.Task == TaskEnum.EnteringSleep);

		Contract.Assert(start != null && end != null);

		return new ComputerRunningSpan(start.Timestamp, end.Timestamp);
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