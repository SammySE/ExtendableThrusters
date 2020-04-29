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
using Sandbox.Common.ObjectBuilders;

namespace IngameScript
{
    partial class Program
    {
        private class Thruster
        {
            // NTH Autoconfig: Give list of thruster names via ini, then go over the grid to detect what needs to be
            // assigned to which thruster. Possibly check for proper setup as well. Store assigned terminal blocks
            // with entity IDs or block names in customdata. Get direction of thrusters via grid alignment

            // Mirrored setup: Try blueprinting one quarter of the setup and pasting it aligned differently

            // Improve IsLocked()
            
            // Cover cases of orientation "on the way"

            // NTH Try to save current operation

            // Make rotor and piston movement smooth and generic (utility class?)
            // - Create smooth piston movement (done)
            //   - Cover edge cases for pistons already moving
            // - Add smooth piston into all schedules (done)
            // - Create smooth rotor movment
            //   - Cover edge cases for rotors already moving
            //   - Cover edge cases for radian overflow
            // - Add smooth rotor into all schedules

            // NTH Rename comparison methods (utility class?)
            IMyShipMergeBlock MergeBlock1;
            IMyShipMergeBlock MergeBlock2;
            IMyShipConnector Connector1;
            IMyShipConnector Connector2;
            public IMyPistonBase Piston;
            public IMyMotorStator Motor;

            Program Program;

            public Thruster(Program program)
            {
                Program = program;
                MergeBlock1 = Program.GridTerminalSystem.GetBlockWithName("0 Merge Block") as IMyShipMergeBlock;
                MergeBlock2 = Program.GridTerminalSystem.GetBlockWithName("0 Merge Block 2") as IMyShipMergeBlock;
                Connector1 = Program.GridTerminalSystem.GetBlockWithName("0 Connector") as IMyShipConnector;
                Connector2 = Program.GridTerminalSystem.GetBlockWithName("0 Connector 2") as IMyShipConnector;
                Piston = Program.GridTerminalSystem.GetBlockWithName("0 Piston") as IMyPistonBase;
                Motor = Program.GridTerminalSystem.GetBlockWithName("0 Rotor") as IMyMotorStator;
            }

            public Orientation GetOrientation()
            {
                if (CompareAngleEquals(Motor.Angle, -0.25f * FullCircle, FullCircle / 100))
                    return Orientation.UP;
                else if (CompareAngleEquals(Motor.Angle, 0.0f, FullCircle / 100))
                    return Orientation.BASE;
                else if (CompareAngleEquals(Motor.Angle, 0.25f * FullCircle, FullCircle / 100))
                    return Orientation.DOWN;
                else if (Motor.Angle > -0.25f * FullCircle && Motor.Angle < 0.0f)
                    return Orientation.BASE_AND_UP;
                else
                    // TODO add failsafe here
                    return Orientation.BASE_AND_DOWN;
            }

            public bool IsLocked()
            {
                // TODO Improve the locked detection
                return MergeBlock1.Enabled;
            }

            private IEnumerable<int> MovePiston(float position)
            {
                float distance = position - Piston.CurrentPosition;
                float halfway = Math.Abs(distance / 2);
                float acceleration = 0.1f;

                if (distance < 0.0f)
                {
                    acceleration *= -1;
                    Piston.MinLimit = position;
                }
                else
                    Piston.MaxLimit = position;

                // Stop piston in case it is moving!
                Piston.Velocity = 0.0f;

                // Increase speed until half the distance is covered
                while (Math.Abs(position - Piston.CurrentPosition) > halfway)
                {
                    Program.debugoutput.WriteText($"Piston acceleration per 10 ticks: {acceleration}\n");
                    Program.debugoutput.WriteText($"Piston velocity atm: {Piston.Velocity}\n", true);
                    Program.debugoutput.WriteText($"Current distance remaining: {Math.Abs(position - Piston.CurrentPosition)}\n", true);
                    Piston.Velocity += acceleration;
                    yield return 10;
                }

                // Decrease speed until target extension is reached
                while (Math.Abs(position - Piston.CurrentPosition) > 0)
                {
                    Program.debugoutput.WriteText($"Piston acceleration per 10 ticks: {acceleration}\n");
                    Program.debugoutput.WriteText($"Piston velocity atm: {Piston.Velocity}\n", true);
                    Program.debugoutput.WriteText($"Current distance remaining: {Math.Abs(position - Piston.CurrentPosition)}\n", true);
                    if (Math.Abs(Piston.Velocity - acceleration) >= Math.Abs(acceleration))
                        Piston.Velocity -= acceleration;
                    else
                        Piston.Velocity = acceleration;
                    yield return 10;
                }

                Piston.Velocity = 0.0f;
            }

