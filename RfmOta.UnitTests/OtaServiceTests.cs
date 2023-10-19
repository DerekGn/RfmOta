/*
* MIT License
*
* Copyright (c) 2022 Derek Goslin
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using FluentAssertions;
using HexIO;
using Microsoft.Extensions.Logging;
using Moq;
using RfmOta.Exceptions;
using RfmOta.Factory;
using RfmUsb.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace RfmOta.UnitTests
{
    public class OtaServiceTests
    {
        private readonly Mock<IIntelHexStreamReaderFactory> _mockIntelHexReaderFactory;
        private readonly Mock<IIntelHexStreamReader> _mockIntelHexStreamReader;
        private readonly Mock<IRfm69> _mockRfmUsb;
        private readonly OtaService _otaService;

        public OtaServiceTests()
        {
            _mockRfmUsb = new Mock<IRfm69>();
            _mockIntelHexStreamReader = new Mock<IIntelHexStreamReader>();
            _mockIntelHexReaderFactory = new Mock<IIntelHexStreamReaderFactory>();

            _otaService = new OtaService(
                Mock.Of<ILogger<IOtaService>>(),
                _mockRfmUsb.Object,
                _mockIntelHexReaderFactory.Object);
        }

        [Fact]
        public void TestGetFlashSizeOk()
        {
            // Arrange
            _mockRfmUsb.Setup(_ => _.TransmitReceive(It.IsAny<List<byte>>(), It.IsAny<int>()))
                .Returns(new List<byte>() { 13, (byte)ResponseType.FlashSize + 0x80, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55 });

            // Act
            var result = _otaService.GetFlashSize();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void TestOtaUpdate()
        {
            bool functionCalled = false;

            // Arrange
            var memoryStream = new MemoryStream();

            _otaService._steps = new List<Func<bool>>()
            {
                () =>
                {
                    functionCalled = true;
                    return true;
                }
            };

            // Act
            bool result = _otaService.OtaUpdate(1, memoryStream, out uint crc);

            // Assert
            result.Should().BeTrue();
            functionCalled.Should().BeTrue();
        }

        [Theory]
        [InlineData(-3)]
        [InlineData(21)]
        public void TestOtaUpdateInvalidPowerLevel(sbyte outputPower)
        {
            // Arrange
            var memoryStream = new MemoryStream();

            _otaService._steps = new List<Func<bool>>();

            // Act
            Action action = () => _otaService.OtaUpdate(outputPower, memoryStream, out uint crc);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>().WithMessage("Specified argument was out of the range of valid values. (Parameter 'outputPower')");
        }

        [Fact]
        public void TestPingBootLoaderOk()
        {
            // Arrange
            _mockRfmUsb.Setup(_ => _.TransmitReceive(It.IsAny<List<byte>>(), It.IsAny<int>()))
                .Returns(new List<byte>() { 1, (byte)ResponseType.Ping + 0x80 });

            // Act
            var result = _otaService.PingBootLoader();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void TestRebootOk()
        {
            // Arrange
            _mockRfmUsb.Setup(_ => _.Transmit(It.IsAny<List<byte>>()));

            // Act
            var result = _otaService.Reboot();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void TestSendHexDataMax()
        {
            // Arrange
            _mockIntelHexReaderFactory
                .Setup(_ => _.Create(It.IsAny<Stream>()))
                .Returns(_mockIntelHexStreamReader.Object);

            _mockIntelHexStreamReader
                .SetupSequence(_ => _.ReadHexRecord())
                .Returns(new IntelHexRecord(0x100, IntelHexRecordType.Data, (new byte[200]).ToList()))
                .Returns(new IntelHexRecord(0x000, IntelHexRecordType.EndOfFile, new List<byte>() { }));

            _mockRfmUsb
                .Setup(_ => _.TransmitReceive(It.IsAny<List<byte>>(), It.IsAny<int>()))
                .Returns(new List<byte>() { 1, (byte)ResponseType.Ok + 0x80 });

            _otaService._flashInfo = new FlashInfo(0x0000, 20, 0x100);

            // Act
            Action action = () => { var result = _otaService.SendHexData(); };

            // Assert
            action.Should().Throw<OtaException>().WithMessage("Invalid flash write size [0xCE] Max: [0x40]");
        }

        [Fact]
        public void TestSendHexDataOk()
        {
            // Arrange

            _mockIntelHexReaderFactory
                .Setup(_ => _.Create(It.IsAny<Stream>()))
                .Returns(_mockIntelHexStreamReader.Object);

            _mockIntelHexStreamReader
                .SetupSequence(_ => _.ReadHexRecord())
                .Returns(new IntelHexRecord(0x100, IntelHexRecordType.Data, new List<byte>() { }))
                .Returns(new IntelHexRecord(0x200, IntelHexRecordType.Data, new List<byte>() { }))
                .Returns(new IntelHexRecord(0x000, IntelHexRecordType.EndOfFile, new List<byte>() { }));

            _mockRfmUsb
                .Setup(_ => _.TransmitReceive(It.IsAny<List<byte>>(), It.IsAny<int>()))
                .Returns(new List<byte>() { 1, (byte)ResponseType.Ok + 0x80 });

            _otaService._flashInfo = new FlashInfo(0x0000, 20, 0x100);
            // Act
            var result = _otaService.SendHexData();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void TestSetCrcNOk()
        {
            // Arrange
            _mockRfmUsb.Setup(_ => _.TransmitReceive(It.IsAny<List<byte>>(), It.IsAny<int>()))
                .Returns(new List<byte>() { 1, (byte)ResponseType.ErrorInvalidLength + 0x80 });

            // Act
            var result = _otaService.SetCrc();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void TestSetCrcOk()
        {
            // Arrange
            _mockRfmUsb.Setup(_ => _.TransmitReceive(It.IsAny<List<byte>>(), It.IsAny<int>()))
                .Returns(new List<byte>() { 5, (byte)ResponseType.Crc + 0x80, 0xAA, 0x55, 0xAA, 0x55 });

            // Act
            var result = _otaService.SetCrc();

            // Assert
            result.Should().BeTrue();
        }
    }
}