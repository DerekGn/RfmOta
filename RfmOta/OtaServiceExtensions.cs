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

using Microsoft.Extensions.DependencyInjection;
using RfmOta.Factory;
using RfmUsb;
using RfmUsb.Net;
using System.Diagnostics.CodeAnalysis;

namespace RfmOta
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extensions
    /// </summary>
    public static class OtaServiceExtensions
    {
        /// <summary>
        /// Add the Ota services to the <see cref="IServiceCollection"/>
        /// </summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add the ota services</param>
        /// <returns>The <see cref="IServiceCollection"/></returns>
        [ExcludeFromCodeCoverage]
        public static IServiceCollection AddOta(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IIntelHexStreamReaderFactory, IntelHexStreamReaderFactory>();
            serviceCollection.AddSingleton<IOtaService, OtaService>();
            serviceCollection.AddRfmUsb();
            return serviceCollection;
        }
    }
}
