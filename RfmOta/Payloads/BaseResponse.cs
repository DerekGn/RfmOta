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

using System;
using System.Collections.Generic;
using System.Linq;

namespace RfmOta.Payloads
{
    internal abstract class BaseResponse : IResponse
    {
        public BaseResponse(ResponseType responseType, byte expectedSize)
        {
            ResponseType = responseType;
            ExpectedSize = expectedSize;
        }

        public byte ExpectedSize { get; }

        public ResponseType ResponseType { get; }

        public virtual void Deserialize(IList<byte> bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Count == 0 || bytes.Count < ExpectedSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bytes),
                    "Invalid response received [{response}]",
                    BitConverter.ToString(bytes.ToArray()));
            }

            if (bytes[0] != ExpectedSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bytes),
                    $"Invalid Response Length: [{bytes[0]}] Expected [{ExpectedSize}]");
            }

            if (bytes[1] != (byte)ResponseType + 0x80)
            {
                throw new ArgumentOutOfRangeException($"BootLoader Invalid Response Type: [0x{(byte)(bytes[1] - 0x80):X2}]");
            }
        }
    }
}