            private IEnumerable<int> TurnMotor(float position)
            {
                // REWRITE
                // Do one code for each direction! Easier to merge afterwards.
                // Start at 0.01 RPM (FullCircle / 100 / 60 seconds / 6 steps per second -> FullCircle / 36000)
                // Maybe increase speed later on
                // After half distance, start braking again
                // When getting close to the target, slow down massively to hit it exactly, with very low speed
                // Also, fix distance calculation. that seems to be wrong.
                float distance = position - Motor.Angle;
                float halfway = Math.Abs(distance / 2);
                float acceleration = FullCircle / 10000;

                if (distance < 0.0f)
                {
                    acceleration *= -1;
                    Motor.LowerLimitRad = position;
                }
                else
                    Motor.UpperLimitRad = position;

                // Stop motor in case it is moving!
                //Motor.TargetVelocityRad = 0.0f;

                // Increase speed until half the distance is covered
                while (Math.Abs(position - Motor.Angle) > halfway)
                {
                    Program.debugoutput.WriteText($"Motor acceleration per 10 ticks: {acceleration}\n");
                    Program.debugoutput.WriteText($"Motor target velocity atm: {Motor.TargetVelocityRad}\n", true);
                    Program.debugoutput.WriteText($"Current distance remaining: {Math.Abs(position - Motor.Angle)}\n", true);
                    Motor.TargetVelocityRad += acceleration;
                    yield return 10;
                }

                float halfwayAngle = Motor.Angle;
                // Decrease speed until target angle is reached
                while (Math.Abs(position - Motor.Angle) > FullCircle / 100)
                {
                    Program.debugoutput.WriteText($"Motor acceleration per 10 ticks: {acceleration}\n");
                    Program.debugoutput.WriteText($"Motor target velocity atm: {Motor.TargetVelocityRad}\n", true);
                    Program.debugoutput.WriteText($"Current distance remaining: {Math.Abs(position - Motor.Angle)}\n", true);
                    Program.debugoutput.WriteText($"Halfway angle: {halfwayAngle * 180 / Math.PI}\n", true);
                    if (Math.Abs(Motor.TargetVelocityRad - acceleration) >= Math.Abs(acceleration))
                        Motor.TargetVelocityRad -= acceleration;
                    else
                        Motor.TargetVelocityRad = acceleration;
                    yield return 10;
                }

                float almostAngle = Motor.Angle;
                acceleration *= 0.1f;

                while (Math.Abs(position - Motor.Angle) > 0)
                {
                    Program.debugoutput.WriteText($"Motor acceleration per 10 ticks: {acceleration}\n");
                    Program.debugoutput.WriteText($"Motor target velocity atm: {Motor.TargetVelocityRad}\n", true);
                    Program.debugoutput.WriteText($"Current distance remaining: {Math.Abs(position - Motor.Angle)}\n", true);
                    Program.debugoutput.WriteText($"Halfway angle: {halfwayAngle * 180 / Math.PI}\n", true);
                    Program.debugoutput.WriteText($"Almost-there angle: {almostAngle * 180 / Math.PI}\n", true);
                    yield return 10;
                }

                Motor.TargetVelocityRad = 0.0f;
                yield return 10;
            }

            public void Unlock()
            {
                Program.StepScheduler.AddSchedule(UnlockSchedule());
            }

            public void Lock()
            {
                Program.StepScheduler.AddSchedule(LockSchedule());
            }

            public void MoveToBase()
            {
                List<IEnumerable<int>> actions = new List<IEnumerable<int>>();

                Orientation orientation = GetOrientation();

                if (IsLocked())
                    actions.Add(UnlockSchedule());

                switch(orientation)
                {
                    case Orientation.UP:
                    case Orientation.BASE_AND_UP:
                        actions.Add(MoveFromUpToBaseSchedule());
                        break;
                    case Orientation.BASE:
                        actions.Add(MoveFromInToBaseSchedule());
                        break;
                    case Orientation.DOWN:
                    case Orientation.BASE_AND_DOWN:
                        actions.Add(MoveFromDownToBaseSchedule());
                        break;
                    default:
                        Program.Echo($"Illegal orientation: {Motor.Angle / FullCircle * 360}");
                        return;
                }

                Program.StepScheduler.AddSchedules(actions);
            }

