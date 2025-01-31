//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Testing;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.RobotFramework
{
    internal class RawUartKeywords : TestersProvider<RawUartTester, IUART>, IRobotFrameworkKeywordProvider
    {
        public void Dispose()
        {
        }

        [RobotFrameworkKeyword(replayMode: Replay.Always)]
        public int CreateRawUartTester(string uart, string machine = null)
        {
            return CreateNewTester(uartTestObject => new RawUartTester(uartTestObject), uart, machine);
        }

        [RobotFrameworkKeyword]
        public void UartWriteBytesAsHex(string hexBytes, int? testerId = null)
        {   
            string cleanedHex = hexBytes.Replace(" ", "");
            byte[] bytes = Convert.FromHexString(cleanedHex);
            GetTesterOrThrowException(testerId).WriteBytes(bytes);
        }

        [RobotFrameworkKeyword]
        public string UartReadNumberOfBytesAsHex(uint count, int? testerId = null)
        {
            byte[] bytes = GetTesterOrThrowException(testerId).ReadNumBytes(count);
            return BitConverter.ToString(bytes).Replace("-", " ");
        }

        [RobotFrameworkKeyword]
        public string UartReadBytesAsHexUntilTimeout(uint timeout, int? testerId = null)
        {
            byte[] bytes = GetTesterOrThrowException(testerId).ReadBytesFor(timeout);
            return BitConverter.ToString(bytes).Replace("-", " ");
        }
      
    }
}
