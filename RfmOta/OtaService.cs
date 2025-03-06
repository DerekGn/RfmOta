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


// Ignore Spelling: Ota

using HexIO;
using Microsoft.Extensions.Logging;
using RfmOta.Exceptions;
using RfmOta.Factory;
using RfmUsb.Net;
using RfmUsb.Net.Exceptions;
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
        internal FlashInfo _flashInfo;
        internal List<Func<bool>> _steps;
        private const int FlashWriteRows = 2;
        private const int MaxFlashWriteSize = 64;
        private readonly IIntelHexStreamReaderFactory _hexStreamReaderFactory;
        private readonly ILogger<IOtaService> _logger;

        private readonly IRfm69 _rfmUsb;
        private uint _crc;
        private bool _disposedValue;
        private uint _flashWriteSize;
        private Stream _stream;

        /// <summary>
        /// Create an instance of a <see cref="OtaService"/>
        /// </summary>
        /// <param name="logger">The <see cref="ILogger{IOtaService}"/> instance</param>
        /// <param name="rfmUsb">The <see cref="IRfm69"/> instance</param>
        /// <param name="hexStreamReaderFactory">The <see cref="IIntelHexStreamReaderFactory"/> instance for creating <see cref="IntelHexStreamReader"/> instances</param>
        public OtaService(ILogger<OtaService> logger, IRfm69 rfmUsb, IIntelHexStreamReaderFactory hexStreamReaderFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rfmUsb = rfmUsb ?? throw new ArgumentNullException(nameof(rfmUsb));
            _hexStreamReaderFactory = hexStreamReaderFactory ?? throw new ArgumentNullException(nameof(hexStreamReaderFactory));

            _steps = new List<Func<bool>>
            {
                () => PingBootLoader(),
                () => GetFlashSize(),
                () => SendHexData(),
                () => SetCrc(),
                () => Reboot()
            };
        }

        public void Dispose()
        {
            // Do not change this code. Put clean-up code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ///<inheritdoc/>
        public bool OtaUpdate(sbyte outputPower, Stream stream, out uint crc)
        {
            if (outputPower < -2 || outputPower > 20)
                throw new ArgumentOutOfRangeException(nameof(outputPower));

            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            bool result = true;

            crc = 0;
            InitaliseRfmUsb(outputPower);

            foreach (var step in _steps)
            {
                if (!step())
                {
                    result = false;
                    break;
                }
            }

            crc = _crc;

            return result;
        }

        internal bool GetFlashSize()
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

                    _flashInfo = new FlashInfo(
                        BitConverter.ToUInt32(response.ToArray(), 2),
                        BitConverter.ToUInt32(response.ToArray(), 6),
                        BitConverter.ToUInt32(response.ToArray(), 10));

                    _logger.LogInformation("FlashInfo: {FlashInfo}", _flashInfo);

                    return true;
                });
        }

        internal bool PingBootLoader()
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

        internal bool Reboot()
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

        internal bool SendHexData()
        {
            return HandleRfmUsbOperation(
                nameof(OtaService),
                () =>
                {
                    using IIntelHexStreamReader hexReader = _hexStreamReaderFactory.Create(_stream);

                    do
                    {
                        FlashWrites flashWrites = GetFlashWrites(hexReader);

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

                    _logger.LogInformation(
                        "[{Name}] Flash Complete Image Size: [0x{FlashWriteSize:X}]",
                        nameof(SendHexData), _flashWriteSize);
                    return true;
                });
        }

        internal bool SetCrc()
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
                    _logger.LogInformation("Flash Crc: [0x{Crc:X}]", _crc);

                    return true;
                });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _rfmUsb?.Close();
                    _rfmUsb?.Dispose();
                }

                _disposedValue = true;
            }
        }

        private FlashWrites GetFlashWrites(IIntelHexStreamReader hexReader)
        {
            var flashWrites = new FlashWrites();

            bool done = false;

            do
            {
                IntelHexRecord intelHexRecord = hexReader.ReadHexRecord();

                if (intelHexRecord.RecordType == IntelHexRecordType.Data)
                {
                    _logger.LogDebug("Read Intel Hex Data Record [{IntelHexRecord}]", intelHexRecord);

                    if (intelHexRecord.Bytes.Count > MaxFlashWriteSize)
                    {
                        throw new OtaException($"Invalid flash write size [0x{intelHexRecord.Bytes.Count:X}] Max: [0x{MaxFlashWriteSize:X}]");
                    }

                    if (intelHexRecord.Offset > _flashInfo.UpperAddress)
                    {
                        throw new OtaException($"Flash offset [0x{intelHexRecord.Offset:X}] outside Flash range [0x{_flashInfo.UpperAddress}]");
                    }

                    var write = new List<byte>();
                    write.AddRange(BitConverter.GetBytes(intelHexRecord.Offset));
                    write.AddRange(BitConverter.GetBytes(intelHexRecord.RecordLength));
                    write.AddRange(intelHexRecord.Data);
                    flashWrites.AddWrite(write);

                    _flashWriteSize += (uint)intelHexRecord.Bytes.Count;
                }

                if (intelHexRecord.RecordType == IntelHexRecordType.EndOfFile)
                {
                    _logger.LogDebug($"Read Eof");
                    done = true;
                }

                if (flashWrites.Writes.Count == FlashWriteRows)
                {
                    _logger.LogDebug("Read [{FlashWriteRows}] Write Rows", FlashWriteRows);
                    done = true;
                }
            } while (!done);

            return flashWrites;
        }

        [DebuggerStepThrough]
        private bool HandleRfmUsbOperation(string className, Func<bool> operation, [CallerMemberName] string memberName = "")
        {
            var sw = new Stopwatch();
            bool result;

            try
            {
                sw.Start();
                result = operation();
                sw.Stop();

                _logger.LogDebug("Executed [{ClassName}].[{MemberName}] in [{TotalMilliseconds}] ms",
                    className, memberName, sw.Elapsed.TotalMilliseconds);
            }
            catch (RfmUsbTransmitException ex)
            {
                _logger.LogError("A transmission exception occurred executing [{ClassName}].[{MemberName}] Reason: [{Message}]",
                    className, memberName, ex.Message);
                _logger.LogDebug(ex, "A transmission exception occurred executing [{ClassName}].[{MemberName}]",
                    className, memberName);

                return false;
            }

            return result;
        }

        private void InitaliseRfmUsb(sbyte outputPower)
        {
            _logger.LogDebug($"Initialising the {nameof(IRfm69)} instance");

            _rfmUsb.ExecuteReset();

            _rfmUsb.PacketFormat = true;

            _rfmUsb.TxStartCondition = true;

            //_rfmUsb.RadioConfig = 23;

            _rfmUsb.OutputPower = outputPower;

            _rfmUsb.Sync = new List<byte>() { 0x55, 0x55 };

            _rfmUsb.Timeout = 5000;
        }

        private bool SendAndValidateResponse(IList<byte> request,
            int expectedSize, ResponseType expectedResponse,
            out IList<byte> response, [CallerMemberName] string memberName = "")
        {
            var sw = new Stopwatch();

            try
            {
                sw.Start();

                response = _rfmUsb.TransmitReceive(request, 5000);

                if (response.Count == 0 || response.Count < expectedSize)
                {
                    _logger.LogError("Invalid response received [{Response}]",
                        BitConverter.ToString(response.ToArray()));

                    return false;
                }

                if (response[0] != (byte)expectedSize)
                {
                    _logger.LogInformation("BootLoader Invalid {MemberName} Response Length: [{Response}]",
                        memberName, response[0]);

                    return false;
                }

                if (response[1] != (byte)expectedResponse + 0x80)
                {
                    _logger.LogInformation("BootLoader Invalid {MemberName} Response: [{Response}]",
                        memberName, (ResponseType)(response[1] - 0x80));

                    return false;
                }
            }
            finally
            {
                sw.Stop();
            }

            _logger.LogInformation("BootLoader {MemberName} Ok. Tx Time: [{Elapsed} ms]",
                memberName, (sw.ElapsedTicks * 1000 / Stopwatch.Frequency));

            return true;
        }

        private void SendRequest(IList<byte> request, [CallerMemberName] string memberName = "")
        {
            var sw = new Stopwatch();

            try
            {
                sw.Start();

                _rfmUsb.Transmit(request);
            }
            finally
            {
                sw.Stop();
            }

            _logger.LogInformation("BootLoader {MemberName} Ok. Tx Time: [{Elapsed} ms]",
                memberName, (sw.ElapsedTicks * 1000 / Stopwatch.Frequency));
        }
    }
}