            public void MoveToIn()
            {
                Orientation orientation = GetOrientation();
                bool isLocked = IsLocked();

                if (isLocked && orientation == Orientation.BASE)
                    // Already in
                    return;

                List<IEnumerable<int>> actions = new List<IEnumerable<int>>(5);

                if (isLocked)
                    actions.Add(UnlockSchedule());

                switch (orientation)
                {
                    case Orientation.UP:
                    case Orientation.BASE_AND_UP:
                        actions.Add(MoveFromUpToBaseSchedule());
                        break;
                    case Orientation.DOWN:
                    case Orientation.BASE_AND_DOWN:
                        actions.Add(MoveFromDownToBaseSchedule());
                        break;
                }

                actions.Add(MoveFromBaseToInSchedule());
                actions.Add(LockSchedule());

                Program.StepScheduler.AddSchedules(actions);
            }

            public void MoveToUp()
            {
                Orientation orientation = GetOrientation();
                bool isLocked = IsLocked();

                if (isLocked && orientation == Orientation.UP)
                    // Already up
                    return;

                List<IEnumerable<int>> actions = new List<IEnumerable<int>>();

                if (isLocked)
                    actions.Add(UnlockSchedule());

                if (orientation == Orientation.BASE)
                    actions.Add(MoveFromInToBaseSchedule());
                else if (orientation == Orientation.DOWN)
                    actions.Add(MoveFromDownToBaseSchedule());

                actions.Add(MoveFromBaseToUpSchedule());
                //actions.Add(LockSchedule());

                Program.StepScheduler.AddSchedules(actions);
            }

            public void MoveToDown()
            {
                Orientation orientation = GetOrientation();
                bool isLocked = IsLocked();

                if (isLocked && orientation == Orientation.DOWN)
                    // Already down
                    return;

                List<IEnumerable<int>> actions = new List<IEnumerable<int>>();

                if (isLocked)
                    actions.Add(UnlockSchedule());

                if (orientation == Orientation.BASE)
                    actions.Add(MoveFromInToBaseSchedule());
                else if (orientation == Orientation.UP)
                    actions.Add(MoveFromUpToBaseSchedule());

                actions.Add(MoveFromBaseToDownSchedule());
                actions.Add(LockSchedule());

                Program.StepScheduler.AddSchedules(actions);
            }

            private IEnumerable<int> UnlockSchedule()
            {
                // Current state: Locked, in any position
                // Target state: Unlocked, in same position

                Motor.Attach();
                // Deactivate merge blocks and connectors
                MergeBlock1.Enabled = false;
                MergeBlock2.Enabled = false;
                Connector1.Enabled = false;
                Connector2.Enabled = false;
                // Increase distance to ship to avoid clang
                Motor.Displacement = -0.4f;
                // Wait for everything to settle
                yield return 20;

                // Wait for rotor head to finish extending
                while (Motor.Displacement < -0.15f)
                {
                    Motor.Displacement += 0.01f;
                    yield return 10;
                }
            }

            private IEnumerable<int> LockSchedule()
            {
                // Current state: Unlocked, in any position
                // Target state: Locked, in same position

                // Retract rotor head to -0.35 (no collision) or -0.4(collision but final resting point)
                while (Motor.Displacement > -0.4f)
                {
                    Motor.Displacement -= 0.01f;
                    yield return 10;
                }

                // Activate merge blocks and connectors
                MergeBlock1.Enabled = true;
                MergeBlock2.Enabled = true;
                Connector1.Enabled = true;
                Connector2.Enabled = true;
                yield return 10;

                // Detach rotor head to avoid clang
                Motor.Detach();
            }

            private IEnumerable<int> MoveFromInToBaseSchedule()
            {
                // Current state: Unlocked, folded in
                // Target state: Unlocked, base position

                // Extend piston
                return MovePiston(10.0f);
            }

            private IEnumerable<int> MoveFromBaseToInSchedule()
            {
                // Current state: Unlocked, base position
                // Target state: Unlocked, folded in

                // Retract piston
                return MovePiston(2.3f);
            }

