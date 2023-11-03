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

using HexIO;
using Microsoft.Extensions.Logging;
using RfmOta.Exceptions;
using RfmOta.Factory;
using RfmOta.Payloads;
using RfmUsb.Net;
using RfmUsb.Net.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace RfmOta
{
    /// <summary>
    ///
    /// </summary>
    public class OtaService : IOtaService
    {
        internal FlashInfo _flashInfo;
        internal List<Func<IRfm, bool>> _steps;
        private const int FlashWriteRows = 2;
        private const int MaxFlashWriteSize = 64;
        private readonly IIntelHexStreamReaderFactory _hexStreamReaderFactory;
        private readonly ILogger<IOtaService> _logger;

        private uint _crc;
        private uint _flashWriteSize;
        private IRfm? _rfm;
        private Stream? _stream;

        /// <summary>
        /// Create an instance of a <see cref="OtaService"/>
        /// </summary>
        /// <param name="logger">The <see cref="ILogger{IOtaService}"/> instance</param>
        /// <param name="hexStreamReaderFactory">The <see cref="IIntelHexStreamReaderFactory"/> instance for creating <see cref="IntelHexStreamReader"/> instances</param>
        public OtaService(ILogger<IOtaService> logger, IIntelHexStreamReaderFactory hexStreamReaderFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hexStreamReaderFactory = hexStreamReaderFactory ?? throw new ArgumentNullException(nameof(hexStreamReaderFactory));

            _steps = new List<Func<IRfm, bool>>
            {
                PingBootLoader,
                GetFirmwareVersion,
                GetFlashSize,
                SendHexData,
                SetCrc,
                Reboot
            };
        }

        ///<inheritdoc/>
        public bool OtaUpdate(IRfm rfm, Stream stream, out uint crc)
        {
            _rfm = rfm ?? throw new ArgumentNullException(nameof(rfm));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            bool result = true;

            crc = 0;

            foreach (var step in _steps)
            {
                if (!step(rfm))
                {
                    result = false;
                    break;
                }
            }

            crc = _crc;

            _stream = null;
            _rfm = null;

            return result;
        }

        internal bool GetFirmwareVersion(IRfm rfm)
        {
            return HandleOperation(
                nameof(OtaService),
                () =>
                {
                    var response = SendRequest<FirmwareVersionResponse>(rfm, new FirmwareVersionRequest());

                    _logger.LogInformation("Firmware Version: [{version}]", response.Version);

                    return true;
                });
        }

        internal bool GetFlashSize(IRfm rfm)
        {
            return HandleOperation(
                nameof(OtaService),
                () =>
                {
                    _flashInfo = SendRequest<FlashSizeResponse>(rfm, new FlashSizeRequest()).Info;
                    _logger.LogInformation("FlashInfo: {flashInfo}", _flashInfo);

                    return true;
                });
        }

        internal bool PingBootLoader(IRfm rfm)
        {
            return HandleOperation(
                nameof(OtaService),
                () =>
                {
                    var response = SendRequest<PingResponse>(rfm, new PingRequest());

                    _logger.LogInformation("BootLoader Ping Ok. Bootloader Version: [{version}]",
                        response.BootLoaderVersion);

                    return true;
                });
        }

        internal bool Reboot(IRfm rfm)
        {
            return HandleOperation(
                nameof(OtaService),
                () =>
                {
                    SendRequest(rfm, new RebootRequest());

                    _logger.LogInformation("Reboot Pending");

                    return true;
                });
        }

        internal bool SendHexData(IRfm rfm)
        {
            return HandleOperation(
                nameof(OtaService),
                () =>
                {
                    using IIntelHexStreamReader hexReader = _hexStreamReaderFactory.Create(_stream);

                    do
                    {
                        FlashWrites flashWrites = GetFlashWrites(hexReader);

                        if (flashWrites.Writes.Count > 0)
                        {
                            var request = new FlashWriteRequest(flashWrites);
                            var response = SendRequest<OkResponse>(rfm, new FlashWriteRequest(flashWrites));

                            _logger.LogDebug("Total bytes flashed [{bytes}]", _flashWriteSize);
                        }
                        else
                        {
                            break;
                        }
                    } while (true);

                    _logger.LogInformation(
                        "[{name}] Flash Complete Image Size: [0x{flashWriteSize:X}] [{flashWriteSize}]",
                        nameof(SendHexData), _flashWriteSize, _flashWriteSize);
                    return true;
                });
        }

        internal bool SetCrc(IRfm rfm)
        {
            return HandleOperation(
                nameof(OtaService),
                () =>
                {
                    _crc = SendRequest<CrcResponse>(rfm, new CrcRequest(_flashWriteSize)).Crc;

                    _logger.LogInformation("Flash Crc: [0x{crc:X}]", _crc);

                    return true;
                });
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
                    _logger.LogDebug("Read Intel Hex Data Record [{intelHexRecord}]", intelHexRecord);

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
        private bool HandleOperation(string className, Func<bool> operation, [CallerMemberName] string memberName = "")
        {
            var sw = new Stopwatch();
            bool result;

            try
            {
                sw.Start();
                result = operation();
                sw.Stop();

                _logger.LogDebug("Executed [{className}].[{memberName}] in [{totalMilliseconds}] ms",
                    className, memberName, sw.Elapsed.TotalMilliseconds);
            }
            catch (RfmUsbTransmitException ex)
            {
                _logger.LogError("A transmission exception occurred executing [{className}].[{memberName}] Reason: [{message}]",
                    className, memberName, ex.Message);
                _logger.LogDebug(ex, "A transmission exception occurred executing [{className}].[{memberName}]",
                    className, memberName);

                result = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred");

                result = false;
            }

            return result;
        }

        private T SendRequest<T>(IRfm rfm, IRequest request, [CallerMemberName] string memberName = "") where T : IResponse
        {
            IResponse result = null;

            var sw = new Stopwatch();

            try
            {
                sw.Start();

                using var stream = new MemoryStream();
                request.Serialize(stream);

                var responseBytes = rfm.TransmitReceive(stream.ToArray(), 5000);

                var responseType = responseBytes[1];

#warning TODO response type validation

                switch (responseType)
                {
                    case (byte)ResponseType.Ping | 0x80:
                        result = new PingResponse();
                        result.Deserialize(responseBytes);
                        break;

                    case (byte)ResponseType.FlashSize | 0x80:
                        result = new FlashSizeResponse();
                        result.Deserialize(responseBytes);
                        break;

                    case (byte)ResponseType.Ok | 0x80:
                        result = new OkResponse();
                        result.Deserialize(responseBytes);
                        break;

                    case (byte)ResponseType.Crc | 0x80:
                        result = new CrcResponse();
                        result.Deserialize(responseBytes);
                        break;

                    case (byte)ResponseType.FirmwareVersion | 0x80:
                        result = new FirmwareVersionResponse();
                        result.Deserialize(responseBytes);
                        break;
                }
            }
            finally
            {
                sw.Stop();
            }

            _logger.LogDebug("BootLoader {memberName} Ok. Tx Time: [{elapsed} ms] Rx Rssi: [{rssi}]",
                memberName, (sw.ElapsedTicks * 1000 / Stopwatch.Frequency), rfm.LastRssi);

            return (T)result;
        }

        private void SendRequest(IRfm rfm, IRequest request, [CallerMemberName] string memberName = "")
        {
            var sw = new Stopwatch();

            try
            {
                sw.Start();

                using var stream = new MemoryStream();
                request.Serialize(stream);

                rfm.Transmit(stream.ToArray());
            }
            finally
            {
                sw.Stop();
            }

            _logger.LogInformation("BootLoader {memberName} Ok. Tx Time: [{elapsed} ms]",
                memberName, (sw.ElapsedTicks * 1000 / Stopwatch.Frequency));
        }
    }
}