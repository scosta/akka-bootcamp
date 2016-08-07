﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using System.Diagnostics;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;

namespace ChartApp.Actors
{
    public class PerformanceCounterCoordinatorActor : ReceiveActor
    {
        #region Message Types
        /// <summary>
        /// Subscribe the <see cref="ChartingActor"/> to updates for <see cref="Counter"/>
        /// </summary>
        public class Watch
        {
            public Watch(CounterType counter)
            {
                Counter = counter;
            }

            public CounterType Counter { get; private set; }
        }

        /// <summary>
        /// Unsubscribe the <see cref="ChartingActor"/> from updates to <see cref="Counter"/>
        /// </summary>
        public class Unwatch
        {
            public Unwatch(CounterType counter)
            {
                Counter = counter;
            }

            public CounterType Counter { get; private set; }
        }
        #endregion

        /// <summary>
        /// Methods for generating new instances of all <see cref="PerformanceCounter"/>s we want to monitor
        /// </summary>
        private static readonly Dictionary<CounterType, Func<PerformanceCounter>>
            CounterGenerators = new Dictionary<CounterType, Func<PerformanceCounter>>()
            {
                { 
                    CounterType.Cpu, 
                    () => new PerformanceCounter("Processor", "% Processor Time", "_Total", true)
                },
                {
                    CounterType.Memory,
                    () => new PerformanceCounter("Memory", "% Committed Bytes In Use", true)
                },
                {
                    CounterType.Disk,
                    () => new PerformanceCounter("LogicalDisk", "% Disk Time", "_Total", true)
                }
            };

        /// <summary>
        /// Methods for creating new <see cref="Series"/> with distinct colors and names corresponding
        /// to each <see cref="PerformanceCounter"/>
        /// </summary>
        private static readonly Dictionary<CounterType, Func<Series>> CounterSeries =
            new Dictionary<CounterType, Func<Series>>()
            {
                {
                    CounterType.Cpu,
                    () => new Series(CounterType.Cpu.ToString()) {
                        ChartType = SeriesChartType.SplineArea,
                        Color = Color.DarkGreen
                    }
                },
                {
                    CounterType.Memory,
                    () => new Series(CounterType.Memory.ToString()) {
                        ChartType = SeriesChartType.FastLine,
                        Color = Color.MediumBlue
                    }
                },
                {
                    CounterType.Disk,
                    () => new Series(CounterType.Disk.ToString()) {
                        ChartType = SeriesChartType.SplineArea,
                        Color = Color.DarkRed
                    }
                }
            };

        private Dictionary<CounterType, IActorRef> _counterActors;

        private IActorRef _chartingActor;

        public PerformanceCounterCoordinatorActor(IActorRef chartingActor) :
            this(chartingActor, new Dictionary<CounterType,IActorRef>())
        {
        }

        public PerformanceCounterCoordinatorActor(IActorRef chartingActor,
            Dictionary<CounterType, IActorRef> counterActors)
        {
            _chartingActor = chartingActor;
            _counterActors = counterActors;

            Receive<Watch>(watch =>
            {
                if (!_counterActors.ContainsKey(watch.Counter))
	            {
                    // Create a child actor to monitor this counter if one doesn't already exist
                    var counterActor = Context.ActorOf(Props.Create(() =>
                        new PerformanceCounterActor(watch.Counter.ToString(),
                            CounterGenerators[watch.Counter])));

                    // Add the counter to the collection
                    _counterActors[watch.Counter] = counterActor;
	            }

                // Register the series with the charting actor
                _chartingActor.Tell(new ChartingActor.AddSeries(
                    CounterSeries[watch.Counter]()));

                // Tell the counter actor to publish his stats to the chartingactor
                _counterActors[watch.Counter].Tell(new SubscribeCounter(watch.Counter, _chartingActor));
            });

            Receive<Unwatch>(unwatch =>
            {
                if (!_counterActors.ContainsKey(unwatch.Counter))
	            {
                    return;
	            }

                // Unsubscribe the ChartingActor from receiving any more updates
                _counterActors[unwatch.Counter].Tell(new UnsubscribeCounter(
                    unwatch.Counter, _chartingActor));
            });
        }
    }
}