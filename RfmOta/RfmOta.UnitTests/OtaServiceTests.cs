using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RfmUsb;
using System.Collections.Generic;
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

            // Act
            var result = _otaService.SendHexData();

            // Assert
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
    }
}
