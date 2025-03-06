﻿/*
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

using RfmUsb.Net;
using System;
using System.IO;

namespace RfmOta
{
    /// <summary>
    /// Defines the interface for the Ota service
    /// </summary>
    public interface IOtaService : IDisposable
    {
        /// <summary>
        /// Perform an Ota update of an Rfm69 node that is running in bootloader mode
        /// </summary>
        /// <param name="outputPower">The output power for the RfmUsb</param>
        /// <param name="stream">The stream for the hex file to upload to the device</param>
        /// <param name="crc">The crc calculated for the uploaded flash image</param>
        /// <returns>true if the update succeeds</returns>
        /// <remarks>The <see cref="IOtaService"/> depends on an <see cref="IRfm69"/> instance to transmit and receive packets.
        /// The containing application must open and close the <see cref="IRfm69"/> instance.</remarks>
        bool OtaUpdate(sbyte outputPower, Stream stream, out uint crc);
    }
}
