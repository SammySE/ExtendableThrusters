using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using Sandbox.Engine.Physics;

namespace IngameScript
{
    partial class Program
    {
        public class Scheduler
        {
            List<Schedule> Schedules;
            Program Program;

            public Scheduler(Program Program)
            {
                // TODO scheduler is not yet resumable
                Schedules = new List<Schedule>();
                this.Program = Program;
            }

            public void DoWork()
            {
                // 16.6666 milliseconds are exactly one tick. (no, not 16.666 or 16.66666 or 16.666666666666)
                long elapsedTicks = (long) (Program.Runtime.TimeSinceLastRun.TotalMilliseconds / 16.6666);
                for (int i = (Schedules.Count - 1); i >= 0; i--)
                {
                    bool workRemaining = Schedules[i].DoNext(elapsedTicks);
                    if (!workRemaining)
                    {
                        Schedules.RemoveAtFast(i);
                    }
                }

                if (Schedules.Count == 0)
                {
                    // TODO maybe take other processes into account? Not sure.
                    Program.Runtime.UpdateFrequency = UpdateFrequency.None;
                }
            }

            private void ActivateUpdating()
            {
                // TODO find a proper frequence more dynamically based on communicated wait times of steps
                Program.Runtime.UpdateFrequency = UpdateFrequency.Update10;
            }

            public void AddSchedule(IEnumerable<int> steps)
            {
                Schedules.Add(new Schedule(steps));
                ActivateUpdating();
            }

            public void AddSchedules(IEnumerable<IEnumerable<int>> steps)
            {
                Schedules.Add(new Schedule(steps));
                ActivateUpdating();
            }

            private class Schedule
            {
                List<IEnumerator<int>> Jobs;
                int CurrentSequence = 0;
                public long CurrentRemainingTicks = 0;

                public Schedule(IEnumerable<int> jobs)
                {
                    Jobs = new List<IEnumerator<int>>
                    {
                        jobs.GetEnumerator()
                    };
                }

                public Schedule(IEnumerable<IEnumerable<int>> jobs)
                {
                    Jobs = new List<IEnumerator<int>>();
                    foreach (IEnumerable<int> job in jobs)
                    {
                        if (job != null)
                            Jobs.Add(job.GetEnumerator());
                    }
                }

                public bool DoNext(long elapsedTicks)
                {
                    CurrentRemainingTicks -= elapsedTicks;
                    if (CurrentRemainingTicks <= 0)
                    {
                        bool hasNext = Jobs[CurrentSequence].MoveNext();
                        if (hasNext)
                        {
                            CurrentRemainingTicks = Jobs[CurrentSequence].Current;
                        }
                        else
                        {
                            Jobs[CurrentSequence].Dispose();
                            // now get the next sequence
                            CurrentSequence += 1;
                            return CurrentSequence < Jobs.Count;
                        }
                    }
                    return true;
                }
                
            }
        }

    }
}
