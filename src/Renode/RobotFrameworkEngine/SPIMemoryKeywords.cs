using System;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Testing;
using Antmicro.Renode.Peripherals.Cutter.SPIDevices;
using Antmicro.Renode.Utilities;


namespace Antmicro.Renode.RobotFramework
{
    internal class SPIMemoryKeywords : TestersProvider<SPIMemoryTester, BaseMemorySpi>, IRobotFrameworkKeywordProvider
    {
        public void Dispose()
        {
        }

        [RobotFrameworkKeyword(replayMode: Replay.Always)]
        public int CreateSPIMemoryTester(string spiMemory, string machine = null)
        {
            return CreateNewTester(spiMemoryObject => new SPIMemoryTester(spiMemoryObject), spiMemory, machine);
        }

        /// <summary>
        /// Read data from SPI memory and return it as a hex string.
        /// </summary>
        /// <param name="address">rr</param>
        /// <param name="count">rr</param>
        /// <param name="testerId">rr</param>
        /// <returns></returns>
        [RobotFrameworkKeyword]
        public string ReadSPIMemoryAsHex(int address, int count, int? testerId = null)
        {   //return hex string compatible with robot framework
            byte[] bytes = GetTesterOrThrowException(testerId).Read(address, count);
            return 	BitConverter.ToString(bytes).Replace("-", " ");
        }

        [RobotFrameworkKeyword]
        public bool CompareSPIMemory(int address, byte[] data, int? testerId = null)
        {
            return GetTesterOrThrowException(testerId).CompareMemory(address, data);
        }
    }
}