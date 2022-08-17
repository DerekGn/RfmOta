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

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace RfmOta
{
    /// <summary>
    /// The flash info read from the device
    /// </summary>
    internal class FlashInfo
    {
        /// <summary>
        /// Create an instance of a <see cref="FlashInfo"/>
        /// </summary>
        /// <param name="startAddress">The start address of the flash</param>
        /// <param name="numberOfPages">The number of flash pages</param>
        /// <param name="pageSize">The page size in bytes</param>
        public FlashInfo(uint startAddress, uint numberOfPages, uint pageSize)
        {
            StartAddress = startAddress;
            NumberOfPages = numberOfPages;
            PageSize = pageSize;
        }

        /// <summary>
        /// The start address of the flash segment
        /// </summary>
        public uint StartAddress { get; }

        /// <summary>
        /// The number of flash pages
        /// </summary>
        public uint NumberOfPages { get; }

        /// <summary>
        /// The page size in bytes
        /// </summary>
        public uint PageSize { get; }

        /// <summary>
        /// The upper address of the flash memory
        /// </summary>
        public uint UpperAddress => StartAddress + (PageSize * NumberOfPages);
        
        /// <inheritdoc/>
        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append($"{nameof(StartAddress)}: 0x{StartAddress:X8} ");
            stringBuilder.Append($"{nameof(NumberOfPages)}: 0x{NumberOfPages:X8} ");
            stringBuilder.Append($"{nameof(PageSize)}: 0x{PageSize:X8} ");
            stringBuilder.Append($"{nameof(UpperAddress)}: 0x{UpperAddress:X8}");

            return stringBuilder.ToString();
        }
    }
}