            private IEnumerable<int> MoveFromBaseToUpSchedule()
            {
                // Current state: Unlocked, base position
                // Target state: Unlocked, up position

                float targetPosition = -0.25f * FullCircle;
                Motor.LowerLimitRad = targetPosition;
                
                // We want a negative angle.
                // Distance to our angle is going to be difficult because: -359 = 1
                // -> let's ignore that for now. (TODO)
                
                // target position - current position
                float distance = -0.25f * FullCircle - Motor.Angle;
                float distanceAbs = Math.Abs(distance);

                // TODO find better solution for already moving rotors

                float maxSpeed = FullCircle * 3 / 60; // 5rpm in rad/sec
                float minSpeed = FullCircle / 1000 / 60; // 0.001 rpm in rad/sec
                // TRY THE SiMPLE WAY!
                // Set speed to remaining distance every new update
                int updateCounter = 0;
                int updateCounterMaxed = 0;
                int updateCounterMin = 0;
                int updateCounterStale = 0;
                float distanceLeft;
                float maxAcceleration = 0; // acceleration in rad/sec
                int maxAccelerationObservation = 6; // duration of observation of acceleration in units of 10 ticks
                float startAngle = Motor.Angle;

                // Measure initial acceleration
                // Calculate distance needed for braking
                // Brake in time
                // Possibly: Give desired turn time or speed, and adjust forces of rotor to match this (smooth turning)
                // Take into account: 1/6 second may be too big of a time frame for deciding to brake upon measurement at certain speeds.
                //     Initiate braking procedure with less torque, but earlier, to match the goal.

                // Go max speed while more than 2 seconds of rotation remain
                while (Math.Abs(distanceLeft = targetPosition - Motor.Angle) > maxSpeed)
                {
                    updateCounter++;
                    updateCounterMaxed++;
                    Program.debugoutput.WriteText("");
                    Debug("Motor target velocity atm: ", Motor.TargetVelocityRad);
                    Debug("Current distance remaining: ", distanceLeft);
                    Debug("Max. acceleration: ", maxAcceleration);
                    Program.debugoutput.WriteText($"Time taken: {updateCounter / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken capped: {updateCounterMaxed / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken below 1/100 circle: {updateCounterMin / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken stale below 1/100 circle: {updateCounterStale / 6f} seconds\n", true);
                    Debug("Capped speed to ", Math.Sign(distanceLeft) * maxSpeed);

                    Motor.TargetVelocityRad = Math.Sign(distanceLeft) * maxSpeed;

                    if (updateCounter - 1 == maxAccelerationObservation)
                    {
                        float s = Math.Abs(Motor.Angle - startAngle);
                        float t = maxAccelerationObservation;
                        float a = 2 * s / (t * t); // rad / 10ticks^2
                        maxAcceleration = (float) Math.Pow(Math.Sqrt(a) * 6, 2); // rad / s^2
                    }
                    yield return 10;
                }

                // Go "normal" speed while more than 1/100th of a full circle is left (speed = remaining rotation in one second)
                while (Math.Abs(distanceLeft = targetPosition - Motor.Angle) > 0.01 * FullCircle)
                {
                    updateCounter++;
                    if (Math.Abs(distanceLeft * 180 / Math.PI) < 1f)
                        updateCounterMin++;
                    Program.debugoutput.WriteText("");
                    Debug("Motor target velocity atm: ", Motor.TargetVelocityRad);
                    Debug("Current distance remaining: ", distanceLeft);
                    Debug("Max. acceleration: ", maxAcceleration);
                    Program.debugoutput.WriteText($"Time taken: {updateCounter / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken capped: {updateCounterMaxed / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken below 1/100 circle: {updateCounterMin / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken stale below 1/100 circle: {updateCounterStale / 6f} seconds\n", true);
                    Debug("Normal speed. ", 0.0f);

                    Motor.TargetVelocityRad = distanceLeft;
                    yield return 10;
                }

                // Stop decreasing speed until we'd be done within 0.5 seconds
                //while (Math.Abs(distanceLeft = targetPosition - Motor.Angle) > 0.01 * FullCircle)
                //while ((targetPosition - Motor.Angle) * 2 > Motor.TargetVelocityRad)
                //{
                //    updateCounter++;
                //    updateCounterMin++;
                //    updateCounterStale++;
                //    yield return 10;
                //}

                //updateCounter++;
                //updateCounterMin++;
                //updateCounterStale++;
                //yield return 10;

                // Finish rotation with double "normal" speed with precision of 1/20000th of a full circle (0.018 degree)
                while (Math.Abs(distanceLeft = targetPosition - Motor.Angle) > FullCircle / 20000)
                {
                    updateCounter++;
                    updateCounterMin++;
                    Program.debugoutput.WriteText("");
                    Debug("Motor target velocity atm: ", Motor.TargetVelocityRad);
                    Debug("Current distance remaining: ", distanceLeft);
                    Debug("Max. acceleration: ", maxAcceleration);
                    Program.debugoutput.WriteText($"Time taken: {updateCounter / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken capped: {updateCounterMaxed / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken below 1/100 circle: {updateCounterMin / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken stale below 1/100 circle: {updateCounterStale / 6f} seconds\n", true);
                    Debug("Double speed last part. ", 0.0f);

                    if (Motor.TargetVelocityRad > distanceLeft * 2) updateCounterStale++;
                    Motor.TargetVelocityRad = Math.Max(Motor.TargetVelocityRad, distanceLeft * 2);
                    yield return 10;
                }

                Motor.TargetVelocityRad = 0.0f;
                //Motor.RotorLock = true;
                while (true)
                {
                    Program.debugoutput.WriteText("");
                    Debug("Motor target velocity atm: ", Motor.TargetVelocityRad);
                    Debug("Current distance remaining: ", targetPosition - Motor.Angle);
                    Debug("Max. acceleration: ", maxAcceleration);
                    Program.debugoutput.WriteText($"Time taken: {updateCounter / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken capped: {updateCounterMaxed / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken below 1/100 circle: {updateCounterMin / 6f} seconds\n", true);
                    Program.debugoutput.WriteText($"Time taken stale below 1/100 circle: {updateCounterStale / 6f} seconds\n", true);
                    Program.debugoutput.WriteText("Locked.", true);
                    yield return 10;
                }
                yield return 120;
                Program.debugoutput.WriteText("DONE", true);

                // Retract piston
                //foreach (int value in MovePiston(7.4f))
                //    yield return value;
            }

