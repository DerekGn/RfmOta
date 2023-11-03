/*
* MIT License
*
* Copyright (c) 2023 Derek Goslin
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
using RfmOta.Payloads;
using System.IO;
using Xunit;

namespace RfmOta.UnitTests
{
    public class PayloadTests
    {
        [Fact]
        public void TestPingRequestSerialize()
        {
            // Arrange
            var stream = new MemoryStream();

            var pingRequest = new PingRequest();

            // Act
            pingRequest.Serialize(stream);

            // Assert
            stream.Length.Should().Be(2);
            stream.ToArray().Should().Contain(new byte[] { 0x01, 0x01 });
        }

        [Fact]
        public void TestPingResponseDeserialize()
        {
            // Arrange
            var bytes = new byte[] {
                0x07, (byte)ResponseType.Ping + 0x80, (byte)'v', (byte)'1', (byte)'.', (byte)'0',(byte)'.',(byte)'0',};

            var pingResponse = new PingResponse();

            // Act
            pingResponse.Deserialize(bytes);

            // Assert
            pingResponse.BootLoaderVersion.Should().Be("v1.0.0");
        }
    }
}