using System.Text.RegularExpressions;
using Serilog;
class EventConverter
{

    private string? _subjectRegEx { get; set; }

    /// <summary>
    /// Initial a new EventConvert object to convert event item to bandwidthaction
    /// </summary>
    /// <param name="subjectRegEx">Regular expression to match the event Subject</param>
    public EventConverter(string? subjectRegEx)
    {
        _subjectRegEx = subjectRegEx;
    }

    /// <summary>
    /// Convert a calendar event to a bandwidth action
    /// </summary>
    /// <param name="calendarItem">calendar event item</param>
    /// <param name="isStart">True if we should use the start bandwidth, false for the end bandwidth</param>
    /// <returns></returns>
    public BandwidthAction CalendarItemToBandwidthAction(CalendarItem calItem, bool isStart)
    {
        string startBandwidth = String.Empty;
        string endBandwidth = String.Empty;
        ValidateEventFormat(calItem, ref startBandwidth, ref endBandwidth);

        string time, bandwidth;
        if (isStart)
        {
            time = calItem.Start;
            bandwidth = startBandwidth;
        }
        else
        {
            time = calItem.End;
            bandwidth = endBandwidth;
        }

        // create new bandwidth item
        return new BandwidthAction {
            Time = DateTime.Parse(time),
            ServiceId = calItem.Location,
            Bandwidth = bandwidth,
            Priority = calItem.Count,
            CalendarItemId = calItem.Id
       };
    }

    /// <summary>
    /// Iterate through a set of calendar events and generate a new list of bandwidth change actions.
    /// Every calendar event has a start and end time, but not every one of those time periods should
    /// result in a bandwidth change.
    /// </summary>
    /// <param name="calItems">List of calendar events we receive from Graph API</param>
    /// <param name="startWindow">Start time</param>
    /// <param name="endWindow">End time</param>
    /// <returns>Narrowed list of occurrences when we need to actually change the bandwidth/returns>
    public List<BandwidthAction> BuildBandwidthActionsFromEvents(List<CalendarItem>? calItems, DateTime startWindow, DateTime endWindow)
    {
        List<BandwidthAction> actions = [];
        if (null == calItems)
        {
            return actions;
        }

        // what follows is a lot of logic to handle what calendar event start and end times
        // we want to use for bandwidth changes. We must handle back-to-back events and conflicting
        // meetings (and use priority indicators where possible)

        // Note: We receive the calendar events SORTED by earliest start time, which is important
        // in this logic (as the last item in our new list will always be the latest time)
        foreach (CalendarItem calItem in calItems)
        {
            DateTime calStartDate = DateTime.Parse(calItem.Start);
            DateTime calEndDate = DateTime.Parse(calItem.End);

            // validate the calendar meets the expected syntax, else ignore
            if(!ValidateEventFormat(calItem))
            {
                Log.Warning($"Event with subject '{calItem.Subject}' doesn't meet expected form");
                continue;
            }

            // if this is the first event and it's in our monitoring window, add it by default
            if (0 == actions.Count && calStartDate >= startWindow)
            {
                actions.Add(CalendarItemToBandwidthAction(calItem, true));
            }

            // if the start time of an event is the same as a bandwidth change already in our list,
            // we replace the previous event (i.e., with back-to-back calendar events, we only need
            // to set the bandwidth once -- at the start of the next event)
            else if (0 != actions.Count && 0 == DateTime.Compare(calStartDate, actions.Last().Time))
            {
                actions.RemoveAt(actions.Count - 1);
                actions.Add(CalendarItemToBandwidthAction(calItem, true));
            }

            // if the start time of an event comes after the last event in our list, its a new bandwidth
            // change we want to add to our list
            else if (0 != actions.Count && 0 < DateTime.Compare(calStartDate, actions.Last().Time))
            {
                actions.Add(CalendarItemToBandwidthAction(calItem, true));
            }
            else
            {
                // if the start time of this event is NOT later than the last item in the list,
                // the _one_ exception we want to make is replacing the last item in the list if
                // this event has HIGHER priority (based on logic that Category tag count will
                // represent Priority) 
                if (0 != actions.Count && calItem.Count > actions.Last().Priority)
                {
                    actions.RemoveAt(actions.Count - 1);
                    actions.Add(CalendarItemToBandwidthAction(calItem, true));
                }
            }

            // and now we need to do SIMILAR to evalute the end time of the event for another
            // possible bandwidth change

            // if the event ends after our monitoring window, we can ignore it
            if (calEndDate > endWindow)
            { 
                continue;
            }

            // if the event time of an event comes after the last event in our list or our list is empty,
            // its a new bandwidth change we want to add to our list
            else if (0 == actions.Count || 0 < DateTime.Compare(calEndDate, actions.Last().Time))
            {
                actions.Add(CalendarItemToBandwidthAction(calItem, false));
            }
            else
            {
                // as above, when an event conflicts with another (end time is less than or equal
                // the previous event), we factor in priority logic to determine if this new event
                // time should win or be ignored
                if (calItem.Count > actions.Last().Priority)
                {
                    actions.RemoveAt(actions.Count - 1);
                    actions.Add(CalendarItemToBandwidthAction(calItem, false));
                }
            }
        }

        return actions;
    }

    /// <summary>
    /// Calendar events must adhere to expected syntax with Subject format and Location value
    /// </summary>
    /// <param name="calItem">Calendar item</param>
    /// <returns>True if matches expected format</returns>
    private bool ValidateEventFormat(CalendarItem calItem)
    {
        string start = String.Empty;
        string end = String.Empty;
        return ValidateEventFormat(calItem, ref start, ref end);
    }

    /// <summary>
    /// Calendar events must adhere to expected syntax with Subject format and Location value
    /// </summary>
    /// <param name="calItem">Calendar event</param>
    /// <param name="startBandwidth">Found starting bandwidth value in Subject line</param>
    /// <param name="endBandwidth">Found ending bandwidth value in Subject line</param>
    /// <returns>True if matches expected format</returns>
    private bool ValidateEventFormat(CalendarItem calItem, ref string startBandwidth, ref string endBandwidth)
    {
        if (String.IsNullOrEmpty(calItem.Subject) || String.IsNullOrEmpty(calItem.Location) || String.IsNullOrEmpty(_subjectRegEx))
        {
            return false;
        }

        Regex regex = new(_subjectRegEx, RegexOptions.IgnoreCase);
        Match match = regex.Match(calItem.Subject);

        if (match.Success)
        {
            startBandwidth = match.Groups["startVal"].Value;
            endBandwidth = match.Groups["endVal"].Value;

            return true;
        }

        return false;
    }





}