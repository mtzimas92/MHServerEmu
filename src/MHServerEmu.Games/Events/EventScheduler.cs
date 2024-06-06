﻿using MHServerEmu.Core.Logging;
using MHServerEmu.Core.System;

namespace MHServerEmu.Games.Events
{
    public class EventScheduler
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private readonly HashSet<ScheduledEvent> _scheduledEvents = new();

        private TimeSpan _currentTime;
        private bool _cancellingAllEvents = false;

        public EventScheduler()
        {
            _currentTime = Clock.GameTime;
        }

        public void ScheduleEvent<T>(EventPointer<T> eventPointer, TimeSpan timeOffset, EventGroup eventGroup) where T: ScheduledEvent, new()
        {
            if (_cancellingAllEvents) return;

            T @event = ConstructAndScheduleEvent<T>(timeOffset);
            // todo: add to event group
            eventPointer.Set(@event);
        }

        public void ScheduleEvent<T>(EventPointer<T> eventPointer, TimeSpan timeOffset) where T : ScheduledEvent, new()
        {
            ScheduleEvent(eventPointer, timeOffset, null);
        }

        public void RescheduleEvent<T>(EventPointer<T> eventPointer, TimeSpan timeOffset) where T: ScheduledEvent
        {
            throw new NotImplementedException();
        }

        public void CancelEvent<T>(EventPointer<T> eventPointer) where T: ScheduledEvent
        {
            CancelEvent(eventPointer);
        }

        public void CancelAllEvents()
        {
            _cancellingAllEvents = true;

            foreach (ScheduledEvent @event in _scheduledEvents)
                CancelEvent(@event);

            _cancellingAllEvents = false;
        }

        public void TriggerEvents()
        {
            _currentTime = Clock.GameTime;

            int numEvents = 0;

            foreach (ScheduledEvent @event in _scheduledEvents.Where(@event => @event.FireTime <= _currentTime))
            {
                _scheduledEvents.Remove(@event);
                @event.InvalidatePointers();
                @event.OnTriggered();
                numEvents++;
            }

            if (numEvents > 0) Logger.Debug($"Triggered {numEvents} event(s) ({_scheduledEvents.Count} more scheduled)");
        }

        private T ConstructAndScheduleEvent<T>(TimeSpan timeOffset) where T : ScheduledEvent, new()
        {
            T @event = new();

            if (timeOffset < TimeSpan.Zero)
            {
                Logger.Warn($"ConstructEvent<{typeof(T).Name}>(): timeOffset < TimeSpan.Zero");
                timeOffset = TimeSpan.Zero;
            }

            @event.FireTime = _currentTime + timeOffset;
            ScheduleEvent(@event);

            return @event;
        }

        private void ScheduleEvent(ScheduledEvent @event)
        {
            // Just add it to a HashSet for now
            _scheduledEvents.Add(@event);
        }

        private void CancelEvent(ScheduledEvent @event)
        {
            _scheduledEvents.Remove(@event);
            @event.InvalidatePointers();
            @event.OnCancelled();
        }
    }
}
