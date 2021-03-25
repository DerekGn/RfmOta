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

namespace RfmOta
{
    internal enum ResponseType
    {
        /// <summary>
        /// The requested operation executed correctly
        /// </summary>
        Ok,
        /// <summary>
        /// Calculate the application flash memory range crc
        /// </summary>
        Crc,
        /// <summary>
        /// Ping the boot loader
        /// </summary>
        Ping,
        /// <summary>
        /// Get the flash size from the target device
        /// </summary>
        FlashSize,
        /// <summary>
        /// The number of flash writes exceed the allowed number in one operation
        /// </summary>
        ErrorNumberWrites,
        /// <summary>
        /// The request message had an invalid length
        /// </summary>
        ErrorInvalidLength,
        /// <summary>
        /// 
        /// </summary>
        ErrorInvalidWrite,
        /// <summary>
        /// The flash write address is out of range
        /// </summary>
        ErrorInvalidWriteAddress
    };
}
