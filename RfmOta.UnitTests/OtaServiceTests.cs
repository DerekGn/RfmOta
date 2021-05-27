using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RfmUsb;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace RfmOta.UnitTests
{
    public class OtaServiceTests
    {
        private readonly OtaService _otaService;
        private readonly Mock<IRfmUsb> _mockRfmUsb;

        public OtaServiceTests()
        {
            _mockRfmUsb = new Mock<IRfmUsb>();

            _otaService = new OtaService(Mock.Of<ILogger<IOtaService>>(), _mockRfmUsb.Object);
        }

        [Fact]
        public void TestSetCrcOk()
        {
            // Arrange
            _mockRfmUsb.Setup(_ => _.TransmitReceive(It.IsAny<List<byte>>(), It.IsAny<int>()))
                .Returns(new List<byte>() { 5, (byte)ResponseType.Crc + 0x80, 0xAA, 0x55, 0xAA, 0x55});

            // Act
            var result = _otaService.SetCrc();

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
        public void TestSendHexDataOk()
        {
            // Arrange
            _otaService.SetStream(GetHexStream());

            _mockRfmUsb.Setup(_ => _.TransmitReceive(It.IsAny<List<byte>>(), It.IsAny<int>()))
                .Returns(new List<byte>() { 1, (byte)ResponseType.Ok + 0x80 });

            // Act
            var result = _otaService.SendHexData();

            // Assert
            result.Should().BeTrue();
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

        private Stream GetHexStream()
        {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);

            sw.WriteLine(":1028000040200020A12C00009D2C00009D2C0000E9");
            sw.WriteLine(":1028100000000000000000000000000000000000B8");
            sw.WriteLine(":102820000000000000000000000000009D2C0000DF");
            sw.WriteLine(":1028300000000000000000009D2C00009D2C000006");
            
            sw.Flush();
            ms.Position = 0;

            return ms;
        }
    }
}
