using System;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Testing;
using Antmicro.Renode.Utilities;


namespace Antmicro.Renode.RobotFramework
{
    internal class InterruptKeywords : TestersProvider<InterruptTester, IPeripheral>, IRobotFrameworkKeywordProvider
    {
        public void Dispose()
        {
        }

        [RobotFrameworkKeyword(replayMode: Replay.Always)]
        public int CreateInterruptTester(string periph, string interruptName = "IRQ" ,string machine = null)
        {
            this.interruptName = interruptName;
            return CreateNewTester(periphObject => new InterruptTester(periphObject, interruptName), periph, machine);
        }
         [RobotFrameworkKeyword]
        public void SetInterruptLevel(bool active, int? testerId = null)
        {   
            activeLevel = active;
        }

        [RobotFrameworkKeyword]
        public void TriggerInterrupt(int? testerId = null)
        {   //return hex string compatible with robot framework
            if (GetTesterOrThrowException(testerId).SetInterrupt(activeLevel) != true)
            {
                throw new InvalidOperationException("Interrupt " + interruptName + " not found");
            }
        }

        [RobotFrameworkKeyword]
        public void TriggerInterruptPulse(int us, int? testerId = null)
        {   //return hex string compatible with robot framework
            if (GetTesterOrThrowException(testerId).GenerateInterruptPulse(activeLevel, (uint)us) != true)
            {
                throw new InvalidOperationException("Interrupt " + interruptName + " not found");
            }
        }

        bool activeLevel = true;
        string interruptName;
    }
}