            private void Debug(string stuff, double value)
            {
                Program.debugoutput.WriteText(stuff, true);
                Program.debugoutput.WriteText($"{Math.Round(value * 180 / Math.PI, 6)}\n", true);
                //Program.debugoutput.WriteText($"{Math.Round(value / FullCircle, 6)}\n", true);
            }

            private IEnumerable<int> MoveFromUpToBaseSchedule()
            {
                // Current state: Unlocked, up position
                // Target state: Unlocked, base position

                // Extend to 10m
                foreach (int value in MovePiston(10.0f))
                    yield return value;

                // Turn to base
                Motor.RotorLock = false;
                Motor.UpperLimitRad = 0.0f;
                Motor.TargetVelocityRad = FullCircle / 60;

                while (!CompareAngleEquals(Motor.Angle, Motor.UpperLimitRad))
                    yield return 10;
            }

            private IEnumerable<int> MoveFromBaseToDownSchedule()
            {
                // Current state: Unlocked, base position
                // Target state: Unlocked, down position

                // Turn downwards
                Motor.UpperLimitRad = 0.25f * FullCircle;
                Motor.TargetVelocityRad = 0.5f * FullCircle / 60;

                // Wait for motor to turn to 95%
                while (!CompareAngleEquals(Motor.Angle, Motor.UpperLimitRad - 0.0125 * FullCircle))
                    yield return 10;
                // Slow down motor to 10% for last 5%
                Motor.TargetVelocityRad = 0.05f * FullCircle / 60;
                while (!CompareAngleEquals(Motor.Angle, Motor.UpperLimitRad))
                    yield return 10;

                // Retract piston
                foreach (int value in MovePiston(7.4f))
                    yield return value;
            }

            private IEnumerable<int> MoveFromDownToBaseSchedule()
            {
                // Current state: Unlocked, down position
                // Target state: Unlocked, base position

                // Extend to 10m
                foreach (int value in MovePiston(10.0f))
                    yield return value;

                // Turn to base
                Motor.LowerLimitRad = 0.0f;
                Motor.TargetVelocityRad = -0.5f * FullCircle / 60;

                while (!CompareAngleEquals(Motor.Angle, Motor.LowerLimitRad))
                    yield return 10;
            }


            private bool CompareAngleEquals(double v1, double v2)
            {
                return Math.Abs(v1 - v2) < AngleEpsilon;
            }

            private bool CompareAngleEquals(double v1, double v2, double epsilon)
            {
                return Math.Abs(v1 - v2) < epsilon;
            }

            public enum Orientation
            {
                UP,
                BASE_AND_UP,
                BASE,
                BASE_AND_DOWN,
                DOWN
            }
        }
    }
}
