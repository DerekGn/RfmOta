/*
* MIT License
*
* Copyright (c) 2021 Derek Goslin 
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

using HexIO;
using Microsoft.Extensions.Logging;
using RfmOta.Exceptions;
using RfmUsb;
using RfmUsb.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RfmOta
{
    internal class OtaService : IOtaService
    {
        private readonly ILogger<IOtaService> _logger;
        private readonly List<Func<bool>> _steps;
        private readonly IRfmUsb _rfmUsb;

        private const int MaxFlashWriteSize = 64;
        private const int FlashWriteRows = 2;

        private uint _flashWriteSize;
        private uint _numberOfPages;
        private uint _startAddress;
        private uint _pageSize;
        private Stream _stream;
        private uint _crc;

        public OtaService(ILogger<IOtaService> logger, IRfmUsb rfmUsb)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rfmUsb = rfmUsb ?? throw new ArgumentNullException(nameof(rfmUsb));

            _steps = new List<Func<bool>>
            {
                () => PingBootLoader(),
                () => GetFlashSize(),
                () => SendHexData(),
                () => SetCrc(),
                () => Reboot()
            };
        }

        public bool OtaUpdate(string serialPort, int baudRate, byte outputPower, Stream stream, out uint crc)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            bool result = true;

            try
            {
                crc = 0;
                InitaliseRfmUsb(serialPort, baudRate, outputPower);

                foreach (var step in _steps)
                {
                    if (!step())
                    {
                        result = false;
                        break;
                    }
                }

                crc = _crc;
            }
            finally
            {
                _rfmUsb?.Close();
            }

            return result;
        }

        private bool SetCrc()
        {
            return HandleRfmUsbOperation(
                nameof(OtaService),
                () =>
                {
                    var requestBytes = new List<byte>() { 0x05, (byte)RequestType.Crc };
                    requestBytes.AddRange(BitConverter.GetBytes(_flashWriteSize));

                    if (!SendAndValidateResponse(
                        requestBytes,
                        PayloadSizes.CrcResponse,
                        ResponseType.Crc, out IList<byte> response))
                    {
                        return false;
                    }

                    _crc = BitConverter.ToUInt32(response.ToArray(), 2);
                    _logger.LogInformation($"Flash Crc: [0x{_crc:X}]");

                    return true;
                });
        }

        private bool Reboot()
        {
            return HandleRfmUsbOperation(
                nameof(OtaService),
                () =>
                {
                    SendRequest(new List<byte>() { 0x01, (byte)RequestType.Reboot });

                    _logger.LogInformation("Reboot Pending");

                    return true;
                });
        }

        private bool SendHexData()
        {
            return HandleRfmUsbOperation(
                nameof(OtaService),
                () =>
                {
                    using IntelHexReader hexReader = new IntelHexReader(_stream);

                    do
                    {
                        var flashWrites = new FlashWrites();

                        for (int i = 0; i < FlashWriteRows; i++)
                        {
                            if (hexReader.Read(out uint address, out IList<byte> hexData))
                            {
                                if (hexData.Count > 0)
                                {
                                    if (hexData.Count > MaxFlashWriteSize)
                                    {
                                        throw new OtaException($"Invalid flash write size [{hexData.Count}] Max: [{MaxFlashWriteSize}]");
                                    }

                                    _logger.LogInformation($"Writing Address: [0x{address:X}] Count: [0x{hexData.Count:X2}]" +
                                            $" Data: [{BitConverter.ToString(hexData.ToArray()).Replace("-", "")}]");
                                    var write = new List<byte>();
                                    write.AddRange(BitConverter.GetBytes(address));
                                    write.AddRange(BitConverter.GetBytes(hexData.Count));
                                    write.AddRange(hexData);
                                    flashWrites.AddWrite(write);

                                    _flashWriteSize += (uint)hexData.Count;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (flashWrites.Writes.Count > 0)
                        {
                            var payload = flashWrites.GetWritesBytes();

                            var request = new List<byte>
                            {
                                (byte)(payload.Count + 2),
                                (byte)RequestType.Write,
                                (byte)flashWrites.Writes.Count,
                            };
                            request.AddRange(payload);

                            if (!SendAndValidateResponse(
                                request, PayloadSizes.OkResponse, ResponseType.Ok, out IList<byte> response))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            break;
                        }
                    } while (true);

                    _logger.LogInformation($"[{nameof(SendHexData)}] Flash Complete Image Size: [0x{_flashWriteSize:X}]");
                    return true;
                });
        }

        private bool GetFlashSize()
        {
            return HandleRfmUsbOperation(
                nameof(OtaService),
                () =>
                {
                    if (!SendAndValidateResponse(
                        new List<byte>() { 0x01, (byte)RequestType.FlashSize },
                        PayloadSizes.FlashSizeResponse, ResponseType.FlashSize, out IList<byte> response))
                    {
                        return false;
                    }

                    _startAddress = BitConverter.ToUInt32(response.ToArray(), 2);
                    _numberOfPages = BitConverter.ToUInt32(response.ToArray(), 6);
                    _pageSize = BitConverter.ToUInt32(response.ToArray(), 10);
                    _logger.LogInformation($"App Start Address: [0x{_startAddress:X}] Number Of Pages: [0x{_numberOfPages:X}] " +
                        $"Page Size: [0x{_pageSize:X}] Flash Size: [0x{_numberOfPages * _pageSize:X}]");

                    return true;
                });
        }

        private bool PingBootLoader()
        {
            return HandleRfmUsbOperation(
                nameof(OtaService),
                () =>
                {
                    if (!SendAndValidateResponse(
                    new List<byte>() { 0x01, (byte)RequestType.Ping },
                    PayloadSizes.PingResponse, ResponseType.Ping, out IList<byte> response))
                    {
                        _logger.LogInformation("BootLoader Ping NOk");
                        return false;
                    }

                    _logger.LogInformation("BootLoader Ping Ok");

                    return true;
                });
        }

        private bool SendAndValidateResponse(IList<byte> request,
            int expectedSize, ResponseType expectedResponse,
            out IList<byte> response, [CallerMemberName] string memberName = "")
        {
            Stopwatch sw = new Stopwatch();

            try
            {
                sw.Start();

                response = _rfmUsb.TransmitReceive(request, 5000);

                if (response.Count == 0 || response.Count < expectedSize)
                {
                    _logger.LogError($"Invalid response received [{BitConverter.ToString(response.ToArray())}]");

                    return false;
                }

                if (response[0] != (byte)expectedSize)
                {
                    _logger.LogInformation($"BootLoader Invalid {memberName} Response Length: [{response[0]}]");

                    return false;
                }

                if (response[1] != (byte)expectedResponse + 0x80)
                {
                    _logger.LogInformation($"BootLoader Invalid {memberName} Response: [{(ResponseType)(response[1] - 0x80)}]");

                    return false;
                }
            }
            finally
            {
                sw.Stop();
            }

            _logger.LogInformation($"BootLoader {memberName} Ok. Tx Time: [{sw.ElapsedTicks * 1000 / Stopwatch.Frequency} ms]");

            return true;
        }
        private void SendRequest(IList<byte> request, [CallerMemberName] string memberName = "")
        {
            Stopwatch sw = new Stopwatch();

            try
            {
                sw.Start();

                _rfmUsb.Transmit(request);
            }
            finally
            {
                sw.Stop();
            }

            _logger.LogInformation($"BootLoader {memberName} Ok. Tx Time: [{sw.ElapsedTicks * 1000 / Stopwatch.Frequency} ms]");
        }
        private bool HandleRfmUsbOperation(string className, Func<bool> operation, [CallerMemberName] string memberName = "")
        {
            Stopwatch sw = new Stopwatch();
            bool result;

            try
            {
                sw.Start();
                result = operation();
                sw.Stop();

                _logger.LogDebug($"Executed [{className}].[{memberName}] in [{sw.Elapsed.TotalMilliseconds}] ms");
            }
            catch (RfmUsbTransmitException ex)
            {
                _logger.LogError($"A transmission exception occurred executing [{className}].[{memberName}] Reason: [{ex.Message}]");
                _logger.LogDebug(ex, $"A transmission exception occurred executing [{className}].[{memberName}]");

                return false;
            }

            return result;
        }

        private void InitaliseRfmUsb(string serialPort, int baudRate, byte outputPower)
        {
            _rfmUsb.Open(serialPort, baudRate);

            _rfmUsb.Reset();

            _logger.LogInformation(_rfmUsb.Version);

            _rfmUsb.PacketFormat = true;

            _rfmUsb.TxStartCondition = true;

            _rfmUsb.RadioConfig = 23;

            _rfmUsb.OutputPower = outputPower;

            _rfmUsb.Sync = new List<byte>() { 0x55, 0x55 };

            _rfmUsb.Timeout = 5000;
        }

        #region
        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _rfmUsb?.Close();
                    _rfmUsb?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~OtaService